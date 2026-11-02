using Dicomr.Core;
using FellowOakDicom;

namespace Dicomr.Dicom;

public static class MacroTags
{
    public static readonly DicomTag Timing = Require("MRTimingAndRelatedParametersSequence");
    public static readonly DicomTag Echo = Require("MREchoSequence");
    public static readonly DicomTag Diffusion = Require("MRDiffusionSequence");
    public static readonly DicomTag Modifier = Require("MRModifierSequence");
    public static readonly DicomTag PixelMeasures = Require("PixelMeasuresSequence");
    public static readonly DicomTag PlaneOrientation = Require("PlaneOrientationSequence");
    public static readonly DicomTag PlanePosition = Require("PlanePositionSequence");
    public static readonly DicomTag FrameContent = Require("FrameContentSequence");
    public static readonly DicomTag ReceiveCoil = Require("MRReceiveCoilSequence");
    public static readonly DicomTag TransmitCoil = Require("MRTransmitCoilSequence");
    public static readonly DicomTag MultiCoilDefinition = Require("MultiCoilDefinitionSequence");
    public static readonly DicomTag EffectiveEchoTime = Require("EffectiveEchoTime");
    public static readonly DicomTag InversionTimes = Require("InversionTimes");
    public static readonly DicomTag DiffusionGradientDirection = Require("DiffusionGradientDirectionSequence");

    private static DicomTag Require(string keyword)
    {
        DicomDictionary.EnsureDefaultDictionariesLoaded();
        var tag = DicomDictionary.Default[keyword];
        return tag ?? throw new InvalidOperationException($"DICOM dictionary 2026b is missing {keyword}.");
    }
}
