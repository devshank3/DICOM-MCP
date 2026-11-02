namespace Dicomr.Core;

public sealed record StudyWarning
{
    public required string Code { get; init; }

    public string? Path { get; init; }

    public required string Message { get; init; }
}

public sealed record ReadStats
{
    public int FilesSeen { get; init; }

    public int FilesRead { get; init; }

    public int FilesSkipped { get; init; }

    public int Studies { get; init; }
}

public sealed record StudyIdentity
{
    public required string StudyInstanceUid { get; init; }

    public string? StudyDescription { get; init; }

    public string? PatientId { get; init; }

    public string? PatientName { get; init; }

    public string? Manufacturer { get; init; }

    public string? ManufacturerModelName { get; init; }

    public double? MagneticFieldStrengthTesla { get; init; }

    public string? Modality { get; init; }
}

public sealed record DistinctFieldCount
{
    public required string Keyword { get; init; }

    public int Count { get; init; }
}

public sealed record SeriesRecord
{
    public required string SeriesInstanceUid { get; init; }

    public string? SeriesDescription { get; init; }

    public int? SeriesNumber { get; init; }

    public string? SeriesTime { get; init; }

    public required string SopClassUid { get; init; }

    public required string Encoding { get; init; }

    public int InstanceCount { get; init; }

    public int FrameCount { get; init; }

    public required IReadOnlyList<FieldValue> Fields { get; init; }

    public IReadOnlyList<string> PrivateCreators { get; init; } = [];

    public IReadOnlyList<DistinctFieldCount> DistinctCounts { get; init; } = [];
}

public sealed record AcquisitionRecord
{
    public required string Name { get; init; }

    public string? ProtocolName { get; init; }

    public string? SequenceName { get; init; }

    public required IReadOnlyList<SeriesRecord> Series { get; init; }
}

public sealed record StudyProtocol
{
    public int SchemaVersion { get; init; } = 1;

    public required StudyIdentity Study { get; init; }

    public required IReadOnlyList<AcquisitionRecord> Acquisitions { get; init; }

    public required ReadStats Stats { get; init; }

    public required IReadOnlyList<StudyWarning> Warnings { get; init; }
}
