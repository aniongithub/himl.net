namespace himl.core.Utils;

/// <summary>
/// Utility for working with directory hierarchies
/// </summary>
public static class DirectoryHierarchy
{
    /// <summary>
    /// Generate a list of directories from root to the specified path for configuration merging
    /// </summary>
    /// <param name="path">Target path</param>
    /// <param name="workingDirectory">Working directory to resolve relative paths</param>
    /// <returns>List of directories in hierarchy order (root first)</returns>
    public static IList<string> GenerateHierarchy(string path, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", nameof(path));

        // Resolve relative path
        var resolvedPath = ResolvePath(path, workingDirectory);
        
        if (!Directory.Exists(resolvedPath))
            throw new DirectoryNotFoundException($"Directory not found: {resolvedPath}");

        var hierarchy = new List<string>();
        var currentPath = resolvedPath;

        // Walk up the directory tree to find the root
        while (!string.IsNullOrEmpty(currentPath))
        {
            if (HasConfigurationFiles(currentPath))
            {
                hierarchy.Insert(0, currentPath); // Insert at beginning to maintain root-first order
            }
            
            var parent = Directory.GetParent(currentPath)?.FullName;
            if (parent == currentPath) // Reached filesystem root
                break;
                
            currentPath = parent;
        }

        return hierarchy;
    }

    /// <summary>
    /// Check if a directory contains YAML configuration files
    /// </summary>
    /// <param name="directory">Directory to check</param>
    /// <returns>True if configuration files are found</returns>
    public static bool HasConfigurationFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return false;

        var yamlFiles = Directory.GetFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(directory, "*.yml", SearchOption.TopDirectoryOnly));

        return yamlFiles.Any();
    }

    /// <summary>
    /// Get all YAML files in a directory
    /// </summary>
    /// <param name="directory">Directory to search</param>
    /// <returns>List of YAML file paths</returns>
    public static IList<string> GetConfigurationFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return new List<string>();

        var yamlFiles = Directory.GetFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(directory, "*.yml", SearchOption.TopDirectoryOnly))
            .OrderBy(f => f)
            .ToList();

        return yamlFiles;
    }

    /// <summary>
    /// Extract values from directory path components (e.g., env=prod, region=us-east-1)
    /// </summary>
    /// <param name="path">Directory path</param>
    /// <returns>Dictionary of key-value pairs extracted from path</returns>
    public static IDictionary<string, string> ExtractValuesFromPath(string path)
    {
        var values = new Dictionary<string, string>();
        
        if (string.IsNullOrWhiteSpace(path))
            return values;

        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, 
            StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            var equalsIndex = segment.IndexOf('=');
            if (equalsIndex > 0 && equalsIndex < segment.Length - 1)
            {
                var key = segment.Substring(0, equalsIndex);
                var value = segment.Substring(equalsIndex + 1);
                values[key] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// Resolve a path relative to a working directory
    /// </summary>
    /// <param name="path">Path to resolve</param>
    /// <param name="workingDirectory">Working directory</param>
    /// <returns>Resolved absolute path</returns>
    private static string ResolvePath(string path, string? workingDirectory)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        var baseDirectory = workingDirectory ?? Environment.CurrentDirectory;
        return Path.GetFullPath(Path.Combine(baseDirectory, path));
    }
}
