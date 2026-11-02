namespace Dicomr.Core;

public static class CleanName
{
    /// <summary>
    /// Trim and collapse internal whitespace. Case stays as written.
    /// Trailing Siemens <c>_RR</c> markers are kept until run detection.
    /// </summary>
    public static string Collapse(string value)
    {
        var parts = value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "" : string.Join(' ', parts);
    }
}
