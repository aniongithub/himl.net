using himl.core;
using himl.core.Interfaces;
using himl.Services;
using himl.Services.SecretResolvers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace himl.Extensions;

/// <summary>
/// Extension methods for IConfigurationBuilder to add HIML support
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Add HIML configuration source
    /// </summary>
    /// <param name="builder">Configuration builder</param>
    /// <param name="path">Path to HIML configuration directory</param>
    /// <param name="optional">Whether the source is optional</param>
    /// <param name="options">HIML options</param>
    /// <returns>Configuration builder for chaining</returns>
    public static IConfigurationBuilder AddHiml(
        this IConfigurationBuilder builder,
        string path,
        bool optional = false,
        HimlOptions? options = null)
    {
        return builder.Add(new HimlConfigurationSource(path, optional, options));
    }

    /// <summary>
    /// Add HIML configuration source with configuration action
    /// </summary>
    /// <param name="builder">Configuration builder</param>
    /// <param name="path">Path to HIML configuration directory</param>
    /// <param name="configureOptions">Action to configure HIML options</param>
    /// <returns>Configuration builder for chaining</returns>
    public static IConfigurationBuilder AddHiml(
        this IConfigurationBuilder builder,
        string path,
        Action<HimlOptions> configureOptions)
    {
        var options = new HimlOptions();
        configureOptions(options);
        return builder.AddHiml(path, false, options);
    }
}

/// <summary>
/// HIML configuration source for Microsoft.Extensions.Configuration
/// </summary>
public class HimlConfigurationSource : IConfigurationSource
{
    private readonly string _path;
    private readonly bool _optional;
    private readonly HimlOptions _options;

    public HimlConfigurationSource(string path, bool optional = false, HimlOptions? options = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _optional = optional;
        _options = options ?? new HimlOptions();
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new HimlConfigurationProvider(_path, _optional, _options);
    }
}

/// <summary>
/// HIML configuration provider for Microsoft.Extensions.Configuration
/// </summary>
public class HimlConfigurationProvider : ConfigurationProvider
{
    private readonly string _path;
    private readonly bool _optional;
    private readonly HimlOptions _options;

    public HimlConfigurationProvider(string path, bool optional, HimlOptions options)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _optional = optional;
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public override void Load()
    {
        try
        {
            // Create a minimal service collection for DI
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());
            services.AddSingleton<IConfigurationMerger, ConfigurationMerger>();
            services.AddSingleton<IInterpolationResolver, InterpolationResolver>();
            services.AddSingleton<IOutputFormatter, OutputFormatter>();
            services.AddSingleton<ISecretResolver, SsmSecretResolver>();
            services.AddSingleton<ISecretResolver, S3SecretResolver>();
            services.AddSingleton<ISecretResolver, VaultSecretResolver>();
            services.AddSingleton<ISecretResolver, GoogleSecretManagerResolver>();
            services.AddSingleton<IConfigurationProcessor, ConfigurationProcessor>();

            using var serviceProvider = services.BuildServiceProvider();
            var processor = serviceProvider.GetRequiredService<IConfigurationProcessor>();

            var result = processor.Process(_path, _options);
            
            if (result.Errors.Any())
            {
                throw new InvalidOperationException($"HIML processing failed: {string.Join(", ", result.Errors)}");
            }

            Data = FlattenData(result.Data);
        }
        catch (Exception ex)
        {
            if (!_optional)
            {
                throw new InvalidOperationException($"Failed to load HIML configuration from '{_path}': {ex.Message}", ex);
            }
            // If optional, just set empty data and continue
            Data = new Dictionary<string, string?>();
        }
    }

    /// <summary>
    /// Flatten the hierarchical data into the flat key-value structure expected by IConfiguration
    /// </summary>
    private IDictionary<string, string?> FlattenData(IDictionary<string, object?> data, string prefix = "")
    {
        var result = new Dictionary<string, string?>();

        foreach (var kvp in data)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}:{kvp.Key}";
            
            switch (kvp.Value)
            {
                case IDictionary<string, object?> dict:
                    var nested = FlattenData(dict, key);
                    foreach (var nestedKvp in nested)
                    {
                        result[nestedKvp.Key] = nestedKvp.Value;
                    }
                    break;
                    
                case IList<object?> list:
                    for (int i = 0; i < list.Count; i++)
                    {
                        var itemKey = $"{key}:{i}";
                        if (list[i] is IDictionary<string, object?> listDict)
                        {
                            var listNested = FlattenData(listDict, itemKey);
                            foreach (var listNestedKvp in listNested)
                            {
                                result[listNestedKvp.Key] = listNestedKvp.Value;
                            }
                        }
                        else
                        {
                            result[itemKey] = list[i]?.ToString();
                        }
                    }
                    break;
                    
                default:
                    result[key] = kvp.Value?.ToString();
                    break;
            }
        }

        return result;
    }
}
