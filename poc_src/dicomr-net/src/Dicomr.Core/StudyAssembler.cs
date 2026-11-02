using System.Globalization;

namespace Dicomr.Core;

public static class StudyAssembler
{
    public static StudyProtocol Assemble(
        IReadOnlyList<InstanceRecord> instances,
        ReadStats? stats = null,
        IReadOnlyList<StudyWarning>? folderWarnings = null)
    {
        ArgumentNullException.ThrowIfNull(instances);
        if (instances.Count == 0)
        {
            throw new ArgumentException("A study needs at least one instance.", nameof(instances));
        }

        var ordered = instances
            .OrderBy(instance => instance.SeriesNumber ?? int.MaxValue)
            .ThenBy(instance => instance.InstanceNumber ?? int.MaxValue)
            .ThenBy(instance => instance.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var acquisitions = ordered
            .GroupBy(AcquisitionKey, StringComparer.Ordinal)
            .Select(group => BuildAcquisition(group.Key, group.ToList()))
            .OrderBy(item => item.MinSeries)
            .ThenBy(item => item.Record.Name, StringComparer.Ordinal)
            .Select(item => item.Record)
            .ToList();

        var first = ordered[0];
        var warnings = new List<StudyWarning>();
        if (folderWarnings is not null)
        {
            warnings.AddRange(folderWarnings);
        }

        warnings.AddRange(ordered.SelectMany(instance => instance.Warnings));
        warnings.AddRange(CollectSeriesWarnings(ordered));
        warnings.AddRange(PrivateHeaderWarnings(ordered));

        return new StudyProtocol
        {
            Study = new StudyIdentity
            {
                StudyInstanceUid = first.StudyInstanceUid,
                StudyDescription = FirstText(ordered.Select(instance => instance.StudyDescription)),
                PatientId = FirstText(ordered.Select(instance => instance.PatientId)),
                PatientName = FirstText(ordered.Select(instance => instance.PatientName)),
                Manufacturer = FirstText(ordered.Select(instance => instance.Manufacturer)),
                ManufacturerModelName = FirstText(ordered.Select(instance => instance.ManufacturerModelName)),
                MagneticFieldStrengthTesla = FirstNumber(ordered, FieldKeys.MagneticFieldStrength),
                Modality = FirstText(ordered.Select(instance => instance.Modality)),
            },
            Acquisitions = acquisitions,
            Stats = stats ?? new ReadStats
            {
                FilesSeen = ordered.Count,
                FilesRead = ordered.Count,
                FilesSkipped = 0,
                Studies = 1,
            },
            Warnings = Dedupe(warnings),
        };
    }

    private static (AcquisitionRecord Record, int MinSeries) BuildAcquisition(
        string name, List<InstanceRecord> instances)
    {
        var series = instances
            .GroupBy(instance => instance.SeriesInstanceUid, StringComparer.Ordinal)
            .Select(group => BuildSeries(group.ToList()))
            .OrderBy(record => record.SeriesNumber ?? int.MaxValue)
            .ThenBy(record => record.SeriesDescription, StringComparer.Ordinal)
            .ToList();

        var record = new AcquisitionRecord
        {
            Name = name,
            ProtocolName = FirstText(instances.Select(instance => instance.ProtocolName)),
            SequenceName = FirstText(instances.Select(instance => instance.SequenceName)),
            Series = series,
        };
        var minSeries = series.Select(item => item.SeriesNumber ?? int.MaxValue).DefaultIfEmpty(int.MaxValue).Min();
        return (record, minSeries);
    }

    private static SeriesRecord BuildSeries(List<InstanceRecord> instances)
    {
        var first = instances[0];
        var fields = MergeFields(instances);
        var counts = new List<DistinctFieldCount>();
        AddGeometryCount(counts, instances, FieldKeys.ImagePositionPatient);
        AddGeometryCount(counts, instances, FieldKeys.SliceLocation);
        var instanceNumbers = instances
            .Select(instance => instance.InstanceNumber)
            .Where(number => number.HasValue)
            .Select(number => number!.Value)
            .Distinct()
            .Count();
        if (instanceNumbers > 0)
        {
            counts.Add(new DistinctFieldCount { Keyword = FieldKeys.InstanceNumber, Count = instanceNumbers });
        }

        return new SeriesRecord
        {
            SeriesInstanceUid = first.SeriesInstanceUid,
            SeriesDescription = FirstText(instances.Select(instance => instance.SeriesDescription)),
            SeriesNumber = first.SeriesNumber,
            SeriesTime = FirstText(instances.Select(instance => instance.SeriesTime)),
            SopClassUid = first.SopClassUid,
            Encoding = first.Encoding,
            InstanceCount = instances.Count,
            FrameCount = instances.Sum(instance => Math.Max(instance.FrameCount, 1)),
            Fields = fields,
            PrivateCreators = instances
                .SelectMany(instance => instance.PrivateCreators)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(creator => creator, StringComparer.Ordinal)
                .ToList(),
            DistinctCounts = counts,
        };
    }

    private static IReadOnlyList<FieldValue> MergeFields(List<InstanceRecord> instances)
    {
        var keywords = instances
            .SelectMany(instance => instance.Fields)
            .Select(field => field.Keyword)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(CardIndex)
            .ThenBy(keyword => keyword, StringComparer.Ordinal);

        var merged = new List<FieldValue>();
        foreach (var keyword in keywords)
        {
            if (FieldKeys.CountOnly.Contains(keyword))
            {
                continue;
            }

            var values = instances
                .Select(instance => instance.Fields.FirstOrDefault(field => field.Keyword == keyword))
                .Where(field => field is not null)
                .Cast<FieldValue>()
                .ToList();
            if (values.Count == 0)
            {
                continue;
            }

            if (FieldKeys.VaryingScalars.Contains(keyword))
            {
                merged.Add(MergeScalars(keyword, values));
            }
            else if (FieldKeys.VaryingTuples.Contains(keyword))
            {
                merged.Add(MergeTuples(keyword, values));
            }
            else
            {
                merged.Add(values[0]);
            }
        }

        return merged;
    }

    private static FieldValue MergeScalars(string keyword, List<FieldValue> values)
    {
        var numbers = new List<double>();
        foreach (var value in values)
        {
            switch (value.Value)
            {
                case FieldScalar.Number number:
                    numbers.Add(number.NumberValue);
                    break;
                case FieldScalar.Integer integer:
                    numbers.Add(integer.IntegerValue);
                    break;
                case FieldScalar.NumberList list:
                    numbers.AddRange(list.Items);
                    break;
            }
        }

        var distinct = DistinctNumbers(numbers);
        var template = values[0];
        if (distinct.Count <= 1)
        {
            var scalar = distinct.Count == 1
                ? new FieldScalar.Number(distinct[0])
                : template.Value;
            return template with { Value = scalar, Varying = false };
        }

        return template with
        {
            Value = new FieldScalar.NumberList(distinct),
            Varying = true,
        };
    }

    private static FieldValue MergeTuples(string keyword, List<FieldValue> values)
    {
        var sequences = values
            .Select(TupleOf)
            .Distinct(SequenceComparer.Instance)
            .ToList();
        var template = values[0];
        if (sequences.Count <= 1)
        {
            return template with { Varying = false };
        }

        var joined = sequences
            .Select(sequence => string.Join('\\', sequence))
            .OrderBy(text => text, StringComparer.Ordinal)
            .ToList();
        return template with
        {
            Value = new FieldScalar.TextList(joined),
            Varying = true,
        };
    }

    private static IReadOnlyList<StudyWarning> CollectSeriesWarnings(List<InstanceRecord> instances)
    {
        var warnings = new List<StudyWarning>();
        foreach (var series in instances.GroupBy(instance => instance.SeriesInstanceUid, StringComparer.Ordinal))
        {
            var members = series.OrderBy(instance => instance.InstanceNumber ?? int.MaxValue).ToList();
            var keywords = members.SelectMany(instance => instance.Fields).Select(field => field.Keyword).Distinct();
            foreach (var keyword in keywords)
            {
                if (FieldKeys.VaryingScalars.Contains(keyword) || FieldKeys.VaryingTuples.Contains(keyword) || FieldKeys.CountOnly.Contains(keyword))
                {
                    continue;
                }

                var present = members
                    .Select(instance => (Instance: instance, Field: instance.Fields.FirstOrDefault(field => field.Keyword == keyword)))
                    .Where(pair => pair.Field is not null)
                    .ToList();
                if (present.Count < 2)
                {
                    continue;
                }

                var first = present[0].Field!;
                var conflict = present.FirstOrDefault(pair => !Equivalent(first, pair.Field!));
                if (conflict.Field is null)
                {
                    continue;
                }

                warnings.Add(new StudyWarning
                {
                    Code = "series.inconsistent",
                    Path = conflict.Instance.Path,
                    Message = $"{keyword} changes inside series {series.Key}. Kept {Describe(first)}; also saw {Describe(conflict.Field)}.",
                });
            }
        }

        return warnings;
    }

    private static IEnumerable<StudyWarning> PrivateHeaderWarnings(List<InstanceRecord> instances)
    {
        var siemens = instances.FirstOrDefault(instance =>
            Contains(instance.Manufacturer, "SIEMENS")
            && instance.PrivateCreators.Any(creator => Contains(creator, "SIEMENS CSA HEADER")));
        if (siemens is not null)
        {
            yield return Notice(siemens, "Siemens CSA headers are present in this study and are not decoded yet. Phase 3 reads them.");
        }

        var philips = instances.FirstOrDefault(instance =>
            instance.PrivateCreators.Any(creator => Contains(creator, "Philips Imaging DD")));
        if (philips is not null)
        {
            yield return Notice(philips, "Philips Imaging DD private elements are present and are not decoded yet. Phase 3 reads them.");
        }

        var ge = instances.FirstOrDefault(instance =>
            instance.PrivateCreators.Any(creator => Contains(creator, "GEMS_PARM_01")));
        if (ge is not null)
        {
            yield return Notice(ge, "GE GEMS_PARM_01 private elements are present and are not decoded yet. Phase 3 reads them.");
        }
    }

    private static StudyWarning Notice(InstanceRecord instance, string message) => new()
    {
        Code = "private.undecoded",
        Path = instance.Path,
        Message = message,
    };

    private static void AddGeometryCount(List<DistinctFieldCount> counts, List<InstanceRecord> instances, string keyword)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sample in instances.SelectMany(instance => instance.Geometry))
        {
            if (!string.Equals(sample.Keyword, keyword, StringComparison.Ordinal))
            {
                continue;
            }

            var key = string.Join(
                ',',
                sample.Components.Select(component => Math.Round(component, 3).ToString("0.###", CultureInfo.InvariantCulture)));
            seen.Add(key);
        }

