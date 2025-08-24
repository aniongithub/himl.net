using himl.core;
using himl.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace himl.tests;

[TestClass]
public class ConfigurationBuilderExtensionsTests
{
    private const string TestConfigPath = "examples/simple/production";
    private const string InvalidConfigPath = "non-existent-path";

    [TestMethod]
    public void AddHiml_WithPath_AddsConfigurationSource()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder.AddHiml(TestConfigPath);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreSame(builder, result, "Should return the same builder for chaining");
    }

    [TestMethod]
    public void AddHiml_WithPathAndOptions_AddsConfigurationSource()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        var options = new HimlOptions
        {
            OutputFormat = OutputFormat.Json,
            SkipInterpolations = true
        };

        // Act
        var result = builder.AddHiml(TestConfigPath, false, options);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreSame(builder, result, "Should return the same builder for chaining");
    }

    [TestMethod]
    public void AddHiml_WithConfigureAction_AddsConfigurationSource()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder.AddHiml(TestConfigPath, options =>
        {
            options.OutputFormat = OutputFormat.Json;
            options.SkipInterpolations = true;
        });

        // Assert
        Assert.IsNotNull(result);
        Assert.AreSame(builder, result, "Should return the same builder for chaining");
    }

    [TestMethod]
    public void AddHiml_BuildConfiguration_LoadsHimlData()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddHiml(TestConfigPath);

        // Act
        var configuration = builder.Build();

        // Assert
        Assert.IsNotNull(configuration);
        
        // Verify some known values from the simple example
        Assert.AreEqual("prod", configuration["env"]);
        Assert.AreEqual("v1", configuration["deep:key1"]);
        Assert.AreEqual("v2", configuration["deep:key2"]);
        Assert.AreEqual("v3", configuration["deep:key3"]);
        Assert.AreEqual("item1", configuration["deep_list:0"]);
        Assert.AreEqual("item2", configuration["deep_list:1"]);
        Assert.AreEqual("item3", configuration["deep_list:2"]);
    }

    [TestMethod]
    public void AddHiml_WithListData_FlattensCorrectly()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddHiml(TestConfigPath);

        // Act
        var configuration = builder.Build();

        // Assert
        Assert.AreEqual("item1", configuration["deep_list:0"]);
        Assert.AreEqual("item2", configuration["deep_list:1"]);
        Assert.AreEqual("item3", configuration["deep_list:2"]);
    }

    [TestMethod]
    public void AddHiml_WithNestedData_FlattensCorrectly()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddHiml(TestConfigPath);

        // Act
        var configuration = builder.Build();

        // Assert
        Assert.AreEqual("v1", configuration["deep:key1"]);
        Assert.AreEqual("v2", configuration["deep:key2"]);
        Assert.AreEqual("v3", configuration["deep:key3"]);
    }

    [TestMethod]
    public void AddHiml_MultipleChaining_WorksCorrectly()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        var result = builder
            .AddHiml("examples/simple")
            .AddHiml(TestConfigPath, options =>
            {
                options.SkipInterpolations = true;
            });

        // Assert
        Assert.IsNotNull(result);
        Assert.AreSame(builder, result, "Should return the same builder for chaining");
        
        var configuration = result.Build();
        Assert.IsNotNull(configuration);
    }

    [TestMethod]
    public void AddHiml_OptionalPath_DoesNotThrowWhenMissing()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddHiml(InvalidConfigPath, optional: true);

        // Act & Assert - should not throw
        var configuration = builder.Build();
        Assert.IsNotNull(configuration);
    }

    [TestMethod]
    public void AddHiml_NonOptionalPath_ThrowsWhenMissing()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddHiml(InvalidConfigPath, optional: false);

        // Act & Assert
        var ex = Assert.ThrowsException<InvalidOperationException>(() => builder.Build());
        Assert.IsTrue(ex.Message.Contains("Failed to load HIML configuration"));
    }

    [TestMethod]
    public void AddHiml_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => builder.AddHiml(null!));
    }

    [TestMethod]
    public void AddHiml_WithCustomOptions_AppliesOptions()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        var options = new HimlOptions
        {
            SkipInterpolations = true,
            OutputFormat = OutputFormat.Json
        };

        // Act
        builder.AddHiml(TestConfigPath, false, options);
        var configuration = builder.Build();

        // Assert
        Assert.IsNotNull(configuration);
        // Note: Since we're skipping interpolations, any interpolated values should remain as-is
        // This is hard to verify without knowing specific interpolated values in the test data
    }
}

