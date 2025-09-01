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
    private Services.InterpolationResolver _interpolationResolver = null!;
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
        
        _interpolationResolver = interpolationResolver;
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
        // Set test-specific environment variable
        var testVarName = "COMPLEX_TEST_USER";
        var originalUser = Environment.GetEnvironmentVariable(testVarName);
        Environment.SetEnvironmentVariable(testVarName, "testuser");
        
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
            // Restore original environment variable
            Environment.SetEnvironmentVariable(testVarName, originalUser);
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

    /// <summary>
    /// Test environment variable defaults functionality
    /// </summary>
    [TestMethod]
    public async Task EnvironmentVariableDefaults_ShouldUseDefaultsWhenVarNotSet()
    {
        // Arrange - use test-specific environment variable names
        var testUser = "ENVDEFAULTS_TEST_USER";
        var testVar = "ENVDEFAULTS_TEST_VAR_NOT_SET";
        var testPath = "ENVDEFAULTS_TEST_PATH";
        
        var originalUser = Environment.GetEnvironmentVariable(testUser);
        var originalTest = Environment.GetEnvironmentVariable(testVar);
        var originalPath = Environment.GetEnvironmentVariable(testPath);
        
        try
        {
            // Ensure test variables are not set
            Environment.SetEnvironmentVariable(testUser, null);
            Environment.SetEnvironmentVariable(testVar, null);
            // Set a test PATH variable with a known value
            Environment.SetEnvironmentVariable(testPath, "/usr/bin:/bin");
            
            // Create test data with environment variable defaults
            var data = new Dictionary<string, object?>
            {
                ["user_with_default"] = $"{{{{env({testUser}):defaultUser}}}}",
                ["test_with_default"] = $"{{{{env({testVar}):fallbackValue}}}}",
                ["user_without_default"] = $"{{{{env({testUser})}}}}",
                ["existing_var"] = $"{{{{env({testPath}):defaultPath}}}}" // This should exist
            };

            var options = new HimlOptions();

            // Act
            var result = await _interpolationResolver.ResolveAsync(data, options);

            // Assert
            Assert.AreEqual("defaultUser", result["user_with_default"], "Should use default when test user var is not set");
            Assert.AreEqual("fallbackValue", result["test_with_default"], "Should use default when test var is not set");
            Assert.AreEqual("", result["user_without_default"], "Should return empty string when no default provided");
            
            // Test path should exist, so it should use the actual value, not the default
            var pathValue = result["existing_var"]?.ToString();
            Assert.IsNotNull(pathValue, "Test path environment variable should exist");
            Assert.AreNotEqual("defaultPath", pathValue, "Should use actual test path value, not default");
            Assert.AreEqual("/usr/bin:/bin", pathValue, "Should use the set test path value");
        }
        finally
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable(testUser, originalUser);
            Environment.SetEnvironmentVariable(testVar, originalTest);
            Environment.SetEnvironmentVariable(testPath, originalPath);
        }
    }

    [TestMethod]
    public async Task EnvironmentVariableDefaults_ShouldUseActualValueWhenVarIsSet()
    {
        // Arrange - use test-specific environment variable names
        var testUser = "ENVDEFAULTS_ACTUAL_TEST_USER";
        var testVar = "ENVDEFAULTS_ACTUAL_TEST_VAR_SET";
        
        var originalUser = Environment.GetEnvironmentVariable(testUser);
        var originalTest = Environment.GetEnvironmentVariable(testVar);
        
        try
        {
            // Set test-specific environment variables
            Environment.SetEnvironmentVariable(testUser, "actualUser");
            Environment.SetEnvironmentVariable(testVar, "actualValue");
            
            // Create test data with environment variable defaults
            var data = new Dictionary<string, object?>
            {
                ["user_with_default"] = $"{{{{env({testUser}):defaultUser}}}}",
                ["test_with_default"] = $"{{{{env({testVar}):fallbackValue}}}}",
                ["compound"] = $"prefix-{{{{env({testUser}):defaultUser}}}}-suffix"
            };

            var options = new HimlOptions();

            // Act
            var result = await _interpolationResolver.ResolveAsync(data, options);

            // Assert
            Assert.AreEqual("actualUser", result["user_with_default"], "Should use actual value when test user var is set");
            Assert.AreEqual("actualValue", result["test_with_default"], "Should use actual value when test var is set");
            Assert.AreEqual("prefix-actualUser-suffix", result["compound"], "Should use actual value in compound strings");
        }
        finally
        {
            // Restore original environment variables
            Environment.SetEnvironmentVariable(testUser, originalUser);
            Environment.SetEnvironmentVariable(testVar, originalTest);
        }
    }

    /// <summary>
    /// Test same-named file merging mode: merge only same-named YAML files
    /// across the hierarchy to produce multiple outputs per leaf
    /// </summary>
    [TestMethod]
    public async Task SameNamedFileMerging_ShouldProduceMultipleOutputs()
    {
        // Arrange - use the new SameNamed merge mode for multiple files/app configs
        var testRoot = Path.Combine(_examplesPath, "samenamed");
        var options = new HimlOptions
        {
            WorkingDirectory = testRoot,
            MergeMode = MergeMode.SameNamedFiles
        };

        // Act
        var result = await _processor.ProcessAsync(Path.Combine(testRoot, "env=dev/region=us-east-1"), options);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, $"Processing failed with errors: {string.Join(", ", result.Errors)}");
        
        // Should have multiple outputs instead of single Data
        Assert.IsTrue(result.MultipleOutputs.Count > 0, "Should have multiple outputs");
        
        // Should have both app1 and app2 outputs
        Assert.IsTrue(result.MultipleOutputs.ContainsKey("app1"), "Should contain 'app1' output");
        Assert.IsTrue(result.MultipleOutputs.ContainsKey("app2"), "Should contain 'app2' output");
        
        // The app1 output should contain merged data from hierarchy
        var app1Output = result.MultipleOutputs["app1"];
        Assert.IsNotNull(app1Output);
        Assert.IsTrue(app1Output.ContainsKey("env"), "app1 should contain 'env' key");
        Assert.AreEqual("dev", app1Output["env"]);
        Assert.IsTrue(app1Output.ContainsKey("region"), "app1 should contain 'region' key");
        Assert.AreEqual("us-east-1", app1Output["region"]);
        Assert.IsTrue(app1Output.ContainsKey("app_name"), "app1 should contain 'app_name' key");
        Assert.AreEqual("app1", app1Output["app_name"]);
        
        // The app2 output should contain merged data from hierarchy
        var app2Output = result.MultipleOutputs["app2"];
        Assert.IsNotNull(app2Output);
        Assert.IsTrue(app2Output.ContainsKey("env"), "app2 should contain 'env' key");
        Assert.AreEqual("dev", app2Output["env"]);
        Assert.IsTrue(app2Output.ContainsKey("region"), "app2 should contain 'region' key");
        Assert.AreEqual("us-east-1", app2Output["region"]);
        Assert.IsTrue(app2Output.ContainsKey("app_name"), "app2 should contain 'app_name' key");
        Assert.AreEqual("app2", app2Output["app_name"]);
        
        // Should have formatted outputs too
        Assert.IsTrue(result.MultipleFormattedOutputs.Count > 0, "Should have multiple formatted outputs");
        Assert.IsTrue(result.MultipleFormattedOutputs.ContainsKey("app1"), "Should contain 'app1' formatted output");
        Assert.IsTrue(result.MultipleFormattedOutputs.ContainsKey("app2"), "Should contain 'app2' formatted output");
        
        // Verify that the configurations are different between app1 and app2
        var app1Config = app1Output["config"] as IDictionary<string, object?>;
        var app2Config = app2Output["config"] as IDictionary<string, object?>;
        Assert.IsNotNull(app1Config, "app1 should have config");
        Assert.IsNotNull(app2Config, "app2 should have config");
        
        // app1 should have cache config, app2 should have queue config
        Assert.IsTrue(app1Config.ContainsKey("cache"), "app1 should have cache config");
        Assert.IsTrue(app2Config.ContainsKey("queue"), "app2 should have queue config");
    }

    /// <summary>
    /// Test that all-files mode (default) still works as before
    /// </summary>
    [TestMethod]
    public async Task AllFilesMerging_ShouldProduceSingleOutput()
    {
        // Arrange - use the new SameNamed merge mode for multiple files/app configs
        var testRoot = Path.Combine(_examplesPath, "samenamed");
        var options = new HimlOptions
        {
            WorkingDirectory = testRoot,
            MergeMode = MergeMode.AllFiles  // explicit setting, though it's the default
        };

        // Act
        var result = await _processor.ProcessAsync(Path.Combine(testRoot, "env=dev/region=us-east-1"), options);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.IsSuccess, $"Processing failed with errors: {string.Join(", ", result.Errors)}");
        
        // Should have single output in Data (traditional behavior)
        Assert.IsTrue(result.Data.Count > 0, "Should have data in single output");
        Assert.IsNotNull(result.Output, "Should have formatted output");
        
        // Should NOT have multiple outputs
        Assert.AreEqual(0, result.MultipleOutputs.Count, "Should not have multiple outputs");
        Assert.AreEqual(0, result.MultipleFormattedOutputs.Count, "Should not have multiple formatted outputs");
        
        // Verify the single output contains expected merged data from both files
        Assert.IsTrue(result.Data.ContainsKey("env"), "Should contain 'env' key");
        Assert.AreEqual("dev", result.Data["env"]);
        Assert.IsTrue(result.Data.ContainsKey("region"), "Should contain 'region' key");
        Assert.AreEqual("us-east-1", result.Data["region"]);
        
        // In all-files mode, later files override earlier ones, so we should see app2 values for conflicting keys
        Assert.IsTrue(result.Data.ContainsKey("app_name"), "Should contain 'app_name' key");
        Assert.AreEqual("app2", result.Data["app_name"], "app2 should override app1 in all-files mode");
        Assert.IsTrue(result.Data.ContainsKey("app_type"), "Should contain 'app_type' key");
        Assert.AreEqual("api", result.Data["app_type"], "Should have app2's app_type");
        
        // Should have merged config from both apps
        var config = result.Data["config"] as IDictionary<string, object?>;
        Assert.IsNotNull(config, "Should have merged config");
        
        // Should have both cache (from app1) and queue (from app2) since they don't conflict
        Assert.IsTrue(config.ContainsKey("cache"), "Should contain cache config from app1");
        Assert.IsTrue(config.ContainsKey("queue"), "Should contain queue config from app2");
    }
}
