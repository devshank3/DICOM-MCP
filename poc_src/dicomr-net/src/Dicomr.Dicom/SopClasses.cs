namespace Dicomr.Dicom;

public static class SopClasses
{
    public const string ClassicMr = "1.2.840.10008.5.1.4.1.1.4";
    public const string EnhancedMr = "1.2.840.10008.5.1.4.1.1.4.1";
    public const string EnhancedMrColor = "1.2.840.10008.5.1.4.1.1.4.3";
    public const string LegacyConvertedEnhancedMr = "1.2.840.10008.5.1.4.1.1.4.4";
    public const string DicomDir = "1.2.840.10008.1.3.10";

    public static bool IsEnhanced(string? sopClassUid) =>
        sopClassUid is EnhancedMr or EnhancedMrColor or LegacyConvertedEnhancedMr;

    public static bool IsClassicMr(string? sopClassUid) =>
        string.Equals(sopClassUid, ClassicMr, StringComparison.Ordinal);

    public static bool IsDicomDir(string? sopClassUid) =>
        string.Equals(sopClassUid, DicomDir, StringComparison.Ordinal);
}
