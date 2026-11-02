namespace Dicomr.Core;

/// <summary>
/// One patient-frame sample used only to count distinct positions. It is not a protocol field.
/// </summary>
public readonly record struct GeometrySample(string Keyword, double[] Components);

public sealed record InstanceRecord
{
    public string? Path { get; init; }

    public required string StudyInstanceUid { get; init; }

    public required string SeriesInstanceUid { get; init; }

    public required string SopInstanceUid { get; init; }

    public required string SopClassUid { get; init; }

    /// <summary>classic, enhanced, or other.</summary>
    public required string Encoding { get; init; }

    public string? Modality { get; init; }

    public string? StudyDescription { get; init; }

    public string? PatientId { get; init; }

    public string? PatientName { get; init; }

    public string? Manufacturer { get; init; }

    public string? ManufacturerModelName { get; init; }

    public string? SoftwareVersions { get; init; }

    public string? ProtocolName { get; init; }

    public string? SequenceName { get; init; }

    public string? SeriesDescription { get; init; }

    public int? SeriesNumber { get; init; }

    public string? SeriesTime { get; init; }

    public int? InstanceNumber { get; init; }

    public int FrameCount { get; init; } = 1;

    public required IReadOnlyList<FieldValue> Fields { get; init; }

    public IReadOnlyList<string> PrivateCreators { get; init; } = [];

    public IReadOnlyList<StudyWarning> Warnings { get; init; } = [];

    public IReadOnlyList<GeometrySample> Geometry { get; init; } = [];
}
