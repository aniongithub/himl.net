using System.Text.Json;

namespace himl.core.Interfaces;

/// <summary>
/// Interface for processing hierarchical configuration files
/// </summary>
public interface IConfigurationProcessor
{
    /// <summary>
    /// Process a configuration hierarchy starting from the given path
    /// </summary>
    /// <param name="path">Path to the configuration directory</param>
    /// <param name="options">Processing options</param>
    /// <returns>Processed configuration result</returns>
    Task<HimlResult> ProcessAsync(string path, HimlOptions? options = null);
    
    /// <summary>
    /// Process a configuration hierarchy starting from the given path
    /// </summary>
    /// <param name="path">Path to the configuration directory</param>
    /// <param name="options">Processing options</param>
    /// <returns>Processed configuration result</returns>
    HimlResult Process(string path, HimlOptions? options = null);
}

/// <summary>
/// Interface for merging configuration data
/// </summary>
public interface IConfigurationMerger
{
    /// <summary>
    /// Deep merge two configuration objects
    /// </summary>
    /// <param name="target">Target configuration to merge into</param>
    /// <param name="source">Source configuration to merge from</param>
    /// <param name="options">Merge options</param>
    /// <returns>Merged configuration</returns>
    IDictionary<string, object?> Merge(
        IDictionary<string, object?> target, 
        IDictionary<string, object?> source, 
        HimlOptions options);
}

/// <summary>
/// Interface for resolving interpolations in configuration values
/// </summary>
public interface IInterpolationResolver
{
    /// <summary>
    /// Resolve interpolations in the given data
    /// </summary>
    /// <param name="data">Configuration data containing interpolations</param>
    /// <param name="options">Processing options</param>
    /// <returns>Data with resolved interpolations</returns>
    Task<IDictionary<string, object?>> ResolveAsync(
        IDictionary<string, object?> data, 
        HimlOptions options);
}

/// <summary>
/// Interface for resolving secrets in configuration values
/// </summary>
public interface ISecretResolver
{
    /// <summary>
    /// Check if this resolver supports the given secret type
    /// </summary>
    /// <param name="secretType">Type of secret (e.g., "ssm", "s3", "vault")</param>
    /// <returns>True if supported</returns>
    bool Supports(string secretType);
    
    /// <summary>
    /// Resolve a secret value
    /// </summary>
    /// <param name="secretType">Type of secret</param>
    /// <param name="parameters">Secret parameters</param>
    /// <returns>Resolved secret value</returns>
    Task<string> ResolveAsync(string secretType, IDictionary<string, string> parameters);
}

/// <summary>
/// Interface for formatting output data
/// </summary>
public interface IOutputFormatter
{
    /// <summary>
    /// Format data as YAML
    /// </summary>
    /// <param name="data">Data to format</param>
    /// <param name="multiLineString">Use multi-line string format</param>
    /// <returns>YAML string</returns>
    string ToYaml(IDictionary<string, object?> data, bool multiLineString = false);
    
    /// <summary>
    /// Format data as JSON
    /// </summary>
    /// <param name="data">Data to format</param>
    /// <param name="options">JSON serializer options</param>
    /// <returns>JSON string</returns>
    string ToJson(IDictionary<string, object?> data, JsonSerializerOptions? options = null);
}
