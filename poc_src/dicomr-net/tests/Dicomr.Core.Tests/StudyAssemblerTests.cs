using Dicomr.Core;

namespace Dicomr.Core.Tests;

public class StudyAssemblerTests
{
    [Fact]
    public void ThreeInstances_BecomeOneSeries()
    {
        var study = StudyAssembler.Assemble(
        [
            Instance("T1", "1.2.3.4", 1, 2300),
            Instance("T1", "1.2.3.4", 2, 2300),
            Instance("T1", "1.2.3.4", 3, 2300),
        ]);

        var series = Assert.Single(Assert.Single(study.Acquisitions).Series);
        Assert.Equal(3, series.InstanceCount);
        Assert.Equal(2300, series.Fields.Single(field => field.Keyword == FieldKeys.RepetitionTime).AsNumber);
    }

    [Fact]
    public void TwoSeriesUids_StayInOneAcquisition()
    {
        var study = StudyAssembler.Assemble(
        [
            Instance("T1", "1.2.3.4", 1, 2300),
            Instance("T1", "1.2.3.5", 1, 2300, seriesDescription: "phase"),
        ]);

        var acquisition = Assert.Single(study.Acquisitions);
        Assert.Equal(2, acquisition.Series.Count);
    }

    [Fact]
    public void TwoProtocolNames_BecomeTwoAcquisitions()
    {
        var study = StudyAssembler.Assemble(
        [
            Instance("T1", "1.2.3.4", 1, 2300, seriesNumber: 2),
            Instance("T2", "1.2.3.5", 1, 5000, seriesNumber: 1),
        ]);

        Assert.Equal(["T2", "T1"], study.Acquisitions.Select(item => item.Name).ToArray());
    }

    [Fact]
    public void EmptyProtocolName_UsesSequenceName()
    {
        var instance = Instance("", "1.2.3.4", 1, 2300);
        instance = instance with { ProtocolName = null, SequenceName = "tfl" };
        var study = StudyAssembler.Assemble([instance]);
        var acquisition = Assert.Single(study.Acquisitions);
        Assert.Equal("tfl", acquisition.Name);
        Assert.Null(acquisition.ProtocolName);
        Assert.Equal("tfl", acquisition.SequenceName);
    }

    [Fact]
    public void DisagreeingRepetitionTime_KeepsTheFirstAndWarns()
    {
        var study = StudyAssembler.Assemble(
        [
            Instance("T1", "1.2.3.4", 1, 2300),
            Instance("T1", "1.2.3.4", 2, 2400),
        ]);

        var series = Assert.Single(Assert.Single(study.Acquisitions).Series);
        Assert.Equal(2300, series.Fields.Single(field => field.Keyword == FieldKeys.RepetitionTime).AsNumber);
        var warning = Assert.Single(study.Warnings);
        Assert.Equal("series.inconsistent", warning.Code);
    }

    [Fact]
    public void EchoTimes_CollapseToAVaryingList()
    {
        var study = StudyAssembler.Assemble(
        [
            Instance("ME", "1.2.3.4", 1, 2300, echo: 10),
            Instance("ME", "1.2.3.4", 2, 2300, echo: 20),
        ]);

        var echo = Assert.Single(Assert.Single(study.Acquisitions).Series.Single().Fields, field => field.Keyword == FieldKeys.EchoTime);
        Assert.True(echo.Varying);
        Assert.Equal([10, 20], echo.AsNumbers);
    }

    [Fact]
    public void DirectionCosines_NameThePlane()
    {
        Assert.Equal("axial", AcquisitionPlane.FromDirectionCosines([1, 0, 0, 0, 1, 0]));
        Assert.Equal("sagittal", AcquisitionPlane.FromDirectionCosines([0, 1, 0, 0, 0, -1]));
    }

    private static InstanceRecord Instance(
        string protocol,
        string seriesUid,
        int instanceNumber,
        double repetitionTime,
        double? echo = null,
        int seriesNumber = 1,
        string? seriesDescription = "t1") => new()
        {
            Path = $"{seriesUid}-{instanceNumber}.dcm",
            StudyInstanceUid = "1.2.3",
            SeriesInstanceUid = seriesUid,
            SopInstanceUid = $"{seriesUid}.{instanceNumber}",
            SopClassUid = "1.2.840.10008.5.1.4.1.1.4",
            Encoding = "classic",
            Modality = "MR",
            ProtocolName = protocol,
            SeriesDescription = seriesDescription,
            SeriesNumber = seriesNumber,
            InstanceNumber = instanceNumber,
            FrameCount = 1,
            Fields =
            [
                FieldRegistry.Create(FieldKeys.RepetitionTime, new FieldScalar.Number(repetitionTime)),
                FieldRegistry.Create(FieldKeys.EchoTime, new FieldScalar.Number(echo ?? 2.98)),
            ],
        };
}
