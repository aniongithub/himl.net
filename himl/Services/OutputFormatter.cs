using himl.core.Interfaces;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace himl.Services;

/// <summary>
/// Service for formatting configuration output
/// </summary>
public class OutputFormatter : IOutputFormatter
{
    /// <summary>
    /// Format data as YAML
    /// </summary>
    public string ToYaml(IDictionary<string, object?> data, bool multiLineString = false)
    {
        var serializerBuilder = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance);

        if (multiLineString)
        {
            serializerBuilder = serializerBuilder.WithDefaultScalarStyle(YamlDotNet.Core.ScalarStyle.Literal);
        }

        var serializer = serializerBuilder.Build();
        return serializer.Serialize(data);
    }

    /// <summary>
    /// Format data as JSON
    /// </summary>
    public string ToJson(IDictionary<string, object?> data, JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(data, options);
    }
}
