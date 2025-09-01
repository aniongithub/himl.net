namespace himl.core;

/// <summary>
/// Merge strategy for lists when merging configurations
/// </summary>
public enum ListMergeStrategy
{
    /// <summary>
    /// Replace the entire list with the new one
    /// </summary>
    Override,
    
    /// <summary>
    /// Append new items to the existing list
    /// </summary>
    Append,
    
    /// <summary>
    /// Prepend new items to the existing list
    /// </summary>
    Prepend,
    
    /// <summary>
    /// Append new items but avoid duplicates
    /// </summary>
    AppendUnique
}

/// <summary>
/// Merge strategy for dictionaries when merging configurations
/// </summary>
public enum DictMergeStrategy
{
    /// <summary>
    /// Replace the entire dictionary with the new one
    /// </summary>
    Override,
    
    /// <summary>
    /// Deep merge dictionaries recursively
    /// </summary>
    Merge
}

/// <summary>
/// Input format for configuration files
/// </summary>
public enum InputFormat
{
    /// <summary>
    /// YAML format
    /// </summary>
    Yaml,
    
    /// <summary>
    /// JSON format
    /// </summary>
    Json
}

/// <summary>
/// Output format for processed configuration
/// </summary>
public enum OutputFormat
{
    /// <summary>
    /// YAML format
    /// </summary>
    Yaml,
    
    /// <summary>
    /// JSON format
    /// </summary>
    Json
}

/// <summary>
/// File merging mode for configuration processing
/// </summary>
public enum MergeMode
{
    /// <summary>
    /// Merge all YAML files in each directory into a single output (default behavior)
    /// </summary>
    AllFiles,
    
    /// <summary>
    /// Merge only same-named YAML files across the hierarchy, producing multiple outputs per leaf
    /// </summary>
    SameNamedFiles
}

/// <summary>
/// Configuration options for HIML processing
/// </summary>
public class HimlOptions
{
    /// <summary>
    /// Input format for configuration files
    /// </summary>
    public InputFormat InputFormat { get; set; } = InputFormat.Yaml;
    
    /// <summary>
    /// Output format for processed configuration
    /// </summary>
    public OutputFormat OutputFormat { get; set; } = OutputFormat.Yaml;
    
    /// <summary>
    /// Working directory for relative path resolution
    /// </summary>
    public string? WorkingDirectory { get; set; }
    
    /// <summary>
    /// Keys to include in the output (filter)
    /// </summary>
    public IList<string> Filters { get; set; } = new List<string>();
    
    /// <summary>
    /// Keys to exclude from the output
    /// </summary>
    public IList<string> ExcludeKeys { get; set; } = new List<string>();
    
    /// <summary>
    /// Key to wrap the entire output under
    /// </summary>
    public string? EnclosingKey { get; set; }
    
    /// <summary>
    /// Key to remove from the output wrapper
    /// </summary>
    public string? RemoveEnclosingKey { get; set; }
    
    /// <summary>
    /// Skip interpolation resolution
    /// </summary>
    public bool SkipInterpolations { get; set; }
    
    /// <summary>
    /// Skip interpolation validation
    /// </summary>
    public bool SkipInterpolationValidation { get; set; }
    
    /// <summary>
    /// Skip secret resolution
    /// </summary>
    public bool SkipSecrets { get; set; }
    
    /// <summary>
    /// Use multi-line string format for YAML output
    /// </summary>
    public bool MultiLineString { get; set; }
    
    /// <summary>
    /// Merge strategy for lists
    /// </summary>
    public ListMergeStrategy ListMergeStrategy { get; set; } = ListMergeStrategy.AppendUnique;
    
    /// <summary>
    /// Merge strategy for dictionaries
    /// </summary>
    public DictMergeStrategy DictMergeStrategy { get; set; } = DictMergeStrategy.Merge;
    
    /// <summary>
    /// File merging mode
    /// </summary>
    public MergeMode MergeMode { get; set; } = MergeMode.AllFiles;
    
    /// <summary>
    /// Default AWS profile for secret resolution
    /// </summary>
    public string? DefaultAwsProfile { get; set; }
}

/// <summary>
/// Result of HIML configuration processing
/// </summary>
public class HimlResult
{
    /// <summary>
    /// The processed configuration data (for single output mode)
    /// </summary>
    public IDictionary<string, object?> Data { get; set; } = new Dictionary<string, object?>();
    
    /// <summary>
    /// Multiple outputs when using same-named file merging mode
    /// Key is the filename (without extension), Value is the merged configuration
    /// </summary>
    public IDictionary<string, IDictionary<string, object?>> MultipleOutputs { get; set; } = new Dictionary<string, IDictionary<string, object?>>();
    
    /// <summary>
    /// Any warnings encountered during processing
    /// </summary>
    public IList<string> Warnings { get; set; } = new List<string>();
    
    /// <summary>
    /// Any errors encountered during processing
    /// </summary>
    public IList<string> Errors { get; set; } = new List<string>();
    
    /// <summary>
    /// Indicates whether the processing was successful (no errors)
    /// </summary>
    public bool IsSuccess => !Errors.Any();
    
    /// <summary>
    /// The formatted output string (for compatibility with tests)
    /// </summary>
    public string? Output { get; set; }
    
    /// <summary>
    /// Multiple formatted outputs when using same-named file merging mode
    /// Key is the filename (without extension), Value is the formatted output
    /// </summary>
    public IDictionary<string, string> MultipleFormattedOutputs { get; set; } = new Dictionary<string, string>();
    
    /// <summary>
    /// First error message (for compatibility with tests)
    /// </summary>
    public string? Error => Errors.FirstOrDefault();
}

/// <summary>
/// Exception thrown when HIML processing fails
/// </summary>
public class HimlException : Exception
{
    public HimlException(string message) : base(message) { }
    public HimlException(string message, Exception innerException) : base(message, innerException) { }
}