using himl.core;
using himl.core.Interfaces;
using himl.Services;
using himl.Services.SecretResolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;

namespace himl.cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var app = new CliApp();
        return await app.RunAsync(args);
    }
}

public class CliApp
{
    public async Task<int> RunAsync(string[] args)
    {
        // Create the root command
        var rootCommand = new RootCommand("HIML - Hierarchical Configuration using YAML");

        // Add the path argument
        var pathArgument = new Argument<string>("path", "Root path to the configuration directory hierarchy");
        rootCommand.AddArgument(pathArgument);

        var outputDirOption = new Option<string?>(new[] { "--output-dir" }, "Output directory (config-merger mode: writes one file per leaf)");
        var outputFileOption = new Option<string?>(new[] { "--output-file" }, "Output file path (single file mode)");
        var printDataOption = new Option<bool>(new[] { "--print-data" }, "Print generated data to stdout");
        var formatOption = new Option<string>(new[] { "--format" }, () => "yaml", "Output format (yaml or json)");
        var filterOption = new Option<string[]>(new[] { "--filter" }, "Keep only these keys from the generated data");
        var excludeOption = new Option<string[]>(new[] { "--exclude" }, "Exclude these keys from the generated data");
        var skipInterpolationValidationOption = new Option<bool>(new[] { "--skip-interpolation-validation" }, "Skip interpolation validation");
        var skipInterpolationsOption = new Option<bool>(new[] { "--skip-interpolation-resolving" }, "Skip interpolation resolution");
        var skipSecretsOption = new Option<bool>(new[] { "--skip-secrets" }, "Skip secret resolution");
        var multiLineStringOption = new Option<bool>(new[] { "--multi-line-string" }, "Use multi-line string format for YAML output");
        var cwdOption = new Option<string?>(new[] { "--cwd" }, "Working directory for relative path resolution");
        var enclosingKeyOption = new Option<string?>(new[] { "--enclosing-key" }, "Wrap output under this key");
        var removeEnclosingKeyOption = new Option<string?>(new[] { "--remove-enclosing-key" }, "Remove this wrapper key from output if present");
        var levelsOption = new Option<string[]>(new[] { "--levels" }, "List of level keys (config-merger mode: env region cluster)");

        rootCommand.AddOption(outputDirOption);
        rootCommand.AddOption(outputFileOption);
        rootCommand.AddOption(printDataOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(filterOption);
        rootCommand.AddOption(excludeOption);
        rootCommand.AddOption(skipInterpolationValidationOption);
        rootCommand.AddOption(skipInterpolationsOption);
        rootCommand.AddOption(skipSecretsOption);
        rootCommand.AddOption(multiLineStringOption);
        rootCommand.AddOption(cwdOption);
        rootCommand.AddOption(levelsOption);
        rootCommand.AddOption(enclosingKeyOption);
        rootCommand.AddOption(removeEnclosingKeyOption);

        // Set the handler
        rootCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var outputDir = context.ParseResult.GetValueForOption(outputDirOption);
            var outputFile = context.ParseResult.GetValueForOption(outputFileOption);
            var printData = context.ParseResult.GetValueForOption(printDataOption);
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var filters = context.ParseResult.GetValueForOption(filterOption) ?? Array.Empty<string>();
            var excludes = context.ParseResult.GetValueForOption(excludeOption) ?? Array.Empty<string>();
            var skipInterpolationValidation = context.ParseResult.GetValueForOption(skipInterpolationValidationOption);
            var skipInterpolations = context.ParseResult.GetValueForOption(skipInterpolationsOption);
            var skipSecrets = context.ParseResult.GetValueForOption(skipSecretsOption);
            var multiLineString = context.ParseResult.GetValueForOption(multiLineStringOption);
            var cwd = context.ParseResult.GetValueForOption(cwdOption);
            var levels = context.ParseResult.GetValueForOption(levelsOption) ?? Array.Empty<string>();
            var enclosingKey = context.ParseResult.GetValueForOption(enclosingKeyOption);
            var removeEnclosingKey = context.ParseResult.GetValueForOption(removeEnclosingKeyOption);

            try
            {
                // Determine mode: config-merger (output-dir + levels) vs single file (output-file or print-data)
                var isConfigMergerMode = !string.IsNullOrEmpty(outputDir);
                
                if (isConfigMergerMode && !string.IsNullOrEmpty(outputDir))
                {
                    // Config-merger mode: scan hierarchy and write one file per leaf
                    if (levels.Length == 0)
                    {
                        Console.Error.WriteLine("--levels must be provided when using --output-dir (config-merger mode)");
                        context.ExitCode = 2;
                        return;
                    }
                    
                    await RunConfigMergerMode(path, outputDir, levels, format, filters, excludes, 
                        skipInterpolations, skipSecrets, multiLineString, cwd, enclosingKey, 
                        removeEnclosingKey, context);
                }
                else
                {
                    // Single file mode: process one path and output to file or stdout
                    if (string.IsNullOrEmpty(outputFile) && !printData)
                    {
                        printData = true; // Default to printing if no output file specified
                    }
                    
                    await RunSingleFileMode(path, outputFile, printData, format, filters, excludes, 
                        skipInterpolations, skipSecrets, skipInterpolationValidation, multiLineString, 
                        cwd, enclosingKey, removeEnclosingKey, context);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                context.ExitCode = 1;
            }
        });

        // Execute the command
        return await rootCommand.InvokeAsync(args);
    }

