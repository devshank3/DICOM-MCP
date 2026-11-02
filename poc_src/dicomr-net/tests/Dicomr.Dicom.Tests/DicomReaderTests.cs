using System.Diagnostics;
using Dicomr.Core;
using Dicomr.Dicom;
using FellowOakDicom;
using FellowOakDicom.IO;

namespace Dicomr.Dicom.Tests;

public class DicomReaderTests
{
    [Fact]
    public void ReferenceFixture_Opens()
    {
        var path = FindFixture();
        var file = DicomHeaderReader.Open(path);
        Assert.NotEmpty(file.Dataset);
        _ = file.Dataset.GetSingleValueOrDefault(DicomTag.Modality, string.Empty);
    }

    [Fact]
    public void ClassicHeader_ReadsProtocolFields()
    {
        var dataset = ClassicMr(ds =>
        {
            ds.Add(DicomTag.ProtocolName, "T1w_MPR_1mm");
            ds.Add(DicomTag.RepetitionTime, 2300.0);
            ds.Add(DicomTag.EchoTime, 2.98);
            ds.Add(DicomTag.FlipAngle, 9.0);
            ds.Add(DicomTag.ImageType, "ORIGINAL", "PRIMARY", "M");
            ds.Add(DicomTag.AcquisitionMatrix, (ushort)0, (ushort)256, (ushort)256, (ushort)0);
            ds.Add(DicomTag.PixelSpacing, 1.0, 1.0);
        });

        var record = InstanceMapper.Map(dataset, fileMeta: null, path: "t1.dcm");
        Assert.Equal("classic", record.Encoding);
        Assert.Equal(2300, Field(record, FieldKeys.RepetitionTime).AsNumber);
        Assert.Equal("ms", Field(record, FieldKeys.RepetitionTime).Unit);
        Assert.Equal(FieldSource.Standard, Field(record, FieldKeys.RepetitionTime).Source);
        Assert.Equal(2.98, Field(record, FieldKeys.EchoTime).AsNumber);
        Assert.Equal("ms", Field(record, FieldKeys.EchoTime).Unit);
        Assert.Equal(9, Field(record, FieldKeys.FlipAngle).AsNumber);
        Assert.Equal("deg", Field(record, FieldKeys.FlipAngle).Unit);
        Assert.Equal(["ORIGINAL", "PRIMARY", "M"], Field(record, FieldKeys.ImageType).AsTexts);
        Assert.Equal([0, 256, 256, 0], Field(record, FieldKeys.AcquisitionMatrix).AsNumbers);
        Assert.Equal([1, 1], Field(record, FieldKeys.PixelSpacing).AsNumbers);
        Assert.Equal("mm", Field(record, FieldKeys.PixelSpacing).Unit);
    }

    [Fact]
    public void ClassicZeroFlipAndRepetition_AreOmitted()
    {
        var dataset = ClassicMr(ds =>
        {
            ds.Add(DicomTag.FlipAngle, 0.0);
            ds.Add(DicomTag.RepetitionTime, 0.0);
            ds.Add(DicomTag.EchoTime, 5.0);
        });

        var record = InstanceMapper.Map(dataset, null, "zero.dcm");
        Assert.DoesNotContain(record.Fields, field => field.Keyword == FieldKeys.FlipAngle);
        Assert.DoesNotContain(record.Fields, field => field.Keyword == FieldKeys.RepetitionTime);
        Assert.Equal(5, Field(record, FieldKeys.EchoTime).AsNumber);
    }

    [Fact]
    public void MissingRepetitionTime_StillReturnsTheInstance()
    {
        var record = InstanceMapper.Map(ClassicMr(_ => { }), null, "bare.dcm");
        Assert.DoesNotContain(record.Fields, field => field.Keyword == FieldKeys.RepetitionTime);
        Assert.Equal("1.2.3", record.StudyInstanceUid);
    }

    [Fact]
    public void UnparsedDecimalString_WarnsAndKeepsOtherFields()
    {
        var dataset = ClassicMr(ds =>
        {
#pragma warning disable CS0618 // Invalid DS values are what this test is forcing.
            ds.AutoValidate = false;
#pragma warning restore CS0618
            ds.Add(new DicomDecimalString(DicomTag.RepetitionTime, ByteConverter.ToByteBuffer("not-a-number", System.Text.Encoding.ASCII)));
            ds.Add(DicomTag.EchoTime, 5.0);
        });

        var record = InstanceMapper.Map(dataset, null, "bad-ds.dcm");
        Assert.Equal(5, Field(record, FieldKeys.EchoTime).AsNumber);
        Assert.Contains(record.Warnings, warning => warning.Code == "field.unparsed");
        Assert.DoesNotContain(record.Fields, field => field.Keyword == FieldKeys.RepetitionTime);
    }

