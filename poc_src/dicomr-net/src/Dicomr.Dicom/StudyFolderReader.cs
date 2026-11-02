using Dicomr.Core;

namespace Dicomr.Dicom;

public sealed record ReadRequest
{
    public bool IncludeNonMr { get; init; }

    public int MaxDegreeOfParallelism { get; init; } = Environment.ProcessorCount;
}

public sealed record FolderReadResult
{
    public required IReadOnlyList<StudyProtocol> Studies { get; init; }

    public required ReadStats Stats { get; init; }

    public required IReadOnlyList<StudyWarning> Warnings { get; init; }
}

public static class StudyFolderReader
{
    private const int MaxListedSkips = 20;

    public static async Task<FolderReadResult> ReadAsync(string path, ReadRequest request, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Path does not exist: {path}");
        }

        var files = Enumerate(path).ToList();
        var results = new HeaderReadResult?[files.Count];
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, request.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, files.Count),
            options,
            async (index, token) =>
            {
                var file = files[index];
                results[index] = await Task.Run(() => ReadOne(file, request.IncludeNonMr), token).ConfigureAwait(false);
            }).ConfigureAwait(false);

        var instances = new List<InstanceRecord>();
        var warnings = new List<StudyWarning>();
        var skipped = 0;
        foreach (var result in results)
        {
            switch (result)
            {
                case HeaderReadResult.Instance instance:
                    instances.Add(instance.Record);
                    break;
                case HeaderReadResult.Skipped skip:
                    skipped++;
                    if (skip.Warn)
                    {
                        warnings.Add(new StudyWarning
                        {
                            Code = skip.Code,
                            Path = skip.Path,
                            Message = skip.Message,
                        });
                    }

                    break;
            }
        }

        var capped = Cap(warnings);
        var studies = instances
            .GroupBy(instance => instance.StudyInstanceUid, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var members = group.ToList();
                return StudyAssembler.Assemble(
                    members,
                    new ReadStats
                    {
                        FilesSeen = files.Count,
                        FilesRead = members.Count,
                        FilesSkipped = skipped,
                        Studies = 1,
                    },
                    capped);
            })
            .ToList();

        return new FolderReadResult
        {
            Studies = studies,
            Stats = new ReadStats
            {
                FilesSeen = files.Count,
                FilesRead = instances.Count,
                FilesSkipped = skipped,
                Studies = studies.Count,
            },
            Warnings = capped,
        };
    }

    private static HeaderReadResult ReadOne(string path, bool includeNonMr)
    {
        var info = new FileInfo(path);
        if (info.Length < 132)
        {
            return new HeaderReadResult.Skipped(path, "file.not-dicom", "File is shorter than a DICOM header.", Warn: true);
        }

        if (!IsCandidate(path) && !HasDicomPrefix(path))
        {
            return new HeaderReadResult.Skipped(path, "file.not-dicom", "File is not a DICOM candidate.", Warn: true);
        }

        return DicomHeaderReader.Read(path, includeNonMr);
    }

    private static IEnumerable<string> Enumerate(string path)
    {
        if (File.Exists(path))
        {
            yield return path;
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
        {
            yield return file;
        }
    }

    private static bool IsCandidate(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Length == 0
            || extension.Equals(".dcm", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".dicom", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ima", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDicomPrefix(string path)
    {
        Span<byte> prefix = stackalloc byte[4];
        using var stream = File.OpenRead(path);
        stream.Seek(128, SeekOrigin.Begin);
        return stream.Read(prefix) == 4
            && prefix[0] == (byte)'D'
            && prefix[1] == (byte)'I'
            && prefix[2] == (byte)'C'
            && prefix[3] == (byte)'M';
    }

    private static IReadOnlyList<StudyWarning> Cap(List<StudyWarning> warnings)
    {
        var kept = new List<StudyWarning>();
        foreach (var group in warnings.GroupBy(warning => warning.Code, StringComparer.Ordinal))
        {
            var items = group.ToList();
            kept.AddRange(items.Take(MaxListedSkips));
            if (items.Count > MaxListedSkips)
            {
                kept.Add(new StudyWarning
                {
                    Code = group.Key,
                    Message = $"{items.Count - MaxListedSkips} additional files were not listed ({group.Key}).",
                });
            }
        }

        return kept;
    }
}
