using Dicomr.Core;
using FellowOakDicom;
using FellowOakDicom.Serialization;

namespace Dicomr.Dicom;

public abstract record HeaderReadResult
{
    public sealed record Instance(InstanceRecord Record) : HeaderReadResult;

    public sealed record Skipped(string Path, string Code, string Message, bool Warn) : HeaderReadResult;
}

public static class DicomHeaderReader
{
    public static DicomFile Open(string path) =>
        DicomFile.Open(path, FileReadOption.SkipLargeTags, HeaderBudget.Bytes);

    public static HeaderReadResult Read(string path, bool includeNonMr)
    {
        DicomFile file;
        try
        {
            file = Open(path);
        }
        catch (Exception ex) when (ex is DicomException or IOException or UnauthorizedAccessException)
        {
            var code = ex is IOException or UnauthorizedAccessException ? "file.unreadable" : "file.not-dicom";
            return new HeaderReadResult.Skipped(path, code, ex.Message, Warn: true);
        }

        string sop;
        string modality;
        try
        {
            sop = SafeText(file.Dataset, DicomTag.SOPClassUID);
            if (sop.Length == 0)
            {
                sop = file.FileMetaInfo.MediaStorageSOPClassUID?.UID ?? "";
            }

            modality = SafeText(file.Dataset, DicomTag.Modality);
        }
        catch (DicomException ex)
        {
            return new HeaderReadResult.Skipped(path, "file.not-dicom", ex.Message, Warn: true);
        }

        if (SopClasses.IsDicomDir(sop))
        {
            return new HeaderReadResult.Skipped(path, "file.dicomdir", "DICOMDIR", Warn: false);
        }

        if (!includeNonMr && !string.Equals(modality, "MR", StringComparison.OrdinalIgnoreCase))
        {
            var detail = string.IsNullOrWhiteSpace(modality) ? "Modality is absent." : $"Modality is {modality}.";
            return new HeaderReadResult.Skipped(path, "file.not-mr", detail, Warn: true);
        }

        try
        {
            return new HeaderReadResult.Instance(InstanceMapper.Map(file.Dataset, file.FileMetaInfo, path));
        }
        catch (DicomException ex)
        {
            return new HeaderReadResult.Skipped(path, "file.not-dicom", ex.Message, Warn: true);
        }
    }

    private static string SafeText(DicomDataset dataset, DicomTag tag)
    {
        if (dataset.TryGetSingleValue(tag, out string value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        return "";
    }

    public static string DumpHeader(string path)
    {
        var file = Open(path);
        return DicomJson.ConvertDicomToJson(
            file.Dataset,
            writeTagsAsKeywords: true,
            formatIndented: true,
            numberSerializationMode: NumberSerializationMode.AsString);
    }
}
