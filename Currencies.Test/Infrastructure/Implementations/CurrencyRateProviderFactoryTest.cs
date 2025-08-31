namespace Currencies.Infrastructure.Tests;

using Currencies.Infrastructure.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public class CurrencyRateProviderFactoryTests
{
    private const string PROVIDER_NAME_KEY = "providerName";

    private readonly IServiceProvider _serviceProvider;
    private readonly CurrencyRateProviderFactory _factory;

    public CurrencyRateProviderFactoryTests()
    {
        var services = new ServiceCollection();
        _serviceProvider = services.BuildServiceProvider();
        _factory = new CurrencyRateProviderFactory(_serviceProvider);
    }

    [Fact]
    public void Constructor_WhenServiceProviderIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new CurrencyRateProviderFactory(null!));
        Assert.Equal("serviceProvider", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenServiceProviderIsValid_InitializesProviders()
    {
        // Arrange
        var expectedProviders = new HashSet<string> { Constants.ProviderFrankfurter };

        // Act
        var factory = new CurrencyRateProviderFactory(_serviceProvider);

        // Assert
        Assert.NotNull(factory.AvailableProviders);
        Assert.Equal(expectedProviders, factory.AvailableProviders);
    }

    [Fact]
    public void AvailableProviders_ReturnsReadOnlySetOfProviders()
    {
        // Arrange
        var expectedProviders = new HashSet<string> { Constants.ProviderFrankfurter };

        // Act
        IReadOnlySet<string> providers = _factory.AvailableProviders;

        // Assert
        Assert.NotNull(providers);
        Assert.Equal(expectedProviders, providers);
    }

    [Fact]
    public void CreateProvider_WhenProviderNameIsValid_ReturnsProviderInstance()
    {
        // Arrange
        var services = new ServiceCollection();
        var providerMock = new Mock<ICurrencyRateProvider>();
        services.AddKeyedSingleton<ICurrencyRateProvider>(Constants.ProviderFrankfurter, providerMock.Object);
        var serviceProvider = services.BuildServiceProvider();
        var factory = new CurrencyRateProviderFactory(serviceProvider);

        // Act
        var provider = factory.CreateProvider(Constants.ProviderFrankfurter);

        // Assert
        Assert.NotNull(provider);
        Assert.Equal(providerMock.Object, provider);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateProvider_WhenProviderNameIsEmptyOrWhitespace_ThrowsArgumentException(string? providerName)
    {
        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateProvider(providerName!));
        Assert.Equal(PROVIDER_NAME_KEY, exception.ParamName);
        Assert.StartsWith("The value cannot be an empty string or composed entirely of whitespace.", exception.Message);
    }

    [Fact]
    public void CreateProvider_WhenProviderNameIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _factory.CreateProvider(null!));
        Assert.Equal(PROVIDER_NAME_KEY, exception.ParamName);
        Assert.StartsWith("Value cannot be null. (Parameter 'providerName')", exception.Message);
    }

    [Fact]
    public void CreateProvider_WhenProviderNameIsInvalid_ThrowsArgumentException()
    {
        // Arrange
        var invalidProviderName = "invalid-provider";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _factory.CreateProvider(invalidProviderName));
        Assert.Equal(PROVIDER_NAME_KEY, exception.ParamName);
        Assert.Equal($"No provider found with name '{invalidProviderName}'. (Parameter 'providerName')", exception.Message);
    }

    [Fact]
    public void CreateProvider_WhenProviderNotResolved_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        // No provider registered for PROVIDER_FRANKFURTER
        var serviceProvider = services.BuildServiceProvider();
        var factory = new CurrencyRateProviderFactory(serviceProvider);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateProvider(Constants.ProviderFrankfurter));
        Assert.Equal($"Could not create an instance of '{Constants.ProviderFrankfurter}' provider.", exception.Message);
    }
}
