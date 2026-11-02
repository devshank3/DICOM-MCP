using Dicomr.Core;
using FellowOakDicom;

namespace Dicomr.Dicom;

public static class EnhancedFieldReader
{
    public sealed record Result(
        IReadOnlyList<FieldValue> Fields,
        int FrameCount,
        IReadOnlyList<GeometrySample> Geometry,
        IReadOnlyList<StudyWarning> Warnings);

    public static Result Read(
        DicomDataset dataset,
        IReadOnlyDictionary<string, FieldValue> root,
        string? path,
        bool sopClaimsEnhanced)
    {
        var warnings = new List<StudyWarning>();
        var frames = FrameItems(dataset).ToList();
        var shared = SharedItem(dataset);
        if (sopClaimsEnhanced && frames.Count == 0)
        {
            warnings.Add(new StudyWarning
            {
                Code = "enhanced.macro-missing",
                Path = path,
                Message = "Enhanced MR SOP class has no Per-Frame Functional Groups Sequence.",
            });
        }

        var fields = new List<FieldValue>();
        AddEchoTimes(fields, root, frames, shared);
        AddTiming(fields, root, frames, shared);
        AddDiffusion(fields, root, frames, shared);
        AddInversion(fields, root, frames, shared);
        AddPixelMeasures(fields, root, frames, shared);
        AddOrientation(fields, root, frames, shared);
        AddFrameType(fields, root, frames, shared);
        AddCoils(fields, root, frames, shared);

        var geometry = new List<GeometrySample>();
        foreach (var frame in frames)
        {
            var position = MacroItem(frame, MacroTags.PlanePosition) ?? MacroItem(shared, MacroTags.PlanePosition);
            if (position is not null && TryNumbers(position, DicomTag.ImagePositionPatient, out var coords) && coords.Count == 3)
            {
                geometry.Add(new GeometrySample(FieldKeys.ImagePositionPatient, coords.ToArray()));
            }
        }

        var frameCount = frames.Count;
        if (frameCount == 0 && root.TryGetValue(FieldKeys.NumberOfFrames, out var declared) && declared.AsNumber is double count && count > 0)
        {
            frameCount = (int)count;
        }

        if (frameCount == 0)
        {
            frameCount = 1;
        }

        return new Result(fields, frameCount, geometry, warnings);
    }

    private static void AddEchoTimes(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        List<DicomDataset> frames,
        DicomDataset? shared)
    {
        var echoes = new List<double>();
        foreach (var frame in frames)
        {
            var item = MacroItem(frame, MacroTags.Echo) ?? MacroItem(shared, MacroTags.Echo);
            if (item is not null && TrySingle(item, MacroTags.EffectiveEchoTime, out var echo))
            {
                echoes.Add(echo);
            }
        }

        if (frames.Count == 0)
        {
            var sharedEcho = MacroItem(shared, MacroTags.Echo);
            if (sharedEcho is not null && TrySingle(sharedEcho, MacroTags.EffectiveEchoTime, out var echo))
            {
                echoes.Add(echo);
            }
        }

        var distinct = Distinct(echoes);
        if (distinct.Count > 1)
        {
            fields.Add(MacroField(
                FieldKeys.EchoTime,
                new FieldScalar.NumberList(distinct),
                "MREchoSequence",
                varying: true,
                tag: StandardFieldReader.FormatTag(MacroTags.EffectiveEchoTime)));
            return;
        }

        if (distinct.Count == 1 && !root.ContainsKey(FieldKeys.EchoTime))
        {
            fields.Add(MacroField(
                FieldKeys.EchoTime,
                new FieldScalar.Number(distinct[0]),
                "MREchoSequence",
                tag: StandardFieldReader.FormatTag(MacroTags.EffectiveEchoTime)));
        }
    }

