using Dicomr.Cli;
using FellowOakDicom;

namespace Dicomr.Cli.Tests;

public class ReadCommandTests
{
    [Fact]
    public async Task MissingPath_ExitsWithCode2()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await ReadCommand.ExecuteAsync(
            new ReadCommandOptions { Path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) },
            stdout,
            stderr,
            log: null,
            CancellationToken.None);

        Assert.Equal(2, code);
        Assert.Contains("does not exist", stderr.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Folder_WritesProtocolJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "Dicomr-cli", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dicom = Path.Combine(root, "image.dcm");
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, "1.2.840.10008.5.1.4.1.1.4" },
            { DicomTag.SOPInstanceUID, DicomUID.Generate().UID },
            { DicomTag.StudyInstanceUID, "1.2.40" },
            { DicomTag.SeriesInstanceUID, "1.2.40.1" },
            { DicomTag.Modality, "MR" },
            { DicomTag.ProtocolName, "T1w" },
            { DicomTag.RepetitionTime, 2300.0 },
            { DicomTag.StudyDescription, "QA phantom" },
            { DicomTag.Manufacturer, "SIEMENS" },
            { DicomTag.ManufacturerModelName, "Prisma" },
            { DicomTag.MagneticFieldStrength, 3.0 },
        };
        await new DicomFile(dataset).SaveAsync(dicom);

        var output = Path.Combine(root, "study.protocol.json");
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await ReadCommand.ExecuteAsync(
            new ReadCommandOptions { Path = root, Output = output },
            stdout,
            stderr,
            log: null,
            CancellationToken.None);

        Assert.Equal(0, code);
        Assert.StartsWith(ProtocolSummary.PatientNotice, stdout.ToString(), StringComparison.Ordinal);
        var json = await File.ReadAllTextAsync(output);
        Assert.Contains("RepetitionTime", json, StringComparison.Ordinal);
        Assert.Contains("2300", json, StringComparison.Ordinal);
        Assert.Contains("T1w", stdout.ToString(), StringComparison.Ordinal);
    }
}
