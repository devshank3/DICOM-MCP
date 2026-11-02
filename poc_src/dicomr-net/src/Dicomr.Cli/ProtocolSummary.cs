using System.Globalization;
using Dicomr.Core;

namespace Dicomr.Cli;

public static class ProtocolSummary
{
    public const string PatientNotice =
        "PatientID and PatientName are included when the DICOM files contain them.";

    public static void Write(StudyProtocol protocol, TextWriter writer)
    {
        var study = protocol.Study;
        var identity = string.Join("  ", new[]
        {
            study.StudyInstanceUid,
            study.StudyDescription,
            Join(study.Manufacturer, study.ManufacturerModelName),
            study.MagneticFieldStrengthTesla is double field ? $"{Format(field)} T" : null,
        }.Where(part => !string.IsNullOrWhiteSpace(part)));

        writer.WriteLine($"Study {identity}");
        foreach (var acquisition in protocol.Acquisitions)
        {
            writer.WriteLine($"  {acquisition.Name}");
            foreach (var series in acquisition.Series)
            {
                var count = series.FrameCount > series.InstanceCount
                    ? $"{series.InstanceCount} file, {series.FrameCount} frames"
                    : $"{series.InstanceCount} files";
                var seriesNumber = series.SeriesNumber?.ToString(CultureInfo.InvariantCulture) ?? "-";
                writer.WriteLine($"    series {seriesNumber}  {series.SeriesDescription}  {series.Encoding}  {count}");
                writer.WriteLine($"      {Line("TR", series, FieldKeys.RepetitionTime)}   {Line("TE", series, FieldKeys.EchoTime)}   {Line("TI", series, FieldKeys.InversionTime)}   {Line("FA", series, FieldKeys.FlipAngle)}");
                writer.WriteLine($"      {Text(series, FieldKeys.MRAcquisitionType) ?? "-"}  matrix {Matrix(series)}   voxel {Voxel(series)}");
                writer.WriteLine($"      PE {Text(series, FieldKeys.InPlanePhaseEncodingDirection) ?? "-"}   plane {Text(series, FieldKeys.AcquisitionPlane) ?? "-"}");
            }
        }

        var warningCount = protocol.Warnings.Count;
        writer.WriteLine($"  files {protocol.Stats.FilesRead} read, {protocol.Stats.FilesSkipped} skipped, {warningCount} warning{(warningCount == 1 ? "" : "s")}");
        foreach (var warning in protocol.Warnings.Take(5))
        {
            writer.WriteLine($"  ! {warning.Code}: {warning.Message}");
        }
    }

    private static string Line(string label, SeriesRecord series, string keyword)
    {
        var field = Find(series, keyword);
        if (field is null)
        {
            return $"{label} -";
        }

        var unit = string.IsNullOrEmpty(field.Unit) ? "" : $" {field.Unit}";
        return $"{label} {Value(field)}{unit}";
    }

    private static string Matrix(SeriesRecord series)
    {
        var numbers = Find(series, FieldKeys.AcquisitionMatrix)?.AsNumbers;
        return numbers is null ? "-" : string.Join('\\', numbers.Select(Format));
    }

    private static string Voxel(SeriesRecord series)
    {
        var spacing = Find(series, FieldKeys.PixelSpacing)?.AsNumbers;
        var thickness = Find(series, FieldKeys.SliceThickness)?.AsNumber;
        if (spacing is null || spacing.Count < 2 || thickness is null)
        {
            return "-";
        }

        return $"{Format(spacing[0])} x {Format(spacing[1])} x {Format(thickness.Value)} mm";
    }

    private static string Value(FieldValue field)
    {
        if (field.AsNumber is double number && field.Value is not FieldScalar.NumberList)
        {
            return Format(number);
        }

        if (field.AsNumbers is IReadOnlyList<double> numbers)
        {
            return string.Join('\\', numbers.Select(Format));
        }

        if (field.AsText is string text)
        {
            return text;
        }

        if (field.AsTexts is IReadOnlyList<string> texts)
        {
            return string.Join('\\', texts);
        }

        return "-";
    }

    private static string Format(double value) =>
        Math.Abs(value - Math.Round(value)) < 0.0001
            ? Math.Round(value).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string? Text(SeriesRecord series, string keyword) => Find(series, keyword)?.AsText;

    private static FieldValue? Find(SeriesRecord series, string keyword) =>
        series.Fields.FirstOrDefault(field => field.Keyword == keyword);

    private static string? Join(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left} {right}";
    }
}