    private static void AddTiming(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        List<DicomDataset> frames,
        DicomDataset? shared)
    {
        var timing = FirstMacro(frames, shared, MacroTags.Timing);
        if (timing is null)
        {
            return;
        }

        if (!root.ContainsKey(FieldKeys.RepetitionTime) && TrySingle(timing, DicomTag.RepetitionTime, out var tr))
        {
            fields.Add(MacroField(
                FieldKeys.RepetitionTime,
                new FieldScalar.Number(tr),
                "MRTimingAndRelatedParameters",
                tag: StandardFieldReader.FormatTag(DicomTag.RepetitionTime)));
        }

        if (!root.ContainsKey(FieldKeys.FlipAngle)
            && TrySingle(timing, DicomTag.FlipAngle, out var flip)
            && flip != 0)
        {
            fields.Add(MacroField(FieldKeys.FlipAngle, new FieldScalar.Number(flip), "MRTimingAndRelatedParameters"));
        }

        if (!root.ContainsKey(FieldKeys.EchoTrainLength)
            && TryInteger(timing, DicomTag.EchoTrainLength, out var etl)
            && etl != 0)
        {
            fields.Add(MacroField(FieldKeys.EchoTrainLength, new FieldScalar.Integer(etl), "MRTimingAndRelatedParameters"));
        }
    }

    private static void AddDiffusion(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        List<DicomDataset> frames,
        DicomDataset? shared)
    {
        var bValues = new List<double>();
        foreach (var frame in frames.DefaultIfEmpty(shared ?? new DicomDataset()))
        {
            var item = MacroItem(frame, MacroTags.Diffusion) ?? MacroItem(shared, MacroTags.Diffusion);
            if (item is not null && TrySingle(item, DicomTag.DiffusionBValue, out var bValue))
            {
                bValues.Add(bValue);
            }
        }

        var distinct = Distinct(bValues);
        if (distinct.Count > 1)
        {
            fields.Add(MacroField(FieldKeys.DiffusionBValue, new FieldScalar.NumberList(distinct), "MRDiffusionSequence", varying: true));
        }
        else if (distinct.Count == 1 && !root.ContainsKey(FieldKeys.DiffusionBValue))
        {
            fields.Add(MacroField(FieldKeys.DiffusionBValue, new FieldScalar.Number(distinct[0]), "MRDiffusionSequence"));
        }

        if (!root.ContainsKey(FieldKeys.DiffusionDirectionality))
        {
            var diffusion = FirstMacro(frames, shared, MacroTags.Diffusion);
            if (diffusion is not null && TryText(diffusion, DicomTag.DiffusionDirectionality, out var directionality))
            {
                fields.Add(MacroField(FieldKeys.DiffusionDirectionality, new FieldScalar.Text(directionality), "MRDiffusionSequence"));
            }
        }

        if (!root.ContainsKey(FieldKeys.DiffusionGradientOrientation))
        {
            var diffusion = FirstMacro(frames, shared, MacroTags.Diffusion);
            var direction = diffusion is null ? null : FirstItem(diffusion, MacroTags.DiffusionGradientDirection);
            if (direction is not null && TryNumbers(direction, DicomTag.DiffusionGradientOrientation, out var orientation) && orientation.Count > 0)
            {
                fields.Add(MacroField(
                    FieldKeys.DiffusionGradientOrientation,
                    new FieldScalar.NumberList(orientation),
                    "MRDiffusionSequence"));
            }
        }
    }

    private static void AddInversion(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        List<DicomDataset> frames,
        DicomDataset? shared)
    {
        var times = new List<double>();
        var sources = frames.Count == 0 ? new List<DicomDataset>() : frames;
        if (sources.Count == 0 && shared is not null)
        {
            sources = [shared];
        }

        foreach (var frame in sources)
        {
            var item = MacroItem(frame, MacroTags.Modifier) ?? MacroItem(shared, MacroTags.Modifier);
            if (item is not null && TryNumbers(item, MacroTags.InversionTimes, out var values))
            {
                times.AddRange(values.Where(value => value != 0));
            }
        }

        var distinct = Distinct(times);
        if (distinct.Count > 1)
        {
            fields.Add(MacroField(FieldKeys.InversionTime, new FieldScalar.NumberList(distinct), "MRModifierSequence", varying: true));
        }
        else if (distinct.Count == 1 && !root.ContainsKey(FieldKeys.InversionTime))
        {
            fields.Add(MacroField(FieldKeys.InversionTime, new FieldScalar.Number(distinct[0]), "MRModifierSequence"));
        }
    }

