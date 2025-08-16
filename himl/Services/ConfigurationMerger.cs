using himl.core;
using himl.core.Interfaces;
using Microsoft.Extensions.Logging;

namespace himl.Services;

/// <summary>
/// Service for merging configuration data with deep merge support
/// </summary>
public class ConfigurationMerger : IConfigurationMerger
{
    private readonly ILogger<ConfigurationMerger> _logger;

    public ConfigurationMerger(ILogger<ConfigurationMerger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Deep merge two configuration objects
    /// </summary>
    public IDictionary<string, object?> Merge(
        IDictionary<string, object?> target, 
        IDictionary<string, object?> source, 
        HimlOptions options)
    {
        var result = new Dictionary<string, object?>(target);

        foreach (var kvp in source)
        {
            var key = kvp.Key;
            var sourceValue = kvp.Value;

            if (!result.ContainsKey(key))
            {
                result[key] = DeepClone(sourceValue);
                continue;
            }

            var targetValue = result[key];
            result[key] = MergeValues(targetValue, sourceValue, options);
        }

        return result;
    }

    private object? MergeValues(object? targetValue, object? sourceValue, HimlOptions options)
    {
        // Handle null values
        if (sourceValue == null)
            return targetValue;
        if (targetValue == null)
            return DeepClone(sourceValue);

        // Both values are dictionaries - merge them
        if (targetValue is IDictionary<string, object?> targetDict && 
            sourceValue is IDictionary<string, object?> sourceDict)
        {
            return options.DictMergeStrategy switch
            {
                DictMergeStrategy.Override => DeepClone(sourceDict),
                DictMergeStrategy.Merge => Merge(targetDict, sourceDict, options),
                _ => throw new ArgumentException($"Unknown dict merge strategy: {options.DictMergeStrategy}")
            };
        }

        // Both values are lists - merge them according to strategy
        if (targetValue is IList<object?> targetList && 
            sourceValue is IList<object?> sourceList)
        {
            return options.ListMergeStrategy switch
            {
                ListMergeStrategy.Override => new List<object?>(sourceList.Select(DeepClone)),
                ListMergeStrategy.Append => MergeListsAppend(targetList, sourceList),
                ListMergeStrategy.Prepend => MergeListsPrepend(targetList, sourceList),
                ListMergeStrategy.AppendUnique => MergeListsAppendUnique(targetList, sourceList),
                _ => throw new ArgumentException($"Unknown list merge strategy: {options.ListMergeStrategy}")
            };
        }

        // Different types or primitive values - source overrides target
        return DeepClone(sourceValue);
    }

    private IList<object?> MergeListsAppend(IList<object?> target, IList<object?> source)
    {
        var result = new List<object?>(target.Select(DeepClone));
        result.AddRange(source.Select(DeepClone));
        return result;
    }

    private IList<object?> MergeListsPrepend(IList<object?> target, IList<object?> source)
    {
        var result = new List<object?>(source.Select(DeepClone));
        result.AddRange(target.Select(DeepClone));
        return result;
    }

    private IList<object?> MergeListsAppendUnique(IList<object?> target, IList<object?> source)
    {
        var result = new List<object?>(target.Select(DeepClone));
        
        foreach (var sourceItem in source)
        {
            // For unique merging, we do a simple equality check
            // This works for primitive types but may need enhancement for complex objects
            if (!result.Any(targetItem => AreEqual(targetItem, sourceItem)))
            {
                result.Add(DeepClone(sourceItem));
            }
        }
        
        return result;
    }

    private bool AreEqual(object? obj1, object? obj2)
    {
        if (obj1 == null && obj2 == null) return true;
        if (obj1 == null || obj2 == null) return false;
        
        // For simple types, use Equals
        if (obj1.GetType().IsPrimitive || obj1 is string || obj2.GetType().IsPrimitive || obj2 is string)
        {
            return obj1.Equals(obj2);
        }
        
        // For complex types, this is a simplified comparison
        // In a production system, you might want to implement deep equality
        return obj1.ToString() == obj2.ToString();
    }

    private object? DeepClone(object? obj)
    {
        if (obj == null) return null;

        return obj switch
        {
            IDictionary<string, object?> dict => CloneDictionary(dict),
            IList<object?> list => CloneList(list),
            _ => obj // Primitive types and strings are immutable
        };
    }

    private IDictionary<string, object?> CloneDictionary(IDictionary<string, object?> source)
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in source)
        {
            result[kvp.Key] = DeepClone(kvp.Value);
        }
        return result;
    }

    private IList<object?> CloneList(IList<object?> source)
    {
        return source.Select(DeepClone).ToList();
    }
}
