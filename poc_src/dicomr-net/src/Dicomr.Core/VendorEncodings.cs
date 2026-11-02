namespace Dicomr.Core;

/// <summary>
/// Vendor-specific code tables. Importers in later phases call these.
/// A mapped value of null means the raw code carries no protocol information and the field is dropped.
/// </summary>
public static class VendorEncodings
{
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> For(string keyword) =>
        All.TryGetValue(keyword, out var tables)
            ? tables
            : Empty;

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> Empty =
        new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>> All =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>>>(StringComparer.Ordinal)
        {
            [FieldKeys.InPlanePhaseEncodingDirection] = One(
                "siemens.pro.lPhaseEncodingType",
                new Dictionary<string, string?> { ["1"] = "ROW", ["2"] = "COL" }),
            [FieldKeys.MRAcquisitionType] = new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.Ordinal)
            {
                ["philips.EX_ACQ_scan_mode"] = Map(("0", "2D"), ("1", "3D"), ("2", "2D"), ("3", "2D")),
                ["ge.IMODE"] = Map(("2D", "2D"), ("3D", "3D"), ("3DE", "3D")),
            },
            [FieldKeys.ParallelAcquisitionTechnique] = One(
                "siemens.pro.ucPATMode",
                new Dictionary<string, string?>
                {
                    ["1"] = null,
                    ["2"] = "GRAPPA",
                    ["4"] = "mSENSE",
                    ["32"] = null,
                }),
        };

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string?>> One(
        string source, Dictionary<string, string?> map) =>
        new Dictionary<string, IReadOnlyDictionary<string, string?>>(StringComparer.Ordinal)
        {
            [source] = map,
        };

    private static IReadOnlyDictionary<string, string?> Map(params (string Raw, string Canonical)[] pairs)
    {
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (raw, canonical) in pairs)
        {
            map[raw] = canonical;
        }

        return map;
    }
}