    [Fact]
    public void EnhancedEcho_UsesEffectiveEchoTime()
    {
        var dataset = EnhancedMr(frames:
        [
            EchoFrame(5),
        ]);

        var record = InstanceMapper.Map(dataset, null, "enh.dcm");
        var echo = Field(record, FieldKeys.EchoTime);
        Assert.Equal(5, echo.AsNumber);
        Assert.Equal(FieldSource.EnhancedMacro, echo.Source);
        Assert.Equal("enhanced", record.Encoding);
    }

    [Fact]
    public void EnhancedTiming_KeepsLegalZeroRepetitionTime()
    {
        var timing = new DicomDataset();
#pragma warning disable CS0618
        timing.AutoValidate = false;
#pragma warning restore CS0618
        timing.Add(new DicomDecimalString(DicomTag.RepetitionTime, "0"));
        var dataset = EnhancedShell();
        var frame = new DicomDataset
        {
            { MacroTags.Timing, new DicomSequence(MacroTags.Timing, timing) },
        };
        dataset.Add(new DicomSequence(DicomTag.PerFrameFunctionalGroupsSequence, frame));

        var record = InstanceMapper.Map(dataset, null, "tr0.dcm");
        var tr = Field(record, FieldKeys.RepetitionTime);
        Assert.Equal(0, tr.AsNumber);
        Assert.Equal("MRTimingAndRelatedParameters", tr.SourceDetail);
        Assert.Equal(FieldSource.EnhancedMacro, tr.Source);
    }

    [Fact]
    public void ThreeFrames_CollapseEchoTimes()
    {
        var dataset = EnhancedShell();
        var frames = new[] { 2.0, 4.0, 6.0 }
            .Select(echo => new DicomDataset
            {
                { MacroTags.Echo, new DicomSequence(MacroTags.Echo, new DicomDataset { { MacroTags.EffectiveEchoTime, echo } }) },
            })
            .ToArray();
        dataset.Add(new DicomSequence(DicomTag.PerFrameFunctionalGroupsSequence, frames));

        var record = InstanceMapper.Map(dataset, null, "echoes.dcm");
        var echo = Field(record, FieldKeys.EchoTime);
        Assert.Equal([2, 4, 6], echo.AsNumbers);
        Assert.True(echo.Varying);
        Assert.Equal(3, record.FrameCount);
    }

    [Fact]
    public void RootEchoTime_WinsOverASingleMacroValue()
    {
        var dataset = EnhancedShell();
        dataset.Add(DicomTag.EchoTime, 10.0);
        var frame = new DicomDataset
        {
            { MacroTags.Echo, new DicomSequence(MacroTags.Echo, new DicomDataset { { MacroTags.EffectiveEchoTime, 5.0 } }) },
        };
        dataset.Add(new DicomSequence(DicomTag.PerFrameFunctionalGroupsSequence, frame));

        var echo = Field(InstanceMapper.Map(dataset, null, "root-te.dcm"), FieldKeys.EchoTime);
        Assert.Equal(10, echo.AsNumber);
        Assert.Equal(FieldSource.Standard, echo.Source);
    }

    [Theory]
    [InlineData(new[] { 1.0, 0.0, 0.0, 0.0, 1.0, 0.0 }, "axial")]
    [InlineData(new[] { 0.0, 1.0, 0.0, 0.0, 0.0, -1.0 }, "sagittal")]
    public void Orientation_BecomesAcquisitionPlane(double[] cosines, string plane)
    {
        var dataset = ClassicMr(ds => ds.Add(DicomTag.ImageOrientationPatient, cosines));
        var record = InstanceMapper.Map(dataset, null, "plane.dcm");
        Assert.Equal(plane, Field(record, FieldKeys.AcquisitionPlane).AsText);
    }

