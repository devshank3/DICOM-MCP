using Dicomr.Core;
using Dicomr.Dicom;

namespace Dicomr.Cli;

public sealed record ReadCommandOptions
{
    public required string Path { get; init; }

    public string? Output { get; init; }

    public bool Stdout { get; init; }

    public bool IncludeNonMr { get; init; }

    public bool DumpHeader { get; init; }
}

public static class ReadCommand
{
    public static async Task<int> ExecuteAsync(
        ReadCommandOptions options,
        TextWriter stdout,
        TextWriter stderr,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(options.Path) && !Directory.Exists(options.Path))
        {
            stderr.WriteLine($"Path does not exist: {options.Path}");
            return 2;
        }

        if (options.DumpHeader && Directory.Exists(options.Path))
        {
            stderr.WriteLine("--dump-header reads one DICOM file.");
            return 2;
        }

        var started = DateTime.UtcNow;
        FolderReadResult result;
        try
        {
            result = await StudyFolderReader.ReadAsync(
                options.Path,
                new ReadRequest { IncludeNonMr = options.IncludeNonMr },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }

        var elapsed = DateTime.UtcNow - started;
        log?.Invoke($"Read {result.Stats.FilesRead} of {result.Stats.FilesSeen} files in {elapsed.TotalSeconds:0.0}s. Studies: {result.Stats.Studies}.");
        foreach (var study in result.Studies)
        {
            log?.Invoke($"Study {study.Study.StudyInstanceUid}");
        }

        if (options.DumpHeader)
        {
            var headerPath = HeaderPath(options);
            await File.WriteAllTextAsync(headerPath, DicomHeaderReader.DumpHeader(options.Path), cancellationToken).ConfigureAwait(false);
            stderr.WriteLine($"Wrote header dump {headerPath}");
        }

        if (options.Stdout)
        {
            stderr.WriteLine(ProtocolSummary.PatientNotice);
            var json = result.Studies.Count == 1
                ? ProtocolJson.Serialize(result.Studies[0], indented: true)
                : "[" + string.Join("," + Environment.NewLine, result.Studies.Select(study => ProtocolJson.Serialize(study, indented: true))) + "]";
            stdout.WriteLine(json);
            return 0;
        }

        stdout.WriteLine(ProtocolSummary.PatientNotice);
        if (result.Studies.Count == 0)
        {
            stdout.WriteLine($"No MR studies read. {result.Stats.FilesSkipped} files skipped.");
            foreach (var warning in result.Warnings.Take(5))
            {
                stdout.WriteLine($"  ! {warning.Code}: {warning.Message}");
            }

            return 0;
        }

        var written = await WriteJsonAsync(options, result, cancellationToken).ConfigureAwait(false);
        foreach (var study in result.Studies)
        {
            ProtocolSummary.Write(study, stdout);
            stdout.WriteLine();
        }

        foreach (var file in written)
        {
            stdout.WriteLine($"Wrote {file}");
        }

        return 0;
    }

    private static async Task<IReadOnlyList<string>> WriteJsonAsync(
        ReadCommandOptions options,
        FolderReadResult result,
        CancellationToken cancellationToken)
    {
        var paths = new List<string>();
        if (result.Studies.Count == 1 && !string.IsNullOrWhiteSpace(options.Output))
        {
            paths.Add(options.Output);
        }
        else
        {
            var directory = OutputDirectory(options.Output);
            Directory.CreateDirectory(directory);
            foreach (var study in result.Studies)
            {
                var name = Sanitize(study.Study.StudyInstanceUid) + ".protocol.json";
                paths.Add(Path.Combine(directory, name));
            }
        }

        for (var i = 0; i < result.Studies.Count; i++)
        {
            var json = ProtocolJson.Serialize(result.Studies[i], indented: true);
            await File.WriteAllTextAsync(paths[i], json, cancellationToken).ConfigureAwait(false);
        }

        return paths;
    }

    private static string OutputDirectory(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Directory.GetCurrentDirectory();
        }

        if (output.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(Path.GetFullPath(output)) ?? Directory.GetCurrentDirectory();
        }

        return output;
    }

    private static string HeaderPath(ReadCommandOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Output))
        {
            return options.Output.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? options.Output + ".header.json"
                : Path.Combine(options.Output, Path.GetFileName(options.Path) + ".header.json");
        }

        return options.Path + ".header.json";
    }

    private static string Sanitize(string uid)
    {
        var chars = uid.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character).ToArray();
        return new string(chars);
    }
}
