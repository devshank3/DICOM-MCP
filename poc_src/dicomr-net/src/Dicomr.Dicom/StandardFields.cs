using FellowOakDicom;

namespace Dicomr.Dicom;

public enum ValueKind
{
    Text,
    Number,
    Integer,
    TextList,
    NumberList,
}

public readonly record struct TagBinding(DicomTag Tag, string Keyword, ValueKind Kind);

public static class StandardFields
{
    public static IReadOnlyList<TagBinding> All { get; } =
    [
        Bind(DicomTag.ScanningSequence, Core.FieldKeys.ScanningSequence, ValueKind.TextList),
        Bind(DicomTag.SequenceVariant, Core.FieldKeys.SequenceVariant, ValueKind.TextList),
        Bind(DicomTag.ScanOptions, Core.FieldKeys.ScanOptions, ValueKind.TextList),
        Bind(DicomTag.MRAcquisitionType, Core.FieldKeys.MRAcquisitionType, ValueKind.Text),
        Bind(DicomTag.AngioFlag, Core.FieldKeys.AngioFlag, ValueKind.Text),
        Bind(DicomTag.SliceThickness, Core.FieldKeys.SliceThickness, ValueKind.Number),
        Bind(DicomTag.RepetitionTime, Core.FieldKeys.RepetitionTime, ValueKind.Number),
        Bind(DicomTag.EchoTime, Core.FieldKeys.EchoTime, ValueKind.Number),
        Bind(DicomTag.InversionTime, Core.FieldKeys.InversionTime, ValueKind.Number),
        Bind(DicomTag.NumberOfAverages, Core.FieldKeys.NumberOfAverages, ValueKind.Number),
        Bind(DicomTag.ImagingFrequency, Core.FieldKeys.ImagingFrequency, ValueKind.Number),
        Bind(DicomTag.ImagedNucleus, Core.FieldKeys.ImagedNucleus, ValueKind.Text),
        Bind(DicomTag.EchoNumbers, Core.FieldKeys.EchoNumbers, ValueKind.Integer),
        Bind(DicomTag.MagneticFieldStrength, Core.FieldKeys.MagneticFieldStrength, ValueKind.Number),
        Bind(DicomTag.SpacingBetweenSlices, Core.FieldKeys.SpacingBetweenSlices, ValueKind.Number),
        Bind(DicomTag.NumberOfPhaseEncodingSteps, Core.FieldKeys.NumberOfPhaseEncodingSteps, ValueKind.Integer),
        Bind(DicomTag.EchoTrainLength, Core.FieldKeys.EchoTrainLength, ValueKind.Integer),
        Bind(DicomTag.PercentSampling, Core.FieldKeys.PercentSampling, ValueKind.Number),
        Bind(DicomTag.PercentPhaseFieldOfView, Core.FieldKeys.PercentPhaseFieldOfView, ValueKind.Number),
        Bind(DicomTag.PixelBandwidth, Core.FieldKeys.PixelBandwidth, ValueKind.Number),
        Bind(DicomTag.TriggerTime, Core.FieldKeys.TriggerTime, ValueKind.Number),
        Bind(DicomTag.ReconstructionDiameter, Core.FieldKeys.ReconstructionDiameter, ValueKind.Number),
        Bind(DicomTag.ReceiveCoilName, Core.FieldKeys.ReceiveCoilName, ValueKind.Text),
        Bind(DicomTag.TransmitCoilName, Core.FieldKeys.TransmitCoilName, ValueKind.Text),
        Bind(DicomTag.AcquisitionMatrix, Core.FieldKeys.AcquisitionMatrix, ValueKind.NumberList),
        Bind(DicomTag.InPlanePhaseEncodingDirection, Core.FieldKeys.InPlanePhaseEncodingDirection, ValueKind.Text),
        Bind(DicomTag.FlipAngle, Core.FieldKeys.FlipAngle, ValueKind.Number),
        Bind(DicomTag.PatientPosition, Core.FieldKeys.PatientPosition, ValueKind.Text),
        Bind(DicomTag.Rows, Core.FieldKeys.Rows, ValueKind.Integer),
        Bind(DicomTag.Columns, Core.FieldKeys.Columns, ValueKind.Integer),
        Bind(DicomTag.PixelSpacing, Core.FieldKeys.PixelSpacing, ValueKind.NumberList),
        Bind(DicomTag.ImageType, Core.FieldKeys.ImageType, ValueKind.TextList),
        Bind(DicomTag.BodyPartExamined, Core.FieldKeys.BodyPartExamined, ValueKind.Text),
        Bind(DicomTag.NumberOfFrames, Core.FieldKeys.NumberOfFrames, ValueKind.Integer),
        Bind(DicomTag.PulseSequenceName, Core.FieldKeys.PulseSequenceName, ValueKind.Text),
        Bind(DicomTag.EchoPulseSequence, Core.FieldKeys.EchoPulseSequence, ValueKind.Text),
        Bind(DicomTag.MultipleSpinEcho, Core.FieldKeys.MultipleSpinEcho, ValueKind.Text),
        Bind(DicomTag.MultiPlanarExcitation, Core.FieldKeys.MultiPlanarExcitation, ValueKind.Text),
        Bind(DicomTag.PhaseContrast, Core.FieldKeys.PhaseContrast, ValueKind.Text),
        Bind(DicomTag.TimeOfFlightContrast, Core.FieldKeys.TimeOfFlightContrast, ValueKind.Text),
        Bind(DicomTag.SteadyStatePulseSequence, Core.FieldKeys.SteadyStatePulseSequence, ValueKind.Text),
        Bind(DicomTag.EchoPlanarPulseSequence, Core.FieldKeys.EchoPlanarPulseSequence, ValueKind.Text),
        Bind(DicomTag.SaturationRecovery, Core.FieldKeys.SaturationRecovery, ValueKind.Text),
        Bind(DicomTag.SpectrallySelectedSuppression, Core.FieldKeys.SpectrallySelectedSuppression, ValueKind.Text),
        Bind(DicomTag.ParallelAcquisition, Core.FieldKeys.ParallelAcquisition, ValueKind.Text),
        Bind(DicomTag.ParallelAcquisitionTechnique, Core.FieldKeys.ParallelAcquisitionTechnique, ValueKind.Text),
        Bind(DicomTag.PartialFourier, Core.FieldKeys.PartialFourier, ValueKind.Text),
        Bind(DicomTag.PartialFourierDirection, Core.FieldKeys.PartialFourierDirection, ValueKind.Text),
        Bind(DicomTag.ParallelReductionFactorInPlane, Core.FieldKeys.ParallelReductionFactorInPlane, ValueKind.Number),
        Bind(DicomTag.ParallelReductionFactorOutOfPlane, Core.FieldKeys.ParallelReductionFactorOutOfPlane, ValueKind.Number),
        Bind(DicomTag.AcquisitionDuration, Core.FieldKeys.AcquisitionDuration, ValueKind.Number),
        Bind(DicomTag.DiffusionBValue, Core.FieldKeys.DiffusionBValue, ValueKind.Number),
        Bind(DicomTag.DiffusionDirectionality, Core.FieldKeys.DiffusionDirectionality, ValueKind.Text),
        Bind(DicomTag.ArterialSpinLabelingContrast, Core.FieldKeys.ArterialSpinLabelingContrast, ValueKind.Text),
        Bind(DicomTag.B1rms, Core.FieldKeys.B1rms, ValueKind.Number),
        Bind(DicomTag.SAR, Core.FieldKeys.SAR, ValueKind.Number),
        Bind(DicomTag.dBdt, Core.FieldKeys.DBDt, ValueKind.Number),
        Bind(DicomTag.ImageOrientationPatient, Core.FieldKeys.ImageOrientationPatient, ValueKind.NumberList),
    ];

    private static TagBinding Bind(DicomTag tag, string keyword, ValueKind kind) => new(tag, keyword, kind);
}
