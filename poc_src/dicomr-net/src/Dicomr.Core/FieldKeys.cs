namespace Dicomr.Core;

/// <summary>
/// Canonical MRI protocol field keywords (aligned with DICOM element names).
/// </summary>
public static class FieldKeys
{
    public const string ScanningSequence = "ScanningSequence";
    public const string SequenceVariant = "SequenceVariant";
    public const string ScanOptions = "ScanOptions";
    public const string MRAcquisitionType = "MRAcquisitionType";
    public const string SequenceName = "SequenceName";
    public const string AngioFlag = "AngioFlag";
    public const string SliceThickness = "SliceThickness";
    public const string RepetitionTime = "RepetitionTime";
    public const string EchoTime = "EchoTime";
    public const string InversionTime = "InversionTime";
    public const string NumberOfAverages = "NumberOfAverages";
    public const string ImagingFrequency = "ImagingFrequency";
    public const string ImagedNucleus = "ImagedNucleus";
    public const string EchoNumbers = "EchoNumbers";
    public const string MagneticFieldStrength = "MagneticFieldStrength";
    public const string SpacingBetweenSlices = "SpacingBetweenSlices";
    public const string NumberOfPhaseEncodingSteps = "NumberOfPhaseEncodingSteps";
    public const string EchoTrainLength = "EchoTrainLength";
    public const string PercentSampling = "PercentSampling";
    public const string PercentPhaseFieldOfView = "PercentPhaseFieldOfView";
    public const string PixelBandwidth = "PixelBandwidth";
    public const string ProtocolName = "ProtocolName";
    public const string TriggerTime = "TriggerTime";
    public const string ReconstructionDiameter = "ReconstructionDiameter";
    public const string ReceiveCoilName = "ReceiveCoilName";
    public const string TransmitCoilName = "TransmitCoilName";
    public const string AcquisitionMatrix = "AcquisitionMatrix";
    public const string InPlanePhaseEncodingDirection = "InPlanePhaseEncodingDirection";
    public const string FlipAngle = "FlipAngle";
    public const string PatientPosition = "PatientPosition";
    public const string Rows = "Rows";
    public const string Columns = "Columns";
    public const string PixelSpacing = "PixelSpacing";
    public const string ImageType = "ImageType";
    public const string BodyPartExamined = "BodyPartExamined";
    public const string NumberOfFrames = "NumberOfFrames";
    public const string PulseSequenceName = "PulseSequenceName";
    public const string EchoPulseSequence = "EchoPulseSequence";
    public const string MultipleSpinEcho = "MultipleSpinEcho";
    public const string MultiPlanarExcitation = "MultiPlanarExcitation";
    public const string PhaseContrast = "PhaseContrast";
    public const string TimeOfFlightContrast = "TimeOfFlightContrast";
    public const string SteadyStatePulseSequence = "SteadyStatePulseSequence";
    public const string EchoPlanarPulseSequence = "EchoPlanarPulseSequence";
    public const string SaturationRecovery = "SaturationRecovery";
    public const string SpectrallySelectedSuppression = "SpectrallySelectedSuppression";
    public const string ParallelAcquisition = "ParallelAcquisition";
    public const string ParallelAcquisitionTechnique = "ParallelAcquisitionTechnique";
    public const string PartialFourier = "PartialFourier";
    public const string PartialFourierDirection = "PartialFourierDirection";
    public const string ParallelReductionFactorInPlane = "ParallelReductionFactorInPlane";
    public const string ParallelReductionFactorOutOfPlane = "ParallelReductionFactorOutOfPlane";
    public const string AcquisitionDuration = "AcquisitionDuration";
    public const string DiffusionBValue = "DiffusionBValue";
    public const string DiffusionDirectionality = "DiffusionDirectionality";
    public const string DiffusionGradientOrientation = "DiffusionGradientOrientation";
    public const string ArterialSpinLabelingContrast = "ArterialSpinLabelingContrast";
    public const string B1rms = "B1rms";
    public const string SAR = "SAR";
    public const string DBDt = "dBdt";
    public const string ImageOrientationPatient = "ImageOrientationPatient";
    public const string AcquisitionPlane = "AcquisitionPlane";
    public const string ImagePositionPatient = "ImagePositionPatient";
    public const string SliceLocation = "SliceLocation";
    public const string InstanceNumber = "InstanceNumber";
    public const string CoilType = "CoilType";
    public const string MultiCoilElementCount = "MultiCoilElementCount";

    /// <summary>
    /// Scalars that become a distinct list when they change inside one series.
    /// </summary>
    public static readonly IReadOnlySet<string> VaryingScalars = new HashSet<string>(StringComparer.Ordinal)
    {
        EchoTime,
        InversionTime,
        DiffusionBValue,
        EchoNumbers,
    };

    /// <summary>
    /// Multi-valued fields whose whole sequence is one value, and that may differ inside a series.
    /// </summary>
    public static readonly IReadOnlySet<string> VaryingTuples = new HashSet<string>(StringComparer.Ordinal)
    {
        ImageType,
    };

    /// <summary>
    /// Geometry that is recorded as a distinct count and left off the protocol card.
    /// </summary>
    public static readonly IReadOnlySet<string> CountOnly = new HashSet<string>(StringComparer.Ordinal)
    {
        ImagePositionPatient,
        SliceLocation,
        InstanceNumber,
    };

    public static readonly IReadOnlyList<string> CardOrder =
    [
        RepetitionTime,
        EchoTime,
        InversionTime,
        FlipAngle,
        MRAcquisitionType,
        ScanningSequence,
        SequenceVariant,
        ScanOptions,
        EchoPulseSequence,
        EchoPlanarPulseSequence,
        SteadyStatePulseSequence,
        SliceThickness,
        SpacingBetweenSlices,
        PixelSpacing,
        Rows,
        Columns,
        AcquisitionMatrix,
        ReconstructionDiameter,
        NumberOfPhaseEncodingSteps,
        EchoTrainLength,
        EchoNumbers,
        NumberOfAverages,
        PixelBandwidth,
        PercentSampling,
        PercentPhaseFieldOfView,
        InPlanePhaseEncodingDirection,
        AcquisitionPlane,
        ImageOrientationPatient,
        ImageType,
        MagneticFieldStrength,
        ImagingFrequency,
        ImagedNucleus,
        ReceiveCoilName,
        TransmitCoilName,
        CoilType,
        MultiCoilElementCount,
        ParallelAcquisition,
        ParallelAcquisitionTechnique,
        ParallelReductionFactorInPlane,
        ParallelReductionFactorOutOfPlane,
        PartialFourier,
        PartialFourierDirection,
        DiffusionBValue,
        DiffusionDirectionality,
        DiffusionGradientOrientation,
        ArterialSpinLabelingContrast,
        TriggerTime,
        NumberOfFrames,
        PatientPosition,
        BodyPartExamined,
        AngioFlag,
        SequenceName,
        PulseSequenceName,
    ];
}
