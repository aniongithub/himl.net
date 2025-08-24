using Microsoft.Extensions.Logging.Abstractions;

using himl.core;

namespace himl.tests;

/// <summary>
/// Tests based on examples from the Python HIML README
/// </summary>
[TestClass]
public sealed class Examples
{
    private ConfigurationProcessor _processor = null!;
    private string _examplesPath = null!;
    public TestContext TestContext { get; set; } = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = NullLogger<ConfigurationProcessor>.Instance;
        
        // Create dependencies
        var merger = new Services.ConfigurationMerger(NullLogger<Services.ConfigurationMerger>.Instance);
        // Include the VaultSecretResolver (placeholder implementation) so vault interpolations are handled in tests
        var secretResolvers = new List<core.Interfaces.ISecretResolver>
        {
            new Services.SecretResolvers.VaultSecretResolver(NullLogger<Services.SecretResolvers.VaultSecretResolver>.Instance)
        };
        
        var interpolationResolver = new Services.InterpolationResolver(NullLogger<Services.InterpolationResolver>.Instance, secretResolvers);
        var formatter = new Services.OutputFormatter();
        
        _processor = new ConfigurationProcessor(logger, merger, interpolationResolver, formatter, secretResolvers);

        // Examples are copied into the test output; use relative path
        _examplesPath = Path.GetFullPath("examples");
    }

    /// <summary>
    /// Test the simple example from README:
    /// This will merge simple/default.yaml with simple/production/env.yaml
    /// </summary>
    [TestMethod]
    public async Task SimpleExample_ShouldMergeDefaultWithProduction()
    {
        // Arrange
        var options = new HimlOptions
        {
            WorkingDirectory = _examplesPath  // Set working directory to examples folder
        };

        // Act - Pass relative path like the README example shows
        var result = await _processor.ProcessAsync("simple/production", options);

        // Assert
        Assert.AreEqual(0, result.Errors.Count, $"Errors: {string.Join(", ", result.Errors)}");
        
        // Expected result from README:
        // env: prod
        // deep:
        //   key1: v1
        //   key2: v2  
        //   key3: v3
        // deep_list:
        // - item1
        // - item2
        // - item3
        
        Assert.AreEqual("prod", result.Data["env"]);
        
        // Check deep object structure
        Assert.IsTrue(result.Data.ContainsKey("deep"));
        var deep = result.Data["deep"] as IDictionary<string, object?>;
        Assert.IsNotNull(deep);
        
        Assert.AreEqual("v1", deep["key1"]);
        Assert.AreEqual("v2", deep["key2"]);
        Assert.AreEqual("v3", deep["key3"]);
        
        // Check deep list structure
        Assert.IsTrue(result.Data.ContainsKey("deep_list"));
        var deepList = result.Data["deep_list"] as IList<object?>;
        Assert.IsNotNull(deepList);
        Assert.AreEqual(3, deepList.Count);
        Assert.IsTrue(deepList.Contains("item1"));
        Assert.IsTrue(deepList.Contains("item2"));
        Assert.IsTrue(deepList.Contains("item3"));
    }

    /// <summary>
    /// Test the complex example from README:
    /// himl examples/complex/env=dev/region=us-east-1/cluster=cluster2
    /// </summary>
    [TestMethod]
    public async Task ComplexExample_DevUsEast1Cluster2_ShouldMergeCorrectly()
    {
        // Arrange
        // Set USER environment variable for the test
        var originalUser = Environment.GetEnvironmentVariable("USER");
        Environment.SetEnvironmentVariable("USER", "testuser");
        
        try
        {
            var options = new HimlOptions
            {
                WorkingDirectory = _examplesPath  // Set working directory to examples folder
            };

            // Act - Pass relative path like the README example shows
            var result = await _processor.ProcessAsync("complex/env=dev/region=us-east-1/cluster=cluster2", options);

            // Assert
            Assert.AreEqual(0, result.Errors.Count, $"Errors: {string.Join(", ", result.Errors)}");
            
            // Check basic merged values
            Assert.AreEqual("dev", result.Data["env"]);
            Assert.AreEqual("us-east-1", result.Data["region"]); 
            Assert.AreEqual("cluster2", result.Data["cluster"]);
            
            // Check if cluster_info exists (from the actual file structure)
            if (result.Data.ContainsKey("cluster_info"))
            {
                var clusterInfo = result.Data["cluster_info"] as IDictionary<string, object?>;
                Assert.IsNotNull(clusterInfo, "cluster_info should not be null");
                
                // Check if description contains interpolated values
                if (clusterInfo.ContainsKey("description"))
                {
                    var description = clusterInfo["description"]?.ToString();
                    Assert.IsTrue(description?.Contains("cluster2"), $"Description should contain cluster2. Actual: {description}");
                }
                
                if (clusterInfo.ContainsKey("node_type"))
                {
                    Assert.AreEqual("c3.2xlarge", clusterInfo["node_type"]);
                }
            }
            
            // Check environment variable interpolation is working
            Assert.IsTrue(result.Data.ContainsKey("foo"));
            Assert.IsTrue(result.Data["foo"]?.ToString()?.Contains("-bar-baz"));
        }
        finally
        {
            // Restore original USER environment variable
            Environment.SetEnvironmentVariable("USER", originalUser);
        }
    }

    /// <summary>
    /// Test output formatting functionality (YAML vs JSON)
    /// </summary>
    [TestMethod]
    public async Task SimpleExample_OutputFormatting_ShouldWork()
    {
        // Arrange
        var options = new HimlOptions
        {
            WorkingDirectory = _examplesPath  // Set working directory to examples folder
        };

        // Act - Test YAML output
        options.OutputFormat = OutputFormat.Yaml;
        var yamlResult = await _processor.ProcessAsync("simple/production", options);
        
        // Act - Test JSON output
        options.OutputFormat = OutputFormat.Json;
        var jsonResult = await _processor.ProcessAsync("simple/production", options);

        // Assert
        Assert.AreEqual(0, yamlResult.Errors.Count);
        Assert.AreEqual(0, jsonResult.Errors.Count);
        
        // Both should have the same data
        Assert.AreEqual(yamlResult.Data["env"], jsonResult.Data["env"]);
        
        // Output format should be different
        Assert.IsNotNull(yamlResult.Output);
        Assert.IsNotNull(jsonResult.Output);
        Assert.AreNotEqual(yamlResult.Output, jsonResult.Output);
        
        // YAML output should contain YAML-style formatting
        Assert.IsTrue(yamlResult.Output.Contains("env: prod"));
        
        // JSON output should contain JSON-style formatting
        Assert.IsTrue(jsonResult.Output.Contains("\"env\": \"prod\"") || 
                     jsonResult.Output.Contains("\"env\":\"prod\""));
    }

    /// <summary>
    /// Test filtering functionality
    /// </summary>
    [TestMethod]
    public async Task SimpleExample_WithFilters_ShouldReturnOnlySpecifiedKeys()
    {
        // Arrange
        var options = new HimlOptions
        {
            WorkingDirectory = _examplesPath,  // Set working directory to examples folder
            Filters = { "env", "deep" }
        };

        // Act
        var result = await _processor.ProcessAsync("simple/production", options);

        // Assert
        Assert.AreEqual(0, result.Errors.Count, $"Errors: {string.Join(", ", result.Errors)}");
        
        // Should only contain the filtered keys
        Assert.IsTrue(result.Data.ContainsKey("env"));
        Assert.IsTrue(result.Data.ContainsKey("deep"));
        Assert.IsFalse(result.Data.ContainsKey("deep_list"));
    }
}
