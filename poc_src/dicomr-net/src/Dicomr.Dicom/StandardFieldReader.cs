using System.Globalization;
using Dicomr.Core;
using FellowOakDicom;

namespace Dicomr.Dicom;

public static class StandardFieldReader
{
    public static IReadOnlyList<FieldValue> Read(DicomDataset dataset, List<StudyWarning> warnings, string? path)
    {
        var fields = new List<FieldValue>();
        foreach (var binding in StandardFields.All)
        {
            if (!dataset.Contains(binding.Tag))
            {
                continue;
            }

            try
            {
                if (!TryRead(dataset, binding, out var field) || field is null)
                {
                    continue;
                }

                if (NonZeroPolicy.Omit(field))
                {
                    continue;
                }

                fields.Add(field with { Source = FieldSource.Standard });
            }
            catch (DicomDataException ex)
            {
                warnings.Add(new StudyWarning
                {
                    Code = "field.unparsed",
                    Path = path,
                    Message = $"{binding.Keyword} ({FormatTag(binding.Tag)}) could not be read: {ex.Message}",
                });
            }
        }

        return fields;
    }

    public static bool TryRead(DicomDataset dataset, TagBinding binding, out FieldValue? field)
    {
        field = null;
        var vr = VrOf(binding.Tag);
        var tag = FormatTag(binding.Tag);
        switch (binding.Kind)
        {
            case ValueKind.Text:
            {
                var text = ReadTexts(dataset, binding.Tag);
                if (text.Count == 0)
                {
                    return false;
                }

                field = FieldRegistry.Create(
                    binding.Keyword,
                    text.Count == 1 ? new FieldScalar.Text(text[0]) : new FieldScalar.TextList(text),
                    tag: tag,
                    vr: vr);
                return true;
            }
            case ValueKind.TextList:
            {
                var text = ReadTexts(dataset, binding.Tag);
                if (text.Count == 0)
                {
                    return false;
                }

                field = FieldRegistry.Create(binding.Keyword, new FieldScalar.TextList(text), tag: tag, vr: vr);
                return true;
            }
            case ValueKind.Number:
            {
                var numbers = ReadNumbers(dataset, binding.Tag);
                if (numbers.Count == 0)
                {
                    return false;
                }

                FieldScalar scalar = numbers.Count == 1
                    ? new FieldScalar.Number(numbers[0])
                    : new FieldScalar.NumberList(numbers);
                field = FieldRegistry.Create(binding.Keyword, scalar, tag: tag, vr: vr);
                return true;
            }
            case ValueKind.NumberList:
            {
                var numbers = ReadNumbers(dataset, binding.Tag);
                if (numbers.Count == 0)
                {
                    return false;
                }

                field = FieldRegistry.Create(binding.Keyword, new FieldScalar.NumberList(numbers), tag: tag, vr: vr);
                return true;
            }
            case ValueKind.Integer:
            {
                if (!TryReadInteger(dataset, binding.Tag, out var integer))
                {
                    return false;
                }

                field = FieldRegistry.Create(binding.Keyword, new FieldScalar.Integer(integer), tag: tag, vr: vr);
                return true;
            }
            default:
                return false;
        }
    }

    public static string FormatTag(DicomTag tag) => $"{tag.Group:X4},{tag.Element:X4}";

    public static string? VrOf(DicomTag tag)
    {
        var entry = DicomDictionary.Default[tag];
        return entry?.ValueRepresentations.FirstOrDefault()?.Code;
    }

    private static List<string> ReadTexts(DicomDataset dataset, DicomTag tag)
    {
        if (dataset.TryGetValues(tag, out string[] values))
        {
            return values
                .Select(value => value.Trim())
                .Where(value => value.Length > 0)
                .ToList();
        }

        if (dataset.TryGetSingleValue(tag, out string single) && !string.IsNullOrWhiteSpace(single))
        {
            return [single.Trim()];
        }

        return [];
    }

    private static List<double> ReadNumbers(DicomDataset dataset, DicomTag tag)
    {
        if (dataset.TryGetValues(tag, out double[] doubles) && doubles.Length > 0)
        {
            return doubles.ToList();
        }

        if (dataset.TryGetValues(tag, out float[] floats) && floats.Length > 0)
        {
            return floats.Select(value => (double)value).ToList();
        }

        if (dataset.TryGetValues(tag, out int[] ints) && ints.Length > 0)
        {
            return ints.Select(value => (double)value).ToList();
        }

        if (dataset.TryGetValues(tag, out string[] strings) && strings.Length > 0)
        {
            var parsed = new List<double>(strings.Length);
            foreach (var text in strings)
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    throw new DicomDataException($"Value '{text}' is not a number.");
                }

                parsed.Add(number);
            }

            return parsed;
        }

        return [];
    }

    private static bool TryReadInteger(DicomDataset dataset, DicomTag tag, out long integer)
    {
        integer = 0;
        if (dataset.TryGetSingleValue(tag, out int value))
        {
            integer = value;
            return true;
        }

        if (dataset.TryGetSingleValue(tag, out long longValue))
        {
            integer = longValue;
            return true;
        }

        if (dataset.TryGetSingleValue(tag, out string text)
            && long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
        {
            return true;
        }

        return false;
    }
}