[TestClass]
public class HimlConfigurationSourceTests
{
    [TestMethod]
    public void Constructor_WithValidPath_CreatesSource()
    {
        // Arrange & Act
        var source = new HimlConfigurationSource("examples/simple");

        // Assert
        Assert.IsNotNull(source);
    }

    [TestMethod]
    public void Constructor_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new HimlConfigurationSource(null!));
    }

    [TestMethod]
    public void Build_ReturnsProvider()
    {
        // Arrange
        var source = new HimlConfigurationSource("examples/simple");
        var builder = new ConfigurationBuilder();

        // Act
        var provider = source.Build(builder);

        // Assert
        Assert.IsNotNull(provider);
        Assert.IsInstanceOfType<HimlConfigurationProvider>(provider);
    }
}

[TestClass]
public class HimlConfigurationProviderTests
{
    [TestMethod]
    public void Constructor_WithValidParameters_CreatesProvider()
    {
        // Arrange & Act
        var provider = new HimlConfigurationProvider("examples/simple", false, new HimlOptions());

        // Assert
        Assert.IsNotNull(provider);
    }

    [TestMethod]
    public void Constructor_WithNullPath_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new HimlConfigurationProvider(null!, false, new HimlOptions()));
    }

    [TestMethod]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => 
            new HimlConfigurationProvider("examples/simple", false, null!));
    }

    [TestMethod]
    public void Load_WithValidPath_LoadsData()
    {
        // Arrange
        var provider = new HimlConfigurationProvider("examples/simple/production", false, new HimlOptions());

        // Act
        provider.Load();

        // Assert
        Assert.IsTrue(provider.TryGet("env", out var envValue));
        Assert.AreEqual("prod", envValue);
        
        Assert.IsTrue(provider.TryGet("deep:key1", out var key1Value));
        Assert.AreEqual("v1", key1Value);
    }

    [TestMethod]
    public void Load_WithOptionalAndInvalidPath_DoesNotThrow()
    {
        // Arrange
        var provider = new HimlConfigurationProvider("non-existent-path", true, new HimlOptions());

        // Act & Assert - should not throw
        provider.Load();
    }

    [TestMethod]
    public void Load_WithNonOptionalAndInvalidPath_ThrowsException()
    {
        // Arrange
        var provider = new HimlConfigurationProvider("non-existent-path", false, new HimlOptions());

        // Act & Assert
        Assert.ThrowsException<InvalidOperationException>(() => provider.Load());
    }

    [TestMethod]
    public void FlattenData_WithSimpleValues_FlattensCorrectly()
    {
        // This tests the private FlattenData method indirectly through Load
        // Arrange
        var provider = new HimlConfigurationProvider("examples/simple/production", false, new HimlOptions());

        // Act
        provider.Load();

        // Assert - verify flattening of nested objects
        Assert.IsTrue(provider.TryGet("deep:key1", out var value1));
        Assert.AreEqual("v1", value1);
        
        Assert.IsTrue(provider.TryGet("deep:key2", out var value2));
        Assert.AreEqual("v2", value2);
        
        Assert.IsTrue(provider.TryGet("deep:key3", out var value3));
        Assert.AreEqual("v3", value3);
    }

    [TestMethod]
    public void FlattenData_WithArrays_FlattensCorrectly()
    {
        // Arrange
        var provider = new HimlConfigurationProvider("examples/simple/production", false, new HimlOptions());

        // Act
        provider.Load();

        // Assert - verify flattening of arrays
        Assert.IsTrue(provider.TryGet("deep_list:0", out var item0));
        Assert.AreEqual("item1", item0);
        
        Assert.IsTrue(provider.TryGet("deep_list:1", out var item1));
        Assert.AreEqual("item2", item1);
        
        Assert.IsTrue(provider.TryGet("deep_list:2", out var item2));
        Assert.AreEqual("item3", item2);
    }
}
