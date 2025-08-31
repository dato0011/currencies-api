namespace Currencies.Infrastructure.Tests;

using Currencies.Infrastructure.Implementations;
using Microsoft.AspNetCore.Http;
using Moq;
using Moq.Protected;

public class CorrelationHandlerTests
{
    private const string URL = "http://test.com";
    private const string HEADER_CORR_ID = "X-Correlation-ID";
    private const string KEY_CORRELATION_ID = "CorrelationId";

    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly Mock<HttpMessageHandler> _innerHandlerMock;
    private readonly CorrelationHandler _correlationHandler;
    private readonly HttpClient _httpClient;

    public CorrelationHandlerTests()
    {
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _innerHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _correlationHandler = new CorrelationHandler(_httpContextAccessorMock.Object)
        {
            InnerHandler = _innerHandlerMock.Object
        };
        _httpClient = new HttpClient(_correlationHandler);
    }

    [Fact]
    public async Task SendAsync_WhenCorrelationIdExists_AddsCorrelationIdHeader()
    {
        // Arrange
        const string CORRELATION_ID = "test-correlation-id";
        var httpContext = new DefaultHttpContext();
        httpContext.Items[KEY_CORRELATION_ID] = CORRELATION_ID;
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _innerHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage())
            .Verifiable();

        var request = new HttpRequestMessage(HttpMethod.Get, URL);

        // Act
        var response = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.True(request.Headers.Contains(HEADER_CORR_ID));
        Assert.Equal(CORRELATION_ID, request.Headers.GetValues(HEADER_CORR_ID).First());
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Headers.Contains(HEADER_CORR_ID) &&
                                                 req.Headers.GetValues(HEADER_CORR_ID).First() == CORRELATION_ID),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenCorrelationIdIsNull_DoesNotAddHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items[KEY_CORRELATION_ID] = null;
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _innerHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage())
            .Verifiable();

        var request = new HttpRequestMessage(HttpMethod.Get, URL);

        // Act
        var response = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.False(request.Headers.Contains(HEADER_CORR_ID));
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => !req.Headers.Contains(HEADER_CORR_ID)),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenCorrelationIdIsEmpty_DoesNotAddHeader()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        httpContext.Items[KEY_CORRELATION_ID] = "";
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

        _innerHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage())
            .Verifiable();

        var request = new HttpRequestMessage(HttpMethod.Get, URL);

        // Act
        var response = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.False(request.Headers.Contains(HEADER_CORR_ID));
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => !req.Headers.Contains(HEADER_CORR_ID)),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenHttpContextIsNull_DoesNotAddHeader()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);

        _innerHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage())
            .Verifiable();

        var request = new HttpRequestMessage(HttpMethod.Get, URL);

        // Act
        var response = await _httpClient.SendAsync(request, CancellationToken.None);

        // Assert
        Assert.False(request.Headers.Contains(HEADER_CORR_ID));
        _innerHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => !req.Headers.Contains(HEADER_CORR_ID)),
            ItExpr.IsAny<CancellationToken>());
    }
}