        if (seen.Count > 0)
        {
            counts.Add(new DistinctFieldCount { Keyword = keyword, Count = seen.Count });
        }
    }

    private static string AcquisitionKey(InstanceRecord instance)
    {
        var raw = FirstText([instance.ProtocolName, instance.SequenceName, instance.SeriesDescription]) ?? "Unknown";
        var cleaned = CleanName.Collapse(raw);
        return cleaned.Length == 0 ? "Unknown" : cleaned;
    }

    private static int CardIndex(string keyword)
    {
        var index = 0;
        foreach (var name in FieldKeys.CardOrder)
        {
            if (string.Equals(name, keyword, StringComparison.Ordinal))
            {
                return index;
            }

            index++;
        }

        return int.MaxValue;
    }

    private static List<double> DistinctNumbers(IEnumerable<double> values)
    {
        var distinct = new List<double>();
        foreach (var value in values)
        {
            if (distinct.All(existing => Math.Abs(existing - value) > 0.0001))
            {
                distinct.Add(value);
            }
        }

        distinct.Sort();
        return distinct;
    }

    private static bool Equivalent(FieldValue left, FieldValue right) =>
        Equivalent(left.Value, right.Value);

    private static bool Equivalent(FieldScalar left, FieldScalar right) => (left, right) switch
    {
        (FieldScalar.Number a, FieldScalar.Number b) => Math.Abs(a.NumberValue - b.NumberValue) <= 0.0001,
        (FieldScalar.Integer a, FieldScalar.Integer b) => a.IntegerValue == b.IntegerValue,
        (FieldScalar.Number a, FieldScalar.Integer b) => Math.Abs(a.NumberValue - b.IntegerValue) <= 0.0001,
        (FieldScalar.Integer a, FieldScalar.Number b) => Math.Abs(a.IntegerValue - b.NumberValue) <= 0.0001,
        (FieldScalar.Text a, FieldScalar.Text b) => string.Equals(a.TextValue, b.TextValue, StringComparison.Ordinal),
        (FieldScalar.NumberList a, FieldScalar.NumberList b) => a.Items.Count == b.Items.Count
            && a.Items.Zip(b.Items).All(pair => Math.Abs(pair.First - pair.Second) <= 0.0001),
        (FieldScalar.TextList a, FieldScalar.TextList b) => a.Items.SequenceEqual(b.Items, StringComparer.Ordinal),
        _ => false,
    };

    private static string[] TupleOf(FieldValue field) => field.Value switch
    {
        FieldScalar.TextList list => list.Items.ToArray(),
        FieldScalar.Text text => [text.TextValue],
        _ => [Describe(field)],
    };

    private static string Describe(FieldValue field) => field.Value switch
    {
        FieldScalar.Text text => text.TextValue,
        FieldScalar.Number number => number.NumberValue.ToString(CultureInfo.InvariantCulture),
        FieldScalar.Integer integer => integer.IntegerValue.ToString(CultureInfo.InvariantCulture),
        FieldScalar.TextList list => string.Join('\\', list.Items),
        FieldScalar.NumberList list => string.Join('\\', list.Items.Select(item => item.ToString(CultureInfo.InvariantCulture))),
        _ => "",
    };

    private static string? FirstText(IEnumerable<string?> values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static double? FirstNumber(IEnumerable<InstanceRecord> instances, string keyword)
    {
        foreach (var instance in instances)
        {
            var field = instance.Fields.FirstOrDefault(item => item.Keyword == keyword);
            if (field?.AsNumber is double number)
            {
                return number;
            }
        }

        return null;
    }

    private static bool Contains(string? value, string fragment) =>
        value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;

    private static List<StudyWarning> Dedupe(List<StudyWarning> warnings)
    {
        var kept = new List<StudyWarning>();
        foreach (var warning in warnings)
        {
            if (warning.Code == "private.undecoded"
                && kept.Any(existing => existing.Code == warning.Code && existing.Message == warning.Message))
            {
                continue;
            }

            kept.Add(warning);
        }

        return kept;
    }

    private sealed class SequenceComparer : IEqualityComparer<string[]>
    {
        public static readonly SequenceComparer Instance = new();

        public bool Equals(string[]? x, string[]? y) =>
            x is not null && y is not null && x.SequenceEqual(y, StringComparer.Ordinal);

        public int GetHashCode(string[] obj)
        {
            var hash = new HashCode();
            foreach (var item in obj)
            {
                hash.Add(item, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }
    }
}
