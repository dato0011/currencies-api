using System.Net;
using Currencies.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Polly;
using Polly.CircuitBreaker;
using Serilog;

namespace Currencies.Infrastructure.Tests
{
    public class ApiResiliencePoliciesTests
    {
        private const string OPTIONS_PARAM_NAME = "options";
        private const string OPTIONS_VALUE_PARAM_NAME = "options.Value";
        private const string LOGGER_PARAM_NAME = "logger";
        private const string URL = "http://test.com";

        private readonly Mock<IOptions<FrankfurterApiConfig>> _optionsMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly FrankfurterApiConfig _config;

        public ApiResiliencePoliciesTests()
        {
            _optionsMock = new Mock<IOptions<FrankfurterApiConfig>>(MockBehavior.Strict);
            _loggerMock = new Mock<ILogger>(MockBehavior.Strict);
            _config = new FrankfurterApiConfig
            {
                RetryPolicy = new RetryPolicyConfig { RetryCount = 3, BaseBackoffSeconds = 2 },
                CircuitBreakerPolicy = new CircuitBreakerPolicyConfig { FailuresBeforeBreaking = 5, BreakDurationMinutes = 1 }
            };
            _optionsMock.Setup(o => o.Value).Returns(_config);
        }

        [Fact]
        public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ApiResiliencePolicies(null!, _loggerMock.Object));
            Assert.Equal(OPTIONS_PARAM_NAME, exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenOptionsValueIsNull_ThrowsArgumentNullException()
        {
            // Arrange
            _optionsMock.Setup(o => o.Value).Returns((FrankfurterApiConfig)null!);

            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ApiResiliencePolicies(_optionsMock.Object, _loggerMock.Object));
            Assert.Equal(OPTIONS_VALUE_PARAM_NAME, exception.ParamName);
        }

        [Fact]
        public void Constructor_WhenLoggerIsNull_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new ApiResiliencePolicies(_optionsMock.Object, null!));
            Assert.Equal(LOGGER_PARAM_NAME, exception.ParamName);
        }

        [Theory]
        [InlineData(-1, 2, nameof(FrankfurterApiConfig.RetryPolicy.RetryCount))]
        [InlineData(3, -1, nameof(FrankfurterApiConfig.RetryPolicy.BaseBackoffSeconds))]
        public void Constructor_WhenRetryPolicyConfigIsInvalid_ThrowsArgumentException(int retryCount, int baseBackoffSeconds, string expectedParamName)
        {
            // Arrange
            _config.RetryPolicy = new RetryPolicyConfig { RetryCount = retryCount, BaseBackoffSeconds = baseBackoffSeconds };
            _optionsMock.Setup(o => o.Value).Returns(_config);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new ApiResiliencePolicies(_optionsMock.Object, _loggerMock.Object));
            Assert.Contains(expectedParamName, exception.Message);
        }

        [Theory]
        [InlineData(0, 5, nameof(FrankfurterApiConfig.CircuitBreakerPolicy.BreakDurationMinutes))]
        [InlineData(1, 0, nameof(FrankfurterApiConfig.CircuitBreakerPolicy.FailuresBeforeBreaking))]
        public void Constructor_WhenCircuitBreakerConfigIsInvalid_ThrowsArgumentException(int breakDurationMinutes, int failuresBeforeBreaking, string expectedParamName)
        {
            // Arrange
            _config.CircuitBreakerPolicy = new CircuitBreakerPolicyConfig { BreakDurationMinutes = breakDurationMinutes, FailuresBeforeBreaking = failuresBeforeBreaking };
            _optionsMock.Setup(o => o.Value).Returns(_config);

            // Act & Assert
            var exception = Assert.Throws<ArgumentException>(() => new ApiResiliencePolicies(_optionsMock.Object, _loggerMock.Object));
            Assert.Contains(expectedParamName, exception.Message);
        }

        [Fact]
        public void Constructor_WhenConfigIsValid_InitializesPolicies()
        {
            // Act
            var policies = new ApiResiliencePolicies(_optionsMock.Object, _loggerMock.Object);

            // Assert
            Assert.NotNull(policies.RetryPolicy);
            Assert.NotNull(policies.CircuitBreakerPolicy);
        }

        [Fact]
        public async Task RetryPolicy_ExecutesWithCorrectConfiguration()
        {
            // Arrange
            _loggerMock
                .Setup(l => l.Warning(
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<HttpStatusCode?>(),
                    It.IsAny<string>()))
                .Verifiable();

            var policies = new ApiResiliencePolicies(_optionsMock.Object, _loggerMock.Object);
            var retryPolicy = policies.RetryPolicy;
            var httpClient = new HttpClient(new Mock<HttpMessageHandler>(MockBehavior.Strict).Object);
            var request = new HttpRequestMessage(HttpMethod.Get, URL);

            // Act
            // Simulate a transient failure to trigger retries
            for (int i = 1; i <= _config.RetryPolicy.RetryCount; i++)
            {
                await retryPolicy.ExecuteAsync(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            }

            // Assert
            _loggerMock.Verify(
                l => l.Warning(
                    It.IsAny<string>(),
                    It.Is<int>(attempt => attempt >= 1 && attempt <= _config.RetryPolicy.RetryCount),
                    It.Is<TimeSpan>(ts => ts >= TimeSpan.FromSeconds(Math.Pow(_config.RetryPolicy.BaseBackoffSeconds, 1)) &&
                                          ts <= TimeSpan.FromSeconds(Math.Pow(_config.RetryPolicy.BaseBackoffSeconds, _config.RetryPolicy.RetryCount))),
                    It.Is<HttpStatusCode?>(sc => sc == HttpStatusCode.ServiceUnavailable),
                    It.IsAny<string>()),
                Times.Exactly(_config.RetryPolicy.RetryCount * _config.RetryPolicy.RetryCount));
        }

        [Fact]
        public async Task CircuitBreakerPolicy_ExecutesWithCorrectConfiguration()
        {
            // Arrange
            _loggerMock
                .Setup(l => l.Error(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<HttpStatusCode?>(), It.IsAny<string>()))
                .Verifiable();

            var policies = new ApiResiliencePolicies(_optionsMock.Object, _loggerMock.Object);
            var circuitBreakerPolicy = policies.CircuitBreakerPolicy;

            // Act

            // Simulate failures to trigger circuit breaker
            for (int i = 0; i < _config.CircuitBreakerPolicy.FailuresBeforeBreaking; i++)
            {
                await circuitBreakerPolicy.ExecuteAsync(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            }

            var context = new Context();
            await Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(async () =>
            {
                await circuitBreakerPolicy.ExecuteAsync(
                    (action) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)),
                    context);
            });

            // Assert
            _loggerMock.Verify(
            l => l.Error(
                It.IsAny<string>(),
                It.Is<TimeSpan>(ts => ts == TimeSpan.FromMinutes(_config.CircuitBreakerPolicy.BreakDurationMinutes)),
                It.Is<HttpStatusCode?>(sc => sc == HttpStatusCode.ServiceUnavailable),
                It.IsAny<string>()),
            Times.Once());

            // TODO: Test onReset and onHalfOpen
        }
    }
}
