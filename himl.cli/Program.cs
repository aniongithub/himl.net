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
        // Create the root command - exactly matching himl-config-merger
        var rootCommand = new RootCommand("HIML Config Merger - Generate configuration files from hierarchical YAML");

        // Add the path argument
        var pathArgument = new Argument<string>("path", "The configs directory");
        rootCommand.AddArgument(pathArgument);

        // Required options (matching himl-config-merger exactly)
        var outputDirOption = new Option<string>(new[] { "--output-dir" }, "output directory, where generated configs will be saved")
        {
            IsRequired = true
        };
        
        var levelsOption = new Option<string[]>(new[] { "--levels" }, "hierarchy levels, for instance: env, region, cluster")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };
        
        var leafDirectoriesOption = new Option<string[]>(new[] { "--leaf-directories" }, "leaf directories, for instance: cluster")
        {
            IsRequired = true,
            AllowMultipleArgumentsPerToken = true
        };

        // Optional options (matching himl-config-merger exactly)
        var enableParallelOption = new Option<bool>(new[] { "--enable-parallel" }, "Process config using multiprocessing");
        var filterRulesKeyOption = new Option<string?>(new[] { "--filter-rules-key" }, "keep these keys from the generated data, based on the configured filter key");

        rootCommand.AddOption(outputDirOption);
        rootCommand.AddOption(levelsOption);
        rootCommand.AddOption(leafDirectoriesOption);
        rootCommand.AddOption(enableParallelOption);
        rootCommand.AddOption(filterRulesKeyOption);

        // Set the handler
        rootCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var outputDir = context.ParseResult.GetValueForOption(outputDirOption)!;
            var levels = context.ParseResult.GetValueForOption(levelsOption)!;
            var leafDirectories = context.ParseResult.GetValueForOption(leafDirectoriesOption)!;
            var enableParallel = context.ParseResult.GetValueForOption(enableParallelOption);
            var filterRulesKey = context.ParseResult.GetValueForOption(filterRulesKeyOption);

            try
            {
                await RunConfigMerger(path, outputDir, levels, leafDirectories, enableParallel, filterRulesKey, context);
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

    private async Task RunConfigMerger(string path, string outputDir, string[] levels, string[] leafDirectories, 
        bool enableParallel, string? filterRulesKey, InvocationContext context)
    {
        if (!Directory.Exists(path))
        {
            throw new ArgumentException($"Path does not exist: {path}");
        }

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        var host = CreateHost();
        var processor = host.Services.GetRequiredService<IConfigurationProcessor>();
        var formatter = host.Services.GetRequiredService<IOutputFormatter>();
        var logger = host.Services.GetRequiredService<ILogger<CliApp>>();

        logger.LogInformation("Scanning for leaf directories under: {Path}", path);

        // Find candidate directories (including root)
        var candidateDirs = new List<string> { path };
        if (Directory.Exists(path))
        {
            candidateDirs.AddRange(Directory.GetDirectories(path, "*", SearchOption.AllDirectories));
        }

        var leafDirs = new List<string>();

        foreach (var dir in candidateDirs)
        {
            // Determine relative path to the provided root
            var rel = Path.GetRelativePath(path, dir);
            var relForExtraction = rel == "." ? string.Empty : rel;

            var values = himl.core.Utils.DirectoryHierarchy.ExtractValuesFromPath(relForExtraction);

            // Check if all requested levels are present AND if this is a leaf directory
            var hasAllLevels = levels.All(l => values.ContainsKey(l));
            var isLeafDirectory = leafDirectories.Any(ld => values.ContainsKey(ld));
            
            if (hasAllLevels && isLeafDirectory)
            {
                leafDirs.Add(dir);
            }
        }

        logger.LogInformation("Found {Count} leaf directories to process", leafDirs.Count);

        var baseOptions = new HimlOptions
        {
            Filters = new List<string>(),
            ExcludeKeys = new List<string>(),
            SkipInterpolations = false,
            SkipSecrets = false,
            MultiLineString = false,
            WorkingDirectory = null,
            EnclosingKey = null,
            RemoveEnclosingKey = null,
            OutputFormat = OutputFormat.Yaml
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

            // Apply filter if specified
            var outputData = result.Data;
            if (!string.IsNullOrEmpty(filterRulesKey) && outputData != null)
            {
                if (outputData.TryGetValue(filterRulesKey, out var filterValue) && filterValue is IDictionary<string, object?> filterDict)
                {
                    outputData = filterDict;
                }
            }

            // Generate output file name using leaf directory values
            var rel = Path.GetRelativePath(path, leaf);
            var relForExtraction = rel == "." ? string.Empty : rel;
            var values = himl.core.Utils.DirectoryHierarchy.ExtractValuesFromPath(relForExtraction);

            var fileName = string.Join("-", leafDirectories.Select(ld => 
            {
                values.TryGetValue(ld, out var value);
                return value ?? "unknown";
            }).Where(v => v != "unknown")) + ".yaml";
            
            var outputFile = Path.Combine(outputDir, fileName);
            
            // Write formatted output
            var outputText = formatter.ToYaml(outputData ?? new Dictionary<string, object?>(), false);
            
            await File.WriteAllTextAsync(outputFile, outputText);
            logger.LogInformation("Generated: {File}", outputFile);
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
                services.AddSingleton<ISecretResolver, GoogleSecretManagerResolver>();
                services.AddSingleton<IConfigurationProcessor, ConfigurationProcessor>();
            })
            .Build();
    }
}
