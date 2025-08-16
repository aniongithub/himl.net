using himl.core;
using himl.core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace himl.Services;

/// <summary>
/// Service for resolving interpolations in configuration values
/// </summary>
public class InterpolationResolver : IInterpolationResolver
{
    private readonly ILogger<InterpolationResolver> _logger;
    private readonly IEnumerable<ISecretResolver> _secretResolvers;
    private static readonly Regex InterpolationRegex = new(@"\{\{([^}]+)\}\}", RegexOptions.Compiled);
    private static readonly Regex EscapedInterpolationRegex = new(@"\{\{`([^`]+)`\}\}", RegexOptions.Compiled);
    private static readonly Regex EnvVarRegex = new(@"env\(([^)]+)\)", RegexOptions.Compiled);

    public InterpolationResolver(
        ILogger<InterpolationResolver> logger,
        IEnumerable<ISecretResolver> secretResolvers)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _secretResolvers = secretResolvers ?? throw new ArgumentNullException(nameof(secretResolvers));
    }

    /// <summary>
    /// Resolve interpolations in configuration data
    /// </summary>
    public async Task<IDictionary<string, object?>> ResolveAsync(
        IDictionary<string, object?> data, 
        HimlOptions options)
    {
        var result = new Dictionary<string, object?>();

        // First pass: resolve dictionary interpolations
        foreach (var kvp in data)
        {
            result[kvp.Key] = await ResolveValue(kvp.Value, data, options);
        }

        // Multiple passes for nested interpolations
        var maxIterations = 10;
        var iteration = 0;
        bool hasChanges;

        do
        {
            hasChanges = false;
            iteration++;

            var newResult = new Dictionary<string, object?>();
            foreach (var kvp in result)
            {
                var newValue = await ResolveValue(kvp.Value, result, options);
                newResult[kvp.Key] = newValue;
                
                if (!AreEqual(newValue, kvp.Value))
                {
                    hasChanges = true;
                }
            }
            result = newResult;

        } while (hasChanges && iteration < maxIterations);

        if (iteration >= maxIterations)
        {
            _logger.LogWarning("Maximum interpolation iterations reached. Some interpolations may remain unresolved.");
        }

        // Clean up escaped interpolations
        CleanEscapedInterpolations(result);

        return result;
    }

    private async Task<object?> ResolveValue(object? value, IDictionary<string, object?> data, HimlOptions options)
    {
        return value switch
        {
            string str => await ResolveStringValue(str, data, options),
            IDictionary<string, object?> dict => await ResolveDictionary(dict, data, options),
            IList<object?> list => await ResolveList(list, data, options),
            _ => value
        };
    }

    private async Task<string> ResolveStringValue(string value, IDictionary<string, object?> data, HimlOptions options)
    {
        // Skip escaped interpolations
        if (EscapedInterpolationRegex.IsMatch(value))
        {
            return value;
        }

        // Check if the entire string is an interpolation (for object replacement)
        if (value.StartsWith("{{") && value.EndsWith("}}") && value.Count(c => c == '{') == 2)
        {
            var interpolationContent = value.Substring(2, value.Length - 4).Trim();
            var resolved = await ResolveInterpolation(interpolationContent, data, options);
            
            // If resolved to an object, return it as-is (this allows object interpolation)
            if (resolved != value.Substring(2, value.Length - 4).Trim())
            {
                return resolved;
            }
        }

        // Replace all interpolations in the string
        var result = value;
        var matches = InterpolationRegex.Matches(value);
        
        foreach (Match match in matches)
        {
            var interpolationContent = match.Groups[1].Value.Trim();
            var resolved = await ResolveInterpolation(interpolationContent, data, options);
            result = result.Replace(match.Value, resolved);
        }

        return result;
    }

    private async Task<string> ResolveInterpolation(string interpolation, IDictionary<string, object?> data, HimlOptions options)
    {
        // Environment variables: env(VAR_NAME)
        var envMatch = EnvVarRegex.Match(interpolation);
        if (envMatch.Success)
        {
            var envVarName = envMatch.Groups[1].Value;
            var envValue = Environment.GetEnvironmentVariable(envVarName);
            return envValue ?? "";
        }

        // Secret resolution: ssm.path(...).aws_profile(...)
        if (!options.SkipSecrets && IsSecretInterpolation(interpolation))
        {
            return await ResolveSecretInterpolation(interpolation);
        }

        // Dictionary path resolution: some.nested.key
        return ResolveDictionaryPath(interpolation, data);
    }

    private bool IsSecretInterpolation(string interpolation)
    {
        var secretTypes = new[] { "ssm.", "s3.", "vault." };
        return secretTypes.Any(type => interpolation.StartsWith(type));
    }

    private async Task<string> ResolveSecretInterpolation(string interpolation)
    {
        try
        {
            // Parse secret interpolation: type.method(value).option(value)
            var parts = interpolation.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return interpolation;

            var secretType = parts[0];
            var parameters = new Dictionary<string, string>();

            foreach (var part in parts.Skip(1))
            {
                var parenIndex = part.IndexOf('(');
                if (parenIndex > 0 && part.EndsWith(')'))
                {
                    var key = part.Substring(0, parenIndex);
                    var value = part.Substring(parenIndex + 1, part.Length - parenIndex - 2);
                    parameters[key] = value;
                }
            }

            var resolver = _secretResolvers.FirstOrDefault(r => r.Supports(secretType));
            if (resolver != null)
            {
                return await resolver.ResolveAsync(secretType, parameters);
            }

            _logger.LogWarning("No secret resolver found for type: {SecretType}", secretType);
            return interpolation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve secret interpolation: {Interpolation}", interpolation);
            return interpolation;
        }
    }

    private string ResolveDictionaryPath(string path, IDictionary<string, object?> data)
    {
        var parts = path.Split('.');
        object? current = data;

        foreach (var part in parts)
        {
            if (current is IDictionary<string, object?> dict && dict.TryGetValue(part, out var value))
            {
                current = value;
            }
            else
            {
                _logger.LogDebug("Could not resolve path: {Path}", path);
                return path; // Return original if not found
            }
        }

        return current?.ToString() ?? "";
    }

    private async Task<IDictionary<string, object?>> ResolveDictionary(
        IDictionary<string, object?> dict, 
        IDictionary<string, object?> data, 
        HimlOptions options)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in dict)
        {
            result[kvp.Key] = await ResolveValue(kvp.Value, data, options);
        }
        return result;
    }

    private async Task<IList<object?>> ResolveList(
        IList<object?> list, 
        IDictionary<string, object?> data, 
        HimlOptions options)
    {
        var result = new List<object?>();
        foreach (var item in list)
        {
            result.Add(await ResolveValue(item, data, options));
        }
        return result;
    }

    private void CleanEscapedInterpolations(IDictionary<string, object?> data)
    {
        foreach (var kvp in data.ToList())
        {
            data[kvp.Key] = CleanEscapedValue(kvp.Value);
        }
    }

    private object? CleanEscapedValue(object? value)
    {
        return value switch
        {
            string str => EscapedInterpolationRegex.Replace(str, "{{$1}}"),
            IDictionary<string, object?> dict => CleanEscapedDictionary(dict),
            IList<object?> list => CleanEscapedList(list),
            _ => value
        };
    }

    private IDictionary<string, object?> CleanEscapedDictionary(IDictionary<string, object?> dict)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in dict)
        {
            result[kvp.Key] = CleanEscapedValue(kvp.Value);
        }
        return result;
    }

    private IList<object?> CleanEscapedList(IList<object?> list)
    {
        return list.Select(CleanEscapedValue).ToList();
    }

    private bool AreEqual(object? obj1, object? obj2)
    {
        if (obj1 == null && obj2 == null) return true;
        if (obj1 == null || obj2 == null) return false;
        return obj1.Equals(obj2);
    }
}
