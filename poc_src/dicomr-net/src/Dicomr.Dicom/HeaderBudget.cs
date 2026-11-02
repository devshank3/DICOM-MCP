namespace Dicomr.Dicom;

public static class HeaderBudget
{
    /// <summary>
    /// Elements larger than this are skipped. Pixel data sits above it.
    /// Siemens CSA series headers sit below it and above fo-dicom's 64 KB default.
    /// </summary>
    public const int Bytes = 4 * 1024 * 1024;
}
