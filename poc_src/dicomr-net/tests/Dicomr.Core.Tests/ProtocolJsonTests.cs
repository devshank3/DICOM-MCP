using Dicomr.Core;

namespace Dicomr.Core.Tests;

public class ProtocolJsonTests
{
    [Fact]
    public void RepetitionTime_RoundTrips()
    {
        var protocol = Sample(FieldRegistry.Create(FieldKeys.RepetitionTime, new FieldScalar.Number(2300)));
        var json = ProtocolJson.Serialize(protocol);
        var restored = ProtocolJson.Deserialize(json);

        var field = restored.Acquisitions[0].Series[0].Fields[0];
        Assert.Equal(FieldKeys.RepetitionTime, field.Keyword);
        Assert.Equal("0018,0080", field.Tag);
        Assert.Equal("ms", field.Unit);
        Assert.Equal(FieldSource.Standard, field.Source);
        Assert.Equal(2300, field.AsNumber);
        Assert.Contains("\"number\": 2300", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"text\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanName_CollapsesWhitespaceAndKeepsCase()
    {
        Assert.Equal("T1w MPR", CleanName.Collapse("  T1w   MPR  "));
        Assert.Equal("t1w_MPR_RR", CleanName.Collapse("t1w_MPR_RR"));
    }

    [Fact]
    public void NonZeroPolicy_DropsClassicZeroFlip()
    {
        var zero = FieldRegistry.Create(FieldKeys.FlipAngle, new FieldScalar.Number(0));
        var nine = FieldRegistry.Create(FieldKeys.FlipAngle, new FieldScalar.Number(9));
        Assert.True(NonZeroPolicy.Omit(zero));
        Assert.False(NonZeroPolicy.Omit(nine));
        Assert.True(NonZeroPolicy.Omit(FieldKeys.RepetitionTime, 0));
    }

    private static StudyProtocol Sample(FieldValue field) => new()
    {
        Study = new StudyIdentity { StudyInstanceUid = "1.2.3", Modality = "MR" },
        Acquisitions =
        [
            new AcquisitionRecord
            {
                Name = "T1",
                Series =
                [
                    new SeriesRecord
                    {
                        SeriesInstanceUid = "1.2.3.4",
                        SopClassUid = "1.2.840.10008.5.1.4.1.1.4",
                        Encoding = "classic",
                        InstanceCount = 1,
                        FrameCount = 1,
                        Fields = [field],
                    },
                ],
            },
        ],
        Stats = new ReadStats { FilesSeen = 1, FilesRead = 1, Studies = 1 },
        Warnings = [],
    };
}
