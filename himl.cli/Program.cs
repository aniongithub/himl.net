using himl.core;
using himl.core.Interfaces;
using himl.Services;
using himl.Services.SecretResolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.CommandLine;
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
        var rootCommand = new RootCommand("HIML - Hierarchical Configuration using YAML (config-merger mode)");

        // Add the path argument
        var pathArgument = new Argument<string>("path", "Root path to the configuration directory hierarchy");
        rootCommand.AddArgument(pathArgument);

        // Add options (config-merger specific)
        var outputDirOption = new Option<string?>(new[] { "--output-dir", "-d" }, "Output directory for himl-config-merger mode (will write one file per leaf)");
        var formatOption = new Option<string>(new[] { "--format", "-f" }, () => "yaml", "Output format (yaml or json)");
        var filterOption = new Option<string[]>(new[] { "--filter" }, "Keep only these keys from the generated data");
        var excludeOption = new Option<string[]>(new[] { "--exclude" }, "Exclude these keys from the generated data");
        var skipInterpolationsOption = new Option<bool>(new[] { "--skip-interpolation-resolving" }, "Skip interpolation resolution");
        var skipSecretsOption = new Option<bool>(new[] { "--skip-secrets" }, "Skip secret resolution");
        var multiLineStringOption = new Option<bool>(new[] { "--multi-line-string" }, "Use multi-line string format for YAML output");
        var cwdOption = new Option<string?>(new[] { "--cwd" }, "Working directory for relative path resolution");
        var levelsOption = new Option<string[]>(new[] { "--levels" }, "List of level keys (eg: env region cluster) used to compute leaf directories");
        var filterRulesKeyOption = new Option<string?>(new[] { "--filter-rules-key" }, "Optional key name containing filter rules used by himl-config-merger (if present in configs)");
        var enclosingKeyOption = new Option<string?>(new[] { "--enclosing-key" }, "Wrap output under this key");
        var removeEnclosingKeyOption = new Option<string?>(new[] { "--remove-enclosing-key" }, "Remove this wrapper key from output if present");
        var listMergeStrategyOption = new Option<string?>(new[] { "--list-merge-strategy" }, "List merge strategy: Override, Append, Prepend, AppendUnique");

        rootCommand.AddOption(outputDirOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(filterOption);
        rootCommand.AddOption(excludeOption);
        rootCommand.AddOption(skipInterpolationsOption);
        rootCommand.AddOption(skipSecretsOption);
        rootCommand.AddOption(multiLineStringOption);
        rootCommand.AddOption(cwdOption);
        rootCommand.AddOption(levelsOption);
        rootCommand.AddOption(filterRulesKeyOption);
        rootCommand.AddOption(enclosingKeyOption);
        rootCommand.AddOption(removeEnclosingKeyOption);
        rootCommand.AddOption(listMergeStrategyOption);

        // Set the handler
        rootCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var outputDir = context.ParseResult.GetValueForOption(outputDirOption);
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var filters = context.ParseResult.GetValueForOption(filterOption) ?? Array.Empty<string>();
            var excludes = context.ParseResult.GetValueForOption(excludeOption) ?? Array.Empty<string>();
            var skipInterpolations = context.ParseResult.GetValueForOption(skipInterpolationsOption);
            var skipSecrets = context.ParseResult.GetValueForOption(skipSecretsOption);
            var multiLineString = context.ParseResult.GetValueForOption(multiLineStringOption);
            var cwd = context.ParseResult.GetValueForOption(cwdOption);
            var levels = context.ParseResult.GetValueForOption(levelsOption) ?? Array.Empty<string>();
            var filterRulesKey = context.ParseResult.GetValueForOption(filterRulesKeyOption);
            var enclosingKey = context.ParseResult.GetValueForOption(enclosingKeyOption);
            var removeEnclosingKey = context.ParseResult.GetValueForOption(removeEnclosingKeyOption);
            var listMergeStrategyStr = context.ParseResult.GetValueForOption(listMergeStrategyOption);

            try
            {
                // Validate mandatory config-merger options
                if (string.IsNullOrEmpty(outputDir))
                {
                    Console.Error.WriteLine("--output-dir is required. This CLI runs in himl-config-merger mode and always writes merged files to an output directory.");
                    context.ExitCode = 2;
                    return;
                }

                if (levels.Length == 0)
                {
                    Console.Error.WriteLine("--levels must be provided when using --output-dir to compute leaf directories (eg: --levels env region cluster)");
                    context.ExitCode = 2;
                    return;
                }

                // Setup DI container once and reuse for all operations
                var host = Host.CreateDefaultBuilder()
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

                // Parse output format
                if (!Enum.TryParse<OutputFormat>(format, true, out var outputFormat))
                {
                    Console.Error.WriteLine($"Invalid output format: {format}");
                    context.ExitCode = 1;
                    return;
                }

                // Parse list merge strategy if provided
                ListMergeStrategy listMergeStrategy = ListMergeStrategy.AppendUnique;
                if (!string.IsNullOrEmpty(listMergeStrategyStr) && Enum.TryParse<ListMergeStrategy>(listMergeStrategyStr, true, out var parsed))
                {
                    listMergeStrategy = parsed;
                }

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
                    ListMergeStrategy = listMergeStrategy,
                    OutputFormat = outputFormat
                };

                var processor = host.Services.GetRequiredService<IConfigurationProcessor>();
                var formatter = host.Services.GetRequiredService<IOutputFormatter>();
                var logger = host.Services.GetRequiredService<ILogger<CliApp>>();

                logger.LogInformation("Running in himl-config-merger mode. Scanning for leaf directories under: {Path}", path);

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

                foreach (var leaf in leafDirs)
                {
                    logger.LogInformation("Processing leaf: {Leaf}", leaf);

                    // Use a copy of base options per leaf
                    var options = new HimlOptions
                    {
                        Filters = baseOptions.Filters,
                        ExcludeKeys = baseOptions.ExcludeKeys,
                        SkipInterpolations = baseOptions.SkipInterpolations,
                        SkipSecrets = baseOptions.SkipSecrets,
                        MultiLineString = baseOptions.MultiLineString,
                        WorkingDirectory = baseOptions.WorkingDirectory,
                        EnclosingKey = baseOptions.EnclosingKey,
                        RemoveEnclosingKey = baseOptions.RemoveEnclosingKey,
                        ListMergeStrategy = baseOptions.ListMergeStrategy,
                        OutputFormat = baseOptions.OutputFormat
                    };

                    var result = await processor.ProcessAsync(leaf, options);

                    if (result.Errors.Any())
                    {
                        logger.LogError("Processing leaf {Leaf} failed with errors:", leaf);
                        foreach (var error in result.Errors)
                        {
                            logger.LogError("  {Error}", error);
                        }
                        // Continue processing others but mark non-zero exit in the end
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
                        OutputFormat.Yaml => formatter.ToYaml(result.Data, options.MultiLineString),
                        OutputFormat.Json => formatter.ToJson(result.Data, new JsonSerializerOptions { WriteIndented = true }),
                        _ => formatter.ToYaml(result.Data, options.MultiLineString)
                    };

                    await File.WriteAllTextAsync(filename, outputText);
                    logger.LogInformation("Stored generated config to: {File}", filename);
                }

                logger.LogInformation("himl-config-merger run completed");
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
}