    [Fact]
    public void PrivateIndex_ResolvesCreatorSlot()
    {
        var firstSlot = new DicomDataset();
        firstSlot.Add(new DicomLongString(new DicomTag(0x0029, 0x0010), "SIEMENS CSA HEADER"));
        firstSlot.Add(new DicomOtherByte(new DicomTag(0x0029, 0x1020), [1]));

        var secondSlot = new DicomDataset();
        secondSlot.Add(new DicomLongString(new DicomTag(0x0029, 0x0010), "OTHER"));
        secondSlot.Add(new DicomLongString(new DicomTag(0x0029, 0x0011), "SIEMENS CSA HEADER"));
        secondSlot.Add(new DicomOtherByte(new DicomTag(0x0029, 0x1120), [1]));

        var fromCreatorApi = new DicomDataset();
        fromCreatorApi.Add(new DicomOtherByte(new DicomTag(0x0029, 0x0020, new DicomPrivateCreator("SIEMENS CSA HEADER")), [1]));

        AssertSlot(PrivateTagIndex.Build(firstSlot), expectedSlot: 0x10);
        AssertSlot(PrivateTagIndex.Build(secondSlot), expectedSlot: 0x11);
        AssertSlot(PrivateTagIndex.Build(fromCreatorApi), expectedSlot: 0x10);
    }

    [Fact]
    public void SiemensCsaCreator_WarnsThatPhase3WillReadIt()
    {
        var dataset = ClassicMr(ds =>
        {
            ds.Add(DicomTag.Manufacturer, "SIEMENS");
            ds.Add(new DicomLongString(new DicomTag(0x0029, 0x0010), "SIEMENS CSA HEADER"));
            ds.Add(new DicomOtherByte(new DicomTag(0x0029, 0x1020), [1]));
        });

        var study = StudyAssembler.Assemble([InstanceMapper.Map(dataset, null, "csa.dcm")]);
        var warning = Assert.Single(study.Warnings, item => item.Code == "private.undecoded");
        Assert.Contains("Phase 3", warning.Message, StringComparison.Ordinal);
        Assert.Contains("SIEMENS CSA HEADER", Assert.Single(study.Acquisitions).Series[0].PrivateCreators, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Folder_SplitsStudies_SkipsDirectoryAndJunk()
    {
        var root = NewFolder();
        await SaveAsync(Path.Combine(root, "a.dcm"), ClassicMr(ds =>
        {
            ds.AddOrUpdate(DicomTag.StudyInstanceUID, "1.2.10");
            ds.Add(DicomTag.RepetitionTime, 2300.0);
        }));
        await SaveAsync(Path.Combine(root, "b.dcm"), ClassicMr(ds =>
        {
            ds.AddOrUpdate(DicomTag.StudyInstanceUID, "1.2.11");
            ds.AddOrUpdate(DicomTag.SeriesInstanceUID, "1.2.11.1");
            ds.Add(DicomTag.RepetitionTime, 5000.0);
        }));
        await SaveAsync(Path.Combine(root, "DICOMDIR"), new DicomDataset
        {
            { DicomTag.SOPClassUID, SopClasses.DicomDir },
            { DicomTag.SOPInstanceUID, DicomUID.Generate().UID },
            { DicomTag.Modality, "MR" },
        });
        await File.WriteAllBytesAsync(Path.Combine(root, "notes"), new byte[200]);

        var result = await StudyFolderReader.ReadAsync(root, new ReadRequest(), CancellationToken.None);
        Assert.Equal(2, result.Studies.Count);
        Assert.Equal(4, result.Stats.FilesSeen);
        Assert.Equal(2, result.Stats.FilesRead);
        Assert.Equal(2, result.Stats.FilesSkipped);
        Assert.Contains(result.Warnings, warning => warning.Code == "file.not-dicom");
    }

    [Fact]
    public async Task TwoSeriesInOneProtocol_GroupTogether()
    {
        var root = NewFolder();
        await SaveAsync(Path.Combine(root, "s1.dcm"), ClassicMr(ds =>
        {
            ds.Add(DicomTag.ProtocolName, "T1w");
            ds.Add(DicomTag.SeriesNumber, 1);
        }));
        await SaveAsync(Path.Combine(root, "s2.dcm"), ClassicMr(ds =>
        {
            ds.Add(DicomTag.ProtocolName, "T1w");
            ds.AddOrUpdate(DicomTag.SeriesInstanceUID, "1.2.3.5");
            ds.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUID.Generate().UID);
            ds.Add(DicomTag.SeriesNumber, 2);
        }));

        var result = await StudyFolderReader.ReadAsync(root, new ReadRequest(), CancellationToken.None);
        var study = Assert.Single(result.Studies);
        Assert.Equal(2, Assert.Single(study.Acquisitions).Series.Count);
    }

    [Fact]
    public async Task LargePixelData_IsSkippedAndPrivateHeaderIsKept()
    {
        var path = Path.Combine(NewFolder(), "bulk.dcm");
        var dataset = ClassicMr(ds =>
        {
            ds.Add(DicomTag.RepetitionTime, 2300.0);
            ds.Add(new DicomLongString(new DicomTag(0x0029, 0x0010), "SIEMENS CSA HEADER"));
            ds.Add(new DicomOtherByte(new DicomTag(0x0029, 0x1020), new byte[200 * 1024]));
            ds.Add(new DicomOtherByte(DicomTag.PixelData, new byte[8 * 1024 * 1024]));
        });
        await SaveAsync(path, dataset);

        var started = Stopwatch.StartNew();
        var opened = DicomHeaderReader.Open(path);
        var record = DicomHeaderReader.Read(path, includeNonMr: false);
        started.Stop();

        Assert.DoesNotContain(opened.Dataset, item => item.Tag == DicomTag.PixelData);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(5));
        var instance = Assert.IsType<HeaderReadResult.Instance>(record);
        Assert.Equal(2300, instance.Record.Fields.Single(field => field.Keyword == FieldKeys.RepetitionTime).AsNumber);
        Assert.Contains(PrivateTagIndex.Build(opened.Dataset).Elements, element => element.Group == 0x0029 && element.Offset == 0x20);
        Assert.DoesNotContain(
            instance.Record.GetType().GetProperties(),
            property => property.PropertyType == typeof(byte[]));
    }

