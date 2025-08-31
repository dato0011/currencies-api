namespace Currencies.Infrastructure.Tests;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Currencies.Infrastructure;
using Currencies.Infrastructure.Configuration;
using Currencies.Infrastructure.Implementations;
using Currencies.Models;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Serilog;
using Xunit;

public class FrankfurterRateProviderTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<ISimpleCacheProvider> _cacheProviderMock;
    private readonly Mock<IOptions<FrankfurterApiConfig>> _optionsMock;
    private readonly Mock<IApiResiliencePolicies> _resiliencePoliciesMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly FrankfurterApiConfig _config;
    private readonly FrankfurterRateProvider _provider;

    public FrankfurterRateProviderTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _loggerMock = new Mock<ILogger>();
        _cacheProviderMock = new Mock<ISimpleCacheProvider>();
        _optionsMock = new Mock<IOptions<FrankfurterApiConfig>>();
        _resiliencePoliciesMock = new Mock<IApiResiliencePolicies>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

        _config = new FrankfurterApiConfig
        {
            BaseUrl = "https://api.frankfurter.app",
            CacheLatestDurationMinutes = 30,
            CacheHistoricalDurationHours = 24
        };
        _optionsMock.Setup(o => o.Value).Returns(_config);

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri(_config.BaseUrl)
        };
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(_httpClient);

        // Use real Polly policies
        var retryPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(1),
            onRetryAsync: async (outcome, timespan, attempt, context) =>
            {
                await Task.CompletedTask;
            });

        var circuitBreakerPolicy = HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 1,
            durationOfBreak: TimeSpan.FromSeconds(5));

        _resiliencePoliciesMock.Setup(r => r.RetryPolicy).Returns(retryPolicy);
        _resiliencePoliciesMock.Setup(r => r.CircuitBreakerPolicy).Returns(circuitBreakerPolicy);

        _provider = new FrankfurterRateProvider(
            _httpClientFactoryMock.Object,
            _loggerMock.Object,
            _cacheProviderMock.Object,
            _optionsMock.Object,
            _resiliencePoliciesMock.Object);
    }

    [Fact]
    public void Constructor_NullHttpClientFactory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FrankfurterRateProvider(
            null!,
            _loggerMock.Object,
            _cacheProviderMock.Object,
            _optionsMock.Object,
            _resiliencePoliciesMock.Object));
    }

    [Fact]
    public async Task Constructor_NullLogger_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>("httpClientFactory", async () =>
        {
            await Task.FromResult(new FrankfurterRateProvider(
                null!,
                _loggerMock.Object!,
                _cacheProviderMock.Object,
                _optionsMock.Object,
                _resiliencePoliciesMock.Object));
        });
        await Assert.ThrowsAsync<ArgumentNullException>("logger", async () =>
        {
            await Task.FromResult(new FrankfurterRateProvider(
                _httpClientFactoryMock.Object,
                null!,
                _cacheProviderMock.Object,
                _optionsMock.Object,
                _resiliencePoliciesMock.Object));
        });
        await Assert.ThrowsAsync<ArgumentNullException>("cacheProvider", async () =>
        {
            await Task.FromResult(new FrankfurterRateProvider(
                _httpClientFactoryMock.Object,
                _loggerMock.Object,
                null!,
                _optionsMock.Object,
                _resiliencePoliciesMock.Object));
        });
        await Assert.ThrowsAsync<ArgumentNullException>("options", async () =>
        {
            await Task.FromResult(new FrankfurterRateProvider(
                _httpClientFactoryMock.Object,
                _loggerMock.Object,
                _cacheProviderMock.Object,
                null!,
                _resiliencePoliciesMock.Object));
        });
        await Assert.ThrowsAsync<ArgumentNullException>("resiliencePolicies", async () =>
        {
            await Task.FromResult(new FrankfurterRateProvider(
                _httpClientFactoryMock.Object,
                _loggerMock.Object,
                _cacheProviderMock.Object,
                _optionsMock.Object,
                null!));
        });
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new FrankfurterRateProvider(
            _httpClientFactoryMock.Object,
            _loggerMock.Object,
            _cacheProviderMock.Object,
            null!,
            _resiliencePoliciesMock.Object));
    }

    [Fact]
    public async Task GetRatesAsync_CachedData_ReturnsCachedResult()
    {
        // Arrange
        var expected = new CurrencyRate
        {
            Base = "USD",
            Rates = new Dictionary<string, decimal> { { "EUR", 0.85m } },
            Date = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(30)
        };
        _cacheProviderMock.Setup(c => c.GetCachedDataAsync<CurrencyRate>(It.IsAny<string>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _provider.GetRatesAsync("USD", new[] { "EUR" });

        // Assert
        Assert.Equal(expected, result);
        _httpMessageHandlerMock.Protected().Verify<Task<HttpResponseMessage>>(
            "SendAsync", Times.Never(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetRatesAsync_ValidResponse_ReturnsDeserializedResult()
    {
        // Arrange
        var responseData = new CurrencyRate
        {
            Base = "USD",
            Rates = new Dictionary<string, decimal> { { "EUR", 0.85m } },
            Date = DateTime.UtcNow
        };
        var responseJson = JsonSerializer.Serialize(responseData);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        };
        _cacheProviderMock.Setup(c => c.GetCachedDataAsync<CurrencyRate>(It.IsAny<string>()))
            .ReturnsAsync((CurrencyRate)null!);
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act
        var result = await _provider.GetRatesAsync("USD", new[] { "EUR" });

        // Assert
        Assert.Equal(responseData.Base, result.Base);
        Assert.Equal(responseData.Rates, result.Rates);
        _cacheProviderMock.Verify(c => c.SetCacheDataAsync(It.IsAny<string>(), It.IsAny<CurrencyRate>()), Times.Once());
    }

    [Fact]
    public async Task GetRatesAsync_EmptyBaseCurrency_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetRatesAsync("", new[] { "EUR" }));
    }

    [Fact]
    public async Task GetHistoricalRatesAsync_ValidResponse_ReturnsDeserializedResult()
    {
        // Arrange
        var responseData = new HistoricalRates
        {
            Base = "USD",
            Rates = new Dictionary<string, Dictionary<string, decimal>>
            {
                { "2023-01-01", new Dictionary<string, decimal> { { "EUR", 0.85m } } }
            },
            StartDate = new DateTime(2023, 1, 1),
            EndDate = new DateTime(2023, 1, 2)
        };
        var responseJson = JsonSerializer.Serialize(responseData);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        };
        _cacheProviderMock.Setup(c => c.GetCachedDataAsync<HistoricalRates>(It.IsAny<string>()))
            .ReturnsAsync((HistoricalRates)null!);
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act
        var result = await _provider.GetHistoricalRatesAsync(new DateTime(2023, 1, 1), new DateTime(2023, 1, 2), "USD", new[] { "EUR" });

        // Assert
        Assert.Equal(responseData.Base, result.Base);
        Assert.Equal(responseData.Rates, result.Rates);
        _cacheProviderMock.Verify(c => c.SetCacheDataAsync(It.IsAny<string>(), It.IsAny<HistoricalRates>()), Times.Once());
    }

    [Fact]
    public async Task GetHistoricalRatesAsync_FutureStartDate_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetHistoricalRatesAsync(DateTime.UtcNow.AddDays(1), null));
    }

    [Fact]
    public async Task GetHistoricalRatesAsync_EndDateBeforeStartDate_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetHistoricalRatesAsync(
            new DateTime(2023, 1, 2), new DateTime(2023, 1, 1)));
    }

    [Fact]
    public async Task Convert_ValidInput_ReturnsConvertedAmount()
    {
        // Arrange
        var responseData = new CurrencyRate
        {
            Base = "USD",
            Rates = new Dictionary<string, decimal> { { "EUR", 0.85m } },
            Date = DateTime.UtcNow
        };
        var responseJson = JsonSerializer.Serialize(responseData);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        };
        _cacheProviderMock.Setup(c => c.GetCachedDataAsync<CurrencyRate>(It.IsAny<string>()))
            .ReturnsAsync((CurrencyRate)null!);
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act
        var result = await _provider.Convert("USD", "EUR", 100m);

        // Assert
        Assert.Equal(85m, result);
    }

    [Fact]
    public async Task Convert_NegativeAmount_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.Convert("USD", "EUR", -100m));
    }

    [Fact]
    public async Task Convert_NoRateFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var responseData = new CurrencyRate
        {
            Base = "USD",
            Rates = new Dictionary<string, decimal>(),
            Date = DateTime.UtcNow
        };
        var responseJson = JsonSerializer.Serialize(responseData);
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson)
        };
        _cacheProviderMock.Setup(c => c.GetCachedDataAsync<CurrencyRate>(It.IsAny<string>()))
            .ReturnsAsync((CurrencyRate)null!);
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(responseMessage);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.Convert("USD", "EUR", 100m));
    }

    [Fact]
    public async Task QueryApiAsync_CircuitBreakerTriggered_ThrowsBrokenCircuitException()
    {
        // Arrange
        _cacheProviderMock.Setup(c => c.GetCachedDataAsync<CurrencyRate>(It.IsAny<string>()))
            .ReturnsAsync((CurrencyRate)null!);
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable", null, HttpStatusCode.ServiceUnavailable));

        // Act & Assert
        await Assert.ThrowsAsync<BrokenCircuitException>(() => _provider.GetRatesAsync("USD", new[] { "EUR" }));
    }

    [Theory]
    [InlineData(null, null, "")]
    [InlineData("USD", null, "?base=USD")]
    [InlineData(null, new[] { "EUR", "GBP" }, "?symbols=EUR%2CGBP")]
    [InlineData("USD", new[] { "EUR", "GBP" }, "?base=USD&symbols=EUR%2CGBP")]
    public void BuildQueryParameters_VariousInputs_ReturnsCorrectQuery(string? baseCurrency, string[]? symbols, string expected)
    {
        // Act
        var result = FrankfurterRateProviderTests.BuildQueryParameters(baseCurrency, symbols);

        // Assert
        Assert.Equal(expected, result);
    }

    // Helper method to access private static method
    private static string BuildQueryParameters(string? @base, string[]? symbols)
    {
        var methodInfo = typeof(FrankfurterRateProvider).GetMethod("BuildQueryParameters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (string)methodInfo!.Invoke(null, new object?[] { @base, symbols })!;
    }
}