    private async Task RunSingleFileMode(string path, string? outputFile, bool printData, string format,
        string[] filters, string[] excludes, bool skipInterpolations, bool skipSecrets, 
        bool skipInterpolationValidation, bool multiLineString, string? cwd, string? enclosingKey,
        string? removeEnclosingKey, InvocationContext context)
    {
        var host = CreateHost();
        var processor = host.Services.GetRequiredService<IConfigurationProcessor>();
        var formatter = host.Services.GetRequiredService<IOutputFormatter>();

        // Parse output format
        if (!Enum.TryParse<OutputFormat>(format, true, out var outputFormat))
        {
            Console.Error.WriteLine($"Invalid output format: {format}");
            context.ExitCode = 1;
            return;
        }

        var options = new HimlOptions
        {
            Filters = filters.ToList(),
            ExcludeKeys = excludes.ToList(),
            SkipInterpolations = skipInterpolations,
            SkipSecrets = skipSecrets,
            MultiLineString = multiLineString,
            WorkingDirectory = string.IsNullOrEmpty(cwd) ? null : cwd,
            EnclosingKey = enclosingKey,
            RemoveEnclosingKey = removeEnclosingKey,
            OutputFormat = outputFormat
        };

        var result = await processor.ProcessAsync(path, options);

        if (result.Errors.Any())
        {
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"Error: {error}");
            }
            context.ExitCode = 1;
            return;
        }

        // Format output
        var outputText = outputFormat switch
        {
            OutputFormat.Yaml => formatter.ToYaml(result.Data, options.MultiLineString),
            OutputFormat.Json => formatter.ToJson(result.Data, new JsonSerializerOptions { WriteIndented = true }),
            _ => formatter.ToYaml(result.Data, options.MultiLineString)
        };

        if (printData)
        {
            Console.WriteLine(outputText);
        }

        if (!string.IsNullOrEmpty(outputFile))
        {
            await File.WriteAllTextAsync(outputFile, outputText);
        }
    }

    private async Task RunConfigMergerMode(string path, string outputDir, string[] levels, string format,
        string[] filters, string[] excludes, bool skipInterpolations, bool skipSecrets, 
        bool multiLineString, string? cwd, string? enclosingKey, string? removeEnclosingKey,
        InvocationContext context)
    {
        var host = CreateHost();
        var processor = host.Services.GetRequiredService<IConfigurationProcessor>();
        var formatter = host.Services.GetRequiredService<IOutputFormatter>();
        var logger = host.Services.GetRequiredService<ILogger<CliApp>>();

        // Parse output format
        if (!Enum.TryParse<OutputFormat>(format, true, out var outputFormat))
        {
            Console.Error.WriteLine($"Invalid output format: {format}");
            context.ExitCode = 1;
            return;
        }

        logger.LogInformation("Scanning for leaf directories under: {Path}", path);

        // Find candidate directories (including root)
        var candidateDirs = new List<string> { path };
        if (Directory.Exists(path))
        {
            candidateDirs.AddRange(Directory.GetDirectories(path, "*", SearchOption.AllDirectories));
        }
        else
        {
            Console.Error.WriteLine($"Input path does not exist or is not a directory: {path}");
            context.ExitCode = 2;
            return;
        }

        var leafDirs = new List<string>();

        foreach (var dir in candidateDirs)
        {
            // Determine relative path to the provided root so extraction yields keys like env=dev
            var rel = Path.GetRelativePath(path, dir);
            var relForExtraction = rel == "." ? string.Empty : rel;

            var values = himl.core.Utils.DirectoryHierarchy.ExtractValuesFromPath(relForExtraction);

            // Check if all requested levels are present in the extracted values
            var hasAllLevels = levels.All(l => values.ContainsKey(l));
            if (hasAllLevels)
            {
                leafDirs.Add(dir);
            }
        }

        logger.LogInformation("Found {Count} leaf directories to process", leafDirs.Count);

        var baseOptions = new HimlOptions
        {
            Filters = filters.ToList(),
            ExcludeKeys = excludes.ToList(),
            SkipInterpolations = skipInterpolations,
            SkipSecrets = skipSecrets,
            MultiLineString = multiLineString,
            WorkingDirectory = string.IsNullOrEmpty(cwd) ? null : cwd,
            EnclosingKey = enclosingKey,
            RemoveEnclosingKey = removeEnclosingKey,
            OutputFormat = outputFormat
        };

        foreach (var leaf in leafDirs)
        {
            logger.LogInformation("Processing leaf: {Leaf}", leaf);

            var result = await processor.ProcessAsync(leaf, baseOptions);

            if (result.Errors.Any())
            {
                logger.LogError("Processing leaf {Leaf} failed with errors:", leaf);
                foreach (var error in result.Errors)
                {
                    logger.LogError("  {Error}", error);
                }
                context.ExitCode = 1;
                continue;
            }

            // Determine output path using extracted values from the leaf relative path
            var rel = Path.GetRelativePath(path, leaf);
            var relForExtraction = rel == "." ? string.Empty : rel;
            var values = himl.core.Utils.DirectoryHierarchy.ExtractValuesFromPath(relForExtraction);

            // Build output subpath: for all levels except last, create directories; last becomes filename
            var subDirs = new List<string>();
            foreach (var lvl in levels.Take(levels.Length - 1))
            {
                if (values.TryGetValue(lvl, out var v))
                    subDirs.Add(v);
            }

            var lastLevel = levels.Last();
            if (!values.TryGetValue(lastLevel, out var lastVal))
            {
                lastVal = "leaf"; // fallback
            }

            var outDir = Path.Combine(new[] { outputDir }.Concat(subDirs).ToArray());
            Directory.CreateDirectory(outDir);

            var ext = outputFormat == OutputFormat.Json ? "json" : "yaml";
            var filename = Path.Combine(outDir, $"{lastVal}.{ext}");

            // Write formatted output
            var outputText = outputFormat switch
            {
                OutputFormat.Yaml => formatter.ToYaml(result.Data, baseOptions.MultiLineString),
                OutputFormat.Json => formatter.ToJson(result.Data, new JsonSerializerOptions { WriteIndented = true }),
                _ => formatter.ToYaml(result.Data, baseOptions.MultiLineString)
            };

            await File.WriteAllTextAsync(filename, outputText);
            logger.LogInformation("Stored generated config to: {File}", filename);
        }

        logger.LogInformation("Run completed");
    }

    private IHost CreateHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                services.AddSingleton<IConfigurationMerger, ConfigurationMerger>();
                services.AddSingleton<IInterpolationResolver, InterpolationResolver>();
                services.AddSingleton<IOutputFormatter, OutputFormatter>();
                services.AddSingleton<ISecretResolver, SsmSecretResolver>();
                services.AddSingleton<ISecretResolver, S3SecretResolver>();
                services.AddSingleton<ISecretResolver, VaultSecretResolver>();
                services.AddSingleton<IConfigurationProcessor, ConfigurationProcessor>();
            })
            .Build();
    }
}