    private static void AssertSlot(PrivateTagIndex index, byte expectedSlot)
    {
        var element = Assert.Single(index.Elements, item => item.Offset == 0x20 && item.Creator.Contains("SIEMENS CSA HEADER", StringComparison.Ordinal));
        Assert.Equal(expectedSlot, element.Slot);
        Assert.Equal((ushort)0x0029, element.Group);
    }

    private static DicomDataset ClassicMr(Action<DicomDataset> fill)
    {
        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, SopClasses.ClassicMr },
            { DicomTag.SOPInstanceUID, DicomUID.Generate().UID },
            { DicomTag.StudyInstanceUID, "1.2.3" },
            { DicomTag.SeriesInstanceUID, "1.2.3.4" },
            { DicomTag.Modality, "MR" },
        };
        fill(dataset);
        return dataset;
    }

    private static DicomDataset EnhancedShell() => new()
    {
        { DicomTag.SOPClassUID, SopClasses.EnhancedMr },
        { DicomTag.SOPInstanceUID, DicomUID.Generate().UID },
        { DicomTag.StudyInstanceUID, "1.2.3" },
        { DicomTag.SeriesInstanceUID, "1.2.3.4" },
        { DicomTag.Modality, "MR" },
    };

    private static DicomDataset EnhancedMr(DicomDataset[] frames)
    {
        var dataset = EnhancedShell();
        dataset.Add(new DicomSequence(DicomTag.PerFrameFunctionalGroupsSequence, frames));
        return dataset;
    }

    private static DicomDataset EchoFrame(double echoTime) => new()
    {
        {
            MacroTags.Echo,
            new DicomSequence(MacroTags.Echo, new DicomDataset { { MacroTags.EffectiveEchoTime, echoTime } })
        },
    };

    private static FieldValue Field(InstanceRecord record, string keyword) =>
        record.Fields.Single(field => field.Keyword == keyword);

    private static async Task SaveAsync(string path, DicomDataset dataset)
    {
        await new DicomFile(dataset).SaveAsync(path).ConfigureAwait(false);
    }

    private static string NewFolder()
    {
        var path = Path.Combine(Path.GetTempPath(), "Dicomr-poc1", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindFixture()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            foreach (var relative in new[]
                     {
                         Path.Combine("fixtures", "ref_dicom.dcm"),
                         Path.Combine("tests", "Dicomr.Dicom.Tests", "fixtures", "ref_dicom.dcm"),
                         Path.Combine("tests", "fixtures", "ref_dicom.dcm"),
                     })
            {
                var candidate = Path.Combine(dir.FullName, relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("ref_dicom.dcm was not found from the test output directory.");
    }
}
