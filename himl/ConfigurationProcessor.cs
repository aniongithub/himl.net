using himl.core;
using himl.core.Interfaces;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace himl;

/// <summary>
/// Main configuration processor for HIML
/// </summary>
public class ConfigurationProcessor : IConfigurationProcessor
{
    private readonly ILogger<ConfigurationProcessor> _logger;
    private readonly IConfigurationMerger _merger;
    private readonly IInterpolationResolver _interpolationResolver;
    private readonly IOutputFormatter _formatter;
    private readonly IEnumerable<ISecretResolver> _secretResolvers;

    public ConfigurationProcessor(
        ILogger<ConfigurationProcessor> logger,
        IConfigurationMerger merger,
        IInterpolationResolver interpolationResolver,
        IOutputFormatter formatter,
        IEnumerable<ISecretResolver> secretResolvers)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _merger = merger ?? throw new ArgumentNullException(nameof(merger));
        _interpolationResolver = interpolationResolver ?? throw new ArgumentNullException(nameof(interpolationResolver));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _secretResolvers = secretResolvers ?? throw new ArgumentNullException(nameof(secretResolvers));
    }

    /// <summary>
    /// Process configuration hierarchy synchronously
    /// </summary>
    public HimlResult Process(string path, HimlOptions? options = null)
    {
        return ProcessAsync(path, options).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Process configuration hierarchy asynchronously
    /// </summary>
    public async Task<HimlResult> ProcessAsync(string path, HimlOptions? options = null)
    {
        options ??= new HimlOptions();
        var result = new HimlResult();

        try
        {
            _logger.LogInformation("Processing configuration hierarchy from path: {Path}", path);

            // Step 1: Generate hierarchy and load files
            IDictionary<string, object?> mergedData;
            
            if (File.Exists(path))
            {
                // Single file processing
                _logger.LogDebug("Processing single file: {Path}", path);
                mergedData = await LoadYamlFile(path);
            }
            else
            {
                // Directory hierarchy processing  
                var hierarchy = core.Utils.DirectoryHierarchy.GenerateHierarchy(path, options.WorkingDirectory);
                mergedData = await LoadAndMergeHierarchy(hierarchy, options);
            }

            // Step 2: Apply path-based values
            ApplyPathValues(mergedData, path);

            // Step 3: Exclude keys before interpolation
            if (options.ExcludeKeys.Any())
            {
                ExcludeKeys(mergedData, options.ExcludeKeys);
            }

            // Step 4: Resolve interpolations
            if (!options.SkipInterpolations)
            {
                mergedData = await _interpolationResolver.ResolveAsync(mergedData, options);
            }

            // Step 5: Filter keys after interpolation
            if (options.Filters.Any())
            {
                FilterData(mergedData, options.Filters);
            }

            // Step 6: Apply enclosing key operations
            if (!string.IsNullOrEmpty(options.RemoveEnclosingKey))
            {
                RemoveEnclosingKey(mergedData, options.RemoveEnclosingKey);
            }

            if (!string.IsNullOrEmpty(options.EnclosingKey))
            {
                AddEnclosingKey(mergedData, options.EnclosingKey);
            }

            result.Data = mergedData;
            
            // Format output based on specified format
            result.Output = options.OutputFormat switch
            {
                OutputFormat.Json => _formatter.ToJson(mergedData),
                OutputFormat.Yaml => _formatter.ToYaml(mergedData, options.MultiLineString),
                _ => _formatter.ToYaml(mergedData, options.MultiLineString)
            };
            
            _logger.LogInformation("Configuration processing completed successfully");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing configuration hierarchy");
            result.Errors.Add(ex.Message);
            throw new HimlException($"Failed to process configuration: {ex.Message}", ex);
        }
    }

    private async Task<IDictionary<string, object?>> LoadAndMergeHierarchy(
        IList<string> hierarchy, 
        HimlOptions options)
    {
        IDictionary<string, object?> result = new Dictionary<string, object?>();

        foreach (var directory in hierarchy)
        {
            var files = core.Utils.DirectoryHierarchy.GetConfigurationFiles(directory);
            foreach (var file in files)
            {
                _logger.LogDebug("Loading configuration file: {File}", file);
                var fileData = await LoadYamlFile(file);
                result = _merger.Merge(result, fileData, options);
            }
        }

        return result;
    }

    private async Task<IDictionary<string, object?>> LoadYamlFile(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yamlData = deserializer.Deserialize<Dictionary<string, object>>(content);
            return ConvertYamlData(yamlData ?? new Dictionary<string, object>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load YAML file: {FilePath}", filePath);
            throw new HimlException($"Failed to load YAML file '{filePath}': {ex.Message}", ex);
        }
    }

    private IDictionary<string, object?> ConvertYamlData(IDictionary<string, object> yamlData)
    {
        var result = new Dictionary<string, object?>();
        
        foreach (var kvp in yamlData)
        {
            result[kvp.Key] = ConvertYamlValue(kvp.Value);
        }
        
        return result;
    }

    private object? ConvertYamlValue(object? value)
    {
        return value switch
        {
            Dictionary<object, object> dict => ConvertYamlDict(dict),
            List<object> list => ConvertYamlList(list),
            _ => value
        };
    }

    private IDictionary<string, object?> ConvertYamlDict(Dictionary<object, object> dict)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in dict)
        {
            if (kvp.Key?.ToString() is string key)
            {
                result[key] = ConvertYamlValue(kvp.Value);
            }
        }
        return result;
    }

    private IList<object?> ConvertYamlList(List<object> list)
    {
        return list.Select(ConvertYamlValue).ToList();
    }

    private void ApplyPathValues(IDictionary<string, object?> data, string path)
    {
        var pathValues = core.Utils.DirectoryHierarchy.ExtractValuesFromPath(path);
        foreach (var kvp in pathValues)
        {
            data[kvp.Key] = kvp.Value;
        }
    }

    private void ExcludeKeys(IDictionary<string, object?> data, IList<string> excludeKeys)
    {
        foreach (var key in excludeKeys.ToList())
        {
            data.Remove(key);
        }
    }

    private void FilterData(IDictionary<string, object?> data, IList<string> filters)
    {
        var keysToRemove = data.Keys.Except(filters).ToList();
        foreach (var key in keysToRemove)
        {
            data.Remove(key);
        }
    }

    private void RemoveEnclosingKey(IDictionary<string, object?> data, string enclosingKey)
    {
        if (data.TryGetValue(enclosingKey, out var value) && value is IDictionary<string, object?> dict)
        {
            data.Clear();
            foreach (var kvp in dict)
            {
                data[kvp.Key] = kvp.Value;
            }
        }
    }

    private void AddEnclosingKey(IDictionary<string, object?> data, string enclosingKey)
    {
        var wrappedData = new Dictionary<string, object?>(data);
        data.Clear();
        data[enclosingKey] = wrappedData;
    }
}