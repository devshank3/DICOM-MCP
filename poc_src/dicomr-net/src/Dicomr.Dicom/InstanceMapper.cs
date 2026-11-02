using Dicomr.Core;
using FellowOakDicom;

namespace Dicomr.Dicom;

public static class InstanceMapper
{
    public static InstanceRecord Map(DicomDataset dataset, DicomFileMetaInformation? fileMeta, string? path)
    {
        var warnings = new List<StudyWarning>();
        var sop = Text(dataset, DicomTag.SOPClassUID)
            ?? fileMeta?.MediaStorageSOPClassUID?.UID
            ?? "";
        var modality = Text(dataset, DicomTag.Modality);
        var perFrame = dataset.Contains(DicomTag.PerFrameFunctionalGroupsSequence);
        var enhancedSop = SopClasses.IsEnhanced(sop);
        var encoding = perFrame || enhancedSop
            ? "enhanced"
            : SopClasses.IsClassicMr(sop) ? "classic" : "other";

        if (string.Equals(modality, "MR", StringComparison.OrdinalIgnoreCase) && encoding == "other")
        {
            warnings.Add(new StudyWarning
            {
                Code = "sop.unhandled",
                Path = path,
                Message = $"MR SOP class {sop} is read with the public attribute table only.",
            });
        }

        var fields = StandardFieldReader.Read(dataset, warnings, path).ToList();
        var geometry = new List<GeometrySample>();
        TakeGeometry(dataset, fields, geometry, FieldKeys.ImagePositionPatient, DicomTag.ImagePositionPatient);
        if (dataset.TryGetSingleValue(DicomTag.SliceLocation, out double slice))
        {
            geometry.Add(new GeometrySample(FieldKeys.SliceLocation, [slice]));
        }

        var frameCount = 1;
        if (encoding == "enhanced")
        {
            var root = fields.ToDictionary(field => field.Keyword, StringComparer.Ordinal);
            var enhanced = EnhancedFieldReader.Read(dataset, root, path, enhancedSop);
            warnings.AddRange(enhanced.Warnings);
            foreach (var field in enhanced.Fields)
            {
                var index = fields.FindIndex(existing => existing.Keyword == field.Keyword);
                if (index >= 0)
                {
                    fields[index] = field;
                }
                else
                {
                    fields.Add(field);
                }
            }

            if (enhanced.Geometry.Count > 0)
            {
                geometry.RemoveAll(sample => sample.Keyword == FieldKeys.ImagePositionPatient);
                geometry.AddRange(enhanced.Geometry);
            }

            frameCount = enhanced.FrameCount;
        }

        AddPlane(fields);
        var privateIndex = PrivateTagIndex.Build(dataset);

        return new InstanceRecord
        {
            Path = path,
            StudyInstanceUid = Text(dataset, DicomTag.StudyInstanceUID) ?? "unknown",
            SeriesInstanceUid = Text(dataset, DicomTag.SeriesInstanceUID) ?? $"unknown-series-{Text(dataset, DicomTag.SOPInstanceUID) ?? path}",
            SopInstanceUid = Text(dataset, DicomTag.SOPInstanceUID) ?? "",
            SopClassUid = sop,
            Encoding = encoding,
            Modality = modality,
            StudyDescription = Text(dataset, DicomTag.StudyDescription),
            PatientId = Text(dataset, DicomTag.PatientID),
            PatientName = Text(dataset, DicomTag.PatientName),
            Manufacturer = Text(dataset, DicomTag.Manufacturer),
            ManufacturerModelName = Text(dataset, DicomTag.ManufacturerModelName),
            SoftwareVersions = Join(dataset, DicomTag.SoftwareVersions),
            ProtocolName = Text(dataset, DicomTag.ProtocolName),
            SequenceName = Text(dataset, DicomTag.SequenceName),
            SeriesDescription = Text(dataset, DicomTag.SeriesDescription),
            SeriesNumber = Integer(dataset, DicomTag.SeriesNumber),
            SeriesTime = Text(dataset, DicomTag.SeriesTime),
            InstanceNumber = Integer(dataset, DicomTag.InstanceNumber),
            FrameCount = frameCount,
            Fields = fields,
            PrivateCreators = privateIndex.Creators,
            Warnings = warnings,
            Geometry = geometry,
        };
    }

    private static void AddPlane(List<FieldValue> fields)
    {
        if (fields.Any(field => field.Keyword == FieldKeys.AcquisitionPlane))
        {
            return;
        }

        var orientation = fields.FirstOrDefault(field => field.Keyword == FieldKeys.ImageOrientationPatient);
        if (orientation?.AsNumbers is not IReadOnlyList<double> cosines)
        {
            return;
        }

        var plane = AcquisitionPlane.FromDirectionCosines(cosines);
        if (plane is null)
        {
            return;
        }

        fields.Add(FieldRegistry.Create(FieldKeys.AcquisitionPlane, new FieldScalar.Text(plane), orientation.Source, "ImageOrientationPatient"));
    }

    private static void TakeGeometry(
        DicomDataset dataset,
        List<FieldValue> fields,
        List<GeometrySample> geometry,
        string keyword,
        DicomTag tag)
    {
        var field = fields.FirstOrDefault(item => item.Keyword == keyword);
        if (field?.AsNumbers is IReadOnlyList<double> numbers)
        {
            geometry.Add(new GeometrySample(keyword, numbers.ToArray()));
            fields.Remove(field);
            return;
        }

        if (dataset.TryGetValues(tag, out double[] values) && values.Length > 0)
        {
            geometry.Add(new GeometrySample(keyword, values));
        }
    }

    private static string? Text(DicomDataset dataset, DicomTag tag)
    {
        if (!dataset.TryGetSingleValue(tag, out string value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? Join(DicomDataset dataset, DicomTag tag)
    {
        if (!dataset.TryGetValues(tag, out string[] values) || values.Length == 0)
        {
            return Text(dataset, tag);
        }

        var parts = values.Select(value => value.Trim()).Where(value => value.Length > 0).ToArray();
        return parts.Length == 0 ? null : string.Join('\\', parts);
    }

    private static int? Integer(DicomDataset dataset, DicomTag tag)
    {
        if (dataset.TryGetSingleValue(tag, out int value))
        {
            return value;
        }

        return null;
    }
}
