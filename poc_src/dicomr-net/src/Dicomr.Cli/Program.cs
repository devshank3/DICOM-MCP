using System.CommandLine;
using Microsoft.Extensions.Logging;

var pathArgument = new Argument<string>("path")
{
    Description = "A DICOM file or a folder of DICOM files.",
};

var outputOption = new Option<string>("--out")
{
    Description = "Protocol JSON path. A folder with several studies writes one file per Study Instance UID.",
};

var stdoutOption = new Option<bool>("--stdout")
{
    Description = "Write protocol JSON to standard output instead of a file.",
};

var includeNonMrOption = new Option<bool>("--include-non-mr")
{
    Description = "Keep series whose Modality is not MR.",
};

var dumpHeaderOption = new Option<bool>("--dump-header")
{
    Description = "Also write fo-dicom keyword JSON for a single file. This is not the protocol document.",
};

var verboseOption = new Option<bool>("--verbose")
{
    Description = "Log at debug level.",
};

var readCommand = new Command("read", "Read MRI protocols from DICOM files.");
readCommand.Add(pathArgument);
readCommand.Add(outputOption);
readCommand.Add(stdoutOption);
readCommand.Add(includeNonMrOption);
readCommand.Add(dumpHeaderOption);
readCommand.Add(verboseOption);

readCommand.SetAction((parseResult, cancellationToken) =>
{
    var options = new Dicomr.Cli.ReadCommandOptions
    {
        Path = parseResult.GetRequiredValue(pathArgument),
        Output = parseResult.GetValue(outputOption),
        Stdout = parseResult.GetValue(stdoutOption),
        IncludeNonMr = parseResult.GetValue(includeNonMrOption),
        DumpHeader = parseResult.GetValue(dumpHeaderOption),
    };

    using var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder.SetMinimumLevel(parseResult.GetValue(verboseOption) ? LogLevel.Debug : LogLevel.Information);
        builder.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);
    });
    var logger = loggerFactory.CreateLogger("dicomr-net");
    return Dicomr.Cli.ReadCommand.ExecuteAsync(
        options,
        Console.Out,
        Console.Error,
        message => logger.LogInformation("{Message}", message),
        cancellationToken);
});

var root = new RootCommand("Read MRI protocols from DICOM studies.");
root.Subcommands.Add(readCommand);
return root.Parse(args).Invoke();
