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
        var rootCommand = new RootCommand("HIML - Hierarchical Configuration using YAML");

        // Add the path argument
        var pathArgument = new Argument<string>("path", "Path to the configuration directory hierarchy");
        rootCommand.AddArgument(pathArgument);

        // Add options
        var outputFileOption = new Option<string?>(["--output-file", "-o"], "Output file location (if not specified, prints to console)");
        var formatOption = new Option<string>(["--format", "-f"], () => "yaml", "Output format (yaml or json)");
        var filterOption = new Option<string[]>(["--filter"], "Keep only these keys from the generated data");
        var excludeOption = new Option<string[]>(["--exclude"], "Exclude these keys from the generated data");
        var skipInterpolationsOption = new Option<bool>(["--skip-interpolation-resolving"], "Skip interpolation resolution");
        var skipSecretsOption = new Option<bool>(["--skip-secrets"], "Skip secret resolution");
        var multiLineStringOption = new Option<bool>(["--multi-line-string"], "Use multi-line string format for YAML output");

        rootCommand.AddOption(outputFileOption);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(filterOption);
        rootCommand.AddOption(excludeOption);
        rootCommand.AddOption(skipInterpolationsOption);
        rootCommand.AddOption(skipSecretsOption);
        rootCommand.AddOption(multiLineStringOption);

        // Set the handler
        rootCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var outputFile = context.ParseResult.GetValueForOption(outputFileOption);
            var format = context.ParseResult.GetValueForOption(formatOption)!;
            var filters = context.ParseResult.GetValueForOption(filterOption) ?? Array.Empty<string>();
            var excludes = context.ParseResult.GetValueForOption(excludeOption) ?? Array.Empty<string>();
            var skipInterpolations = context.ParseResult.GetValueForOption(skipInterpolationsOption);
            var skipSecrets = context.ParseResult.GetValueForOption(skipSecretsOption);
            var multiLineString = context.ParseResult.GetValueForOption(multiLineStringOption);

            try
            {
                // Setup DI container
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

                // Create options
                var options = new HimlOptions
                {
                    Filters = filters.ToList(),
                    ExcludeKeys = excludes.ToList(),
                    SkipInterpolations = skipInterpolations,
                    SkipSecrets = skipSecrets,
                    MultiLineString = multiLineString,
                };

                // Process configuration
                var processor = host.Services.GetRequiredService<IConfigurationProcessor>();
                var formatter = host.Services.GetRequiredService<IOutputFormatter>();
                var logger = host.Services.GetRequiredService<ILogger<CliApp>>();

                logger.LogInformation("Processing configuration from path: {Path}", path);
                var result = await processor.ProcessAsync(path, options);

                if (result.Errors.Any())
                {
                    logger.LogError("Processing failed with errors:");
                    foreach (var error in result.Errors)
                    {
                        logger.LogError("  {Error}", error);
                    }
                    context.ExitCode = 1;
                    return;
                }

                if (result.Warnings.Any())
                {
                    foreach (var warning in result.Warnings)
                    {
                        logger.LogWarning("{Warning}", warning);
                    }
                }

                // Format output
                var output = outputFormat switch
                {
                    OutputFormat.Yaml => formatter.ToYaml(result.Data, multiLineString),
                    OutputFormat.Json => formatter.ToJson(result.Data, new JsonSerializerOptions { WriteIndented = true }),
                    _ => throw new ArgumentException($"Unsupported output format: {outputFormat}")
                };

                // Write output
                if (string.IsNullOrEmpty(outputFile))
                {
                    Console.WriteLine(output);
                }
                else
                {
                    await File.WriteAllTextAsync(outputFile, output);
                    logger.LogInformation("Output written to: {OutputFile}", outputFile);
                }

                logger.LogInformation("Configuration processing completed successfully");
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