    private static void AddPixelMeasures(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        List<DicomDataset> frames,
        DicomDataset? shared)
    {
        var measures = FirstMacro(frames, shared, MacroTags.PixelMeasures);
        if (measures is null)
        {
            return;
        }

        FillNumber(fields, root, measures, FieldKeys.SliceThickness, DicomTag.SliceThickness, "PixelMeasuresSequence", omitZero: true);
        FillNumberList(fields, root, measures, FieldKeys.PixelSpacing, DicomTag.PixelSpacing, "PixelMeasuresSequence");
        FillNumber(fields, root, measures, FieldKeys.SpacingBetweenSlices, DicomTag.SpacingBetweenSlices, "PixelMeasuresSequence", omitZero: false);
    }

    private static void AddOrientation(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        List<DicomDataset> frames,
        DicomDataset? shared)
    {
        if (root.ContainsKey(FieldKeys.ImageOrientationPatient))
        {
            return;
        }

        var orientation = FirstMacro(frames, shared, MacroTags.PlaneOrientation);
        if (orientation is null || !TryNumbers(orientation, DicomTag.ImageOrientationPatient, out var cosines) || cosines.Count != 6)
        {
            return;
        }

        fields.Add(MacroField(FieldKeys.ImageOrientationPatient, new FieldScalar.NumberList(cosines), "PlaneOrientationSequence"));
    }

