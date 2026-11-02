namespace Dicomr.Core;

/// <summary>
/// Drops numeric zeros that vendors write for "not applicable" on classic objects.
/// Enhanced repetition time of zero is a legal single-excitation sentinel and is not passed here.
/// </summary>
public static class NonZeroPolicy
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        FieldKeys.EchoTime,
        FieldKeys.FlipAngle,
        FieldKeys.SliceThickness,
        FieldKeys.RepetitionTime,
        FieldKeys.InversionTime,
        FieldKeys.NumberOfAverages,
        FieldKeys.ImagingFrequency,
        FieldKeys.MagneticFieldStrength,
        FieldKeys.NumberOfPhaseEncodingSteps,
        FieldKeys.EchoTrainLength,
        FieldKeys.PercentSampling,
        FieldKeys.PercentPhaseFieldOfView,
        FieldKeys.PixelBandwidth,
    };

    public static bool Omit(string keyword, double value) =>
        Keywords.Contains(keyword) && value == 0;

    public static bool Omit(FieldValue field)
    {
        if (!Keywords.Contains(field.Keyword))
        {
            return false;
        }

        return field.Value switch
        {
            FieldScalar.Number number => number.NumberValue == 0,
            FieldScalar.Integer integer => integer.IntegerValue == 0,
            FieldScalar.NumberList list => list.Items.Count > 0 && list.Items.All(value => value == 0),
            _ => false,
        };
    }
}
