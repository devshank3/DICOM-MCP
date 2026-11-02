namespace Dicomr.Core;

public sealed record FieldDef(
    string Keyword,
    string? Tag,
    string? Vr,
    string? Unit,
    bool Continuous,
    double? SuggestedTolerance,
    IReadOnlySet<string>? Vocabulary,
    bool CompareByDefault);

public static class FieldRegistry
{
    private static readonly IReadOnlyDictionary<string, FieldDef> Definitions = Build();

    public static IReadOnlyCollection<FieldDef> All => Definitions.Values.ToArray();

    public static FieldDef? Find(string keyword) =>
        Definitions.TryGetValue(keyword, out var def) ? def : null;

    public static FieldValue Create(
        string keyword,
        FieldScalar value,
        FieldSource source = FieldSource.Standard,
        string? sourceDetail = null,
        bool varying = false,
        string? tag = null,
        string? vr = null)
    {
        var def = Find(keyword);
        return new FieldValue
        {
            Keyword = keyword,
            Tag = tag ?? def?.Tag,
            Vr = vr ?? def?.Vr,
            Unit = def?.Unit,
            Source = source,
            SourceDetail = sourceDetail,
            Value = value,
            Varying = varying,
        };
    }

    private static Dictionary<string, FieldDef> Build()
    {
        var defs = new List<FieldDef>
        {
            Enumerated(FieldKeys.InPlanePhaseEncodingDirection, "0018,1312", "CS", ["ROW", "COL"], compare: true),
            Enumerated(FieldKeys.MRAcquisitionType, "0018,0023", "CS", ["2D", "3D"], compare: true),
            Enumerated(FieldKeys.PatientPosition, "0018,5100", "CS",
                ["HFS", "HFP", "FFS", "FFP", "HFDR", "HFDL", "FFDR", "FFDL", "LFP", "LFS", "RFP", "RFS"],
                compare: true),
            Enumerated(FieldKeys.ImagedNucleus, "0018,0085", "SH",
                ["1H", "31P", "13C", "23NA", "19F", "129XE", "2H", "7LI"], compare: true),
            Enumerated(FieldKeys.ParallelAcquisitionTechnique, "0018,9078", "CS",
                ["GRAPPA", "SENSE", "mSENSE", "SMASH", "PILS", "ASSET", "ARC"], compare: true),
            Enumerated(FieldKeys.AcquisitionPlane, null, "CS", ["axial", "sagittal", "coronal"], compare: true),

            Continuous(FieldKeys.RepetitionTime, "0018,0080", "DS", "ms", 10, compare: true),
            Continuous(FieldKeys.EchoTime, "0018,0081", "DS", "ms", 1, compare: true),
            Continuous(FieldKeys.InversionTime, "0018,0082", "DS", "ms", 10, compare: true),
            Continuous(FieldKeys.SliceThickness, "0018,0050", "DS", "mm", 0.1, compare: true),
            Continuous(FieldKeys.SpacingBetweenSlices, "0018,0088", "DS", "mm", 0.1, compare: true),
            Continuous(FieldKeys.PixelBandwidth, "0018,0095", "DS", "Hz/pixel", 50, compare: true),
            Continuous(FieldKeys.FlipAngle, "0018,1314", "DS", "deg", 2, compare: true),
            Continuous(FieldKeys.ImagingFrequency, "0018,0084", "DS", "MHz", 1, compare: true),
            Continuous(FieldKeys.MagneticFieldStrength, "0018,0087", "DS", "T", 0.3, compare: true),
            Continuous(FieldKeys.PercentPhaseFieldOfView, "0018,0094", "DS", "%", 10, compare: true),
            Continuous(FieldKeys.PercentSampling, "0018,0093", "DS", "%", 10, compare: true),
            Continuous(FieldKeys.PixelSpacing, "0028,0030", "DS", "mm", 0.1, compare: true),
            Continuous(FieldKeys.DiffusionBValue, "0018,9087", "FD", "s/mm2", 0, compare: true),
            Continuous(FieldKeys.ParallelReductionFactorInPlane, "0018,9069", "FD", null, 0, compare: true),
            Continuous(FieldKeys.ParallelReductionFactorOutOfPlane, "0018,9155", "FD", null, 0, compare: true),
            Continuous(FieldKeys.SAR, "0018,1316", "DS", "W/kg", null, compare: false),
            Continuous(FieldKeys.DBDt, "0018,1318", "DS", "T/s", null, compare: false),
            Continuous(FieldKeys.B1rms, "0018,1320", "FL", "uT", null, compare: false),
            Continuous(FieldKeys.TriggerTime, "0018,1060", "DS", "ms", 0, compare: true),
            Continuous(FieldKeys.ReconstructionDiameter, "0018,1100", "DS", "mm", 0, compare: true),
            Continuous(FieldKeys.NumberOfAverages, "0018,0083", "DS", null, 0, compare: true),
            Continuous(FieldKeys.AcquisitionDuration, "0018,9073", "FD", "s", null, compare: false),

            Text(FieldKeys.ScanningSequence, "0018,0020", "CS", compare: true),
            Text(FieldKeys.SequenceVariant, "0018,0021", "CS", compare: true),
            Text(FieldKeys.ScanOptions, "0018,0022", "CS", compare: true),
            Text(FieldKeys.SequenceName, "0018,0024", "SH", compare: false),
            Text(FieldKeys.AngioFlag, "0018,0025", "CS", compare: true),
            Text(FieldKeys.ProtocolName, "0018,1030", "LO", compare: false),
            Text(FieldKeys.ReceiveCoilName, "0018,1250", "SH", compare: true),
            Text(FieldKeys.TransmitCoilName, "0018,1251", "SH", compare: true),
            Text(FieldKeys.ImageType, "0008,0008", "CS", compare: true),
            Text(FieldKeys.BodyPartExamined, "0018,0015", "CS", compare: true),
            Text(FieldKeys.PulseSequenceName, "0018,9005", "SH", compare: false),
            Text(FieldKeys.EchoPulseSequence, "0018,9008", "CS", compare: true),
            Text(FieldKeys.MultipleSpinEcho, "0018,9011", "CS", compare: true),
            Text(FieldKeys.MultiPlanarExcitation, "0018,9012", "CS", compare: true),
            Text(FieldKeys.PhaseContrast, "0018,9014", "CS", compare: true),
            Text(FieldKeys.TimeOfFlightContrast, "0018,9015", "CS", compare: true),
            Text(FieldKeys.SteadyStatePulseSequence, "0018,9017", "CS", compare: true),
            Text(FieldKeys.EchoPlanarPulseSequence, "0018,9018", "CS", compare: true),
            Text(FieldKeys.SaturationRecovery, "0018,9024", "CS", compare: true),
            Text(FieldKeys.SpectrallySelectedSuppression, "0018,9025", "CS", compare: true),
            Text(FieldKeys.ParallelAcquisition, "0018,9077", "CS", compare: true),
            Text(FieldKeys.PartialFourier, "0018,9081", "CS", compare: true),
            Text(FieldKeys.PartialFourierDirection, "0018,9036", "CS", compare: true),
            Text(FieldKeys.DiffusionDirectionality, "0018,9075", "CS", compare: true),
            Text(FieldKeys.ArterialSpinLabelingContrast, "0018,9250", "CS", compare: true),
            Text(FieldKeys.CoilType, null, "CS", compare: false),
            Text(FieldKeys.ImageOrientationPatient, "0020,0037", "DS", compare: false),

            Integer(FieldKeys.EchoNumbers, "0018,0086", "IS", compare: true),
            Integer(FieldKeys.NumberOfPhaseEncodingSteps, "0018,0089", "IS", compare: true),
            Integer(FieldKeys.EchoTrainLength, "0018,0091", "IS", compare: true),
            Integer(FieldKeys.Rows, "0028,0010", "US", compare: true),
            Integer(FieldKeys.Columns, "0028,0011", "US", compare: true),
            Integer(FieldKeys.NumberOfFrames, "0028,0008", "IS", compare: false),
            Integer(FieldKeys.MultiCoilElementCount, null, "IS", compare: false),
            Text(FieldKeys.AcquisitionMatrix, "0018,1310", "US", compare: true),
            Text(FieldKeys.DiffusionGradientOrientation, "0018,9089", "FD", compare: false),
        };

        return defs.ToDictionary(d => d.Keyword, StringComparer.Ordinal);
    }

    private static FieldDef Continuous(
        string keyword, string? tag, string vr, string? unit, double? tolerance, bool compare) =>
        new(keyword, tag, vr, unit, Continuous: true, tolerance, Vocabulary: null, compare);

    private static FieldDef Enumerated(
        string keyword, string? tag, string vr, string[] vocabulary, bool compare) =>
        new(keyword, tag, vr, Unit: null, Continuous: false, SuggestedTolerance: null,
            new HashSet<string>(vocabulary, StringComparer.Ordinal), compare);

    private static FieldDef Text(string keyword, string? tag, string vr, bool compare) =>
        new(keyword, tag, vr, Unit: null, Continuous: false, SuggestedTolerance: null, Vocabulary: null, compare);

    private static FieldDef Integer(string keyword, string? tag, string vr, bool compare) =>
        new(keyword, tag, vr, Unit: null, Continuous: false, SuggestedTolerance: 0, Vocabulary: null, compare);
}