    private static void AddFrameType(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        List<DicomDataset> frames,
        DicomDataset? shared)
    {
        if (root.ContainsKey(FieldKeys.ImageType))
        {
            return;
        }

        var types = new List<string[]>();
        foreach (var frame in frames.DefaultIfEmpty(shared ?? new DicomDataset()))
        {
            var content = MacroItem(frame, MacroTags.FrameContent) ?? MacroItem(shared, MacroTags.FrameContent);
            if (content is not null && TryTexts(content, DicomTag.FrameType, out var texts) && texts.Count > 0)
            {
                types.Add(texts.ToArray());
            }
        }

        var distinct = types
            .GroupBy(text => string.Join('\\', text), StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (distinct.Count == 1)
        {
            fields.Add(MacroField(FieldKeys.ImageType, new FieldScalar.TextList(distinct[0]), "FrameContentSequence"));
        }
        else if (distinct.Count > 1)
        {
            fields.Add(MacroField(
                FieldKeys.ImageType,
                new FieldScalar.TextList(distinct.Select(text => string.Join('\\', text)).ToList()),
                "FrameContentSequence",
                varying: true));
        }
    }

    private static void AddCoils(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        List<DicomDataset> frames,
        DicomDataset? shared)
    {
        var receive = FirstMacro(frames, shared, MacroTags.ReceiveCoil);
        if (receive is not null)
        {
            if (!root.ContainsKey(FieldKeys.ReceiveCoilName) && TryText(receive, DicomTag.ReceiveCoilName, out var name))
            {
                fields.Add(MacroField(FieldKeys.ReceiveCoilName, new FieldScalar.Text(name), "MRReceiveCoilSequence"));
            }

            if (receive.TryGetSequence(MacroTags.MultiCoilDefinition, out var elements))
            {
                var count = elements.Items.Count;
                if (count > 0)
                {
                    fields.Add(MacroField(FieldKeys.MultiCoilElementCount, new FieldScalar.Integer(count), "MRReceiveCoilSequence"));
                    var coilType = count > 1 ? "Combined" : "Uncombined";
                    fields.Add(MacroField(FieldKeys.CoilType, new FieldScalar.Text(coilType), "MRReceiveCoilSequence"));
                }
            }
        }

        var transmit = FirstMacro(frames, shared, MacroTags.TransmitCoil);
        if (transmit is not null
            && !root.ContainsKey(FieldKeys.TransmitCoilName)
            && TryText(transmit, DicomTag.TransmitCoilName, out var transmitName))
        {
            fields.Add(MacroField(FieldKeys.TransmitCoilName, new FieldScalar.Text(transmitName), "MRTransmitCoilSequence"));
        }
    }

    private static void FillNumber(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        DicomDataset item,
        string keyword,
        DicomTag tag,
        string detail,
        bool omitZero)
    {
        if (root.ContainsKey(keyword) || !TrySingle(item, tag, out var number))
        {
            return;
        }

        if (omitZero && number == 0)
        {
            return;
        }

        fields.Add(MacroField(keyword, new FieldScalar.Number(number), detail));
    }

    private static void FillNumberList(
        List<FieldValue> fields,
        IReadOnlyDictionary<string, FieldValue> root,
        DicomDataset item,
        string keyword,
        DicomTag tag,
        string detail)
    {
        if (root.ContainsKey(keyword) || !TryNumbers(item, tag, out var numbers) || numbers.Count == 0)
        {
            return;
        }

        fields.Add(MacroField(keyword, new FieldScalar.NumberList(numbers), detail));
    }

    private static FieldValue MacroField(
        string keyword,
        FieldScalar value,
        string detail,
        bool varying = false,
        string? tag = null) =>
        FieldRegistry.Create(keyword, value, FieldSource.EnhancedMacro, detail, varying, tag);

    private static DicomDataset? FirstMacro(List<DicomDataset> frames, DicomDataset? shared, DicomTag sequenceTag)
    {
        foreach (var frame in frames)
        {
            var item = MacroItem(frame, sequenceTag);
            if (item is not null)
            {
                return item;
            }
        }

        return MacroItem(shared, sequenceTag);
    }

    private static IEnumerable<DicomDataset> FrameItems(DicomDataset dataset)
    {
        if (!dataset.TryGetSequence(DicomTag.PerFrameFunctionalGroupsSequence, out var sequence))
        {
            yield break;
        }

        foreach (var item in sequence.Items)
        {
            yield return item;
        }
    }

    private static DicomDataset? SharedItem(DicomDataset dataset)
    {
        if (!dataset.TryGetSequence(DicomTag.SharedFunctionalGroupsSequence, out var sequence) || sequence.Items.Count == 0)
        {
            return null;
        }

        return sequence.Items[0];
    }

    private static DicomDataset? MacroItem(DicomDataset? parent, DicomTag sequenceTag) =>
        parent is null ? null : FirstItem(parent, sequenceTag);

    private static DicomDataset? FirstItem(DicomDataset parent, DicomTag sequenceTag)
    {
        if (!parent.TryGetSequence(sequenceTag, out var sequence) || sequence.Items.Count == 0)
        {
            return null;
        }

        return sequence.Items[0];
    }

    private static bool TrySingle(DicomDataset dataset, DicomTag tag, out double value)
    {
        value = 0;
        if (dataset.TryGetSingleValue(tag, out double number))
        {
            value = number;
            return true;
        }

        if (dataset.TryGetSingleValue(tag, out float floatNumber))
        {
            value = floatNumber;
            return true;
        }

        if (dataset.TryGetSingleValue(tag, out int integer))
        {
            value = integer;
            return true;
        }

        return false;
    }

    private static bool TryInteger(DicomDataset dataset, DicomTag tag, out long value)
    {
        value = 0;
        if (dataset.TryGetSingleValue(tag, out int integer))
        {
            value = integer;
            return true;
        }

        if (dataset.TryGetSingleValue(tag, out long longValue))
        {
            value = longValue;
            return true;
        }

        return false;
    }

    private static bool TryNumbers(DicomDataset dataset, DicomTag tag, out List<double> values)
    {
        if (dataset.TryGetValues(tag, out double[] doubles) && doubles.Length > 0)
        {
            values = doubles.ToList();
            return true;
        }

        if (dataset.TryGetValues(tag, out float[] floats) && floats.Length > 0)
        {
            values = floats.Select(value => (double)value).ToList();
            return true;
        }

        values = [];
        return false;
    }

    private static bool TryText(DicomDataset dataset, DicomTag tag, out string value)
    {
        value = "";
        if (!dataset.TryGetSingleValue(tag, out string text) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text.Trim();
        return true;
    }

    private static bool TryTexts(DicomDataset dataset, DicomTag tag, out List<string> values)
    {
        if (dataset.TryGetValues(tag, out string[] texts))
        {
            values = texts.Select(text => text.Trim()).Where(text => text.Length > 0).ToList();
            return values.Count > 0;
        }

        values = [];
        return false;
    }

    private static List<double> Distinct(IEnumerable<double> values)
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
}
