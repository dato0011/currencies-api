namespace Currencies.Infrastructure.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Currencies.Infrastructure.Configuration;
using Currencies.Infrastructure.Implementations;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Serilog;

public class JwtTokenFactoryTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly Mock<IOptions<JwtSettings>> _optionsMock;
    private readonly JwtSettings _jwtSettings;

    public JwtTokenFactoryTests()
    {
        _loggerMock = new Mock<ILogger>();
        _optionsMock = new Mock<IOptions<JwtSettings>>();
        _jwtSettings = new JwtSettings
        {
            Key = "ThisIsASecretKeyWithMoreThanEnoughLength1234567890",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessExpireMinutes = 30,
            RefreshExpireDays = 7
        };
        _optionsMock.Setup(o => o.Value).Returns(_jwtSettings);
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var optionsMock = new Mock<IOptions<JwtSettings>>();
        optionsMock.Setup(o => o.Value).Returns((JwtSettings)null!);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new JwtTokenFactory(null!, _loggerMock.Object));
        Assert.Throws<ArgumentNullException>(() => new JwtTokenFactory(optionsMock.Object, _loggerMock.Object));
        Assert.Throws<ArgumentNullException>(() => new JwtTokenFactory(_optionsMock.Object, null!));
    }

    [Theory]
    [InlineData(null, "issuer", "audience")]
    [InlineData("", "issuer", "audience")]
    [InlineData("key", null, "audience")]
    [InlineData("key", "", "audience")]
    [InlineData("key", "issuer", null)]
    [InlineData("key", "issuer", "")]
    public void Constructor_InvalidJwtSettings_ThrowsArgumentException(string? key, string? issuer, string? audience)
    {
        // Arrange
        var settings = new JwtSettings
        {
            Key = key!,
            Issuer = issuer!,
            Audience = audience!,
            AccessExpireMinutes = 30,
            RefreshExpireDays = 7
        };
        _optionsMock.Setup(o => o.Value).Returns(settings);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new JwtTokenFactory(_optionsMock.Object, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_HighAccessExpireMinutes_LogsWarning()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Key = "ThisIsASecretKeyWithMoreThanEnoughLength1234567890",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessExpireMinutes = 60, // Above threshold of 45
            RefreshExpireDays = 7
        };
        _optionsMock.Setup(o => o.Value).Returns(settings);

        // Act
        var factory = new JwtTokenFactory(_optionsMock.Object, _loggerMock.Object);

        // Assert
        _loggerMock.Verify(
            l => l.Warning(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Once());
        _loggerMock.Verify(
            l => l.Warning(
                It.Is<string>(s => s.Contains($"JWT token expiration is set to")),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Once());
    }

    [Fact]
    public void Constructor_HighRefreshExpireDays_LogsWarning()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Key = "ThisIsASecretKeyWithMoreThanEnoughLength1234567890",
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessExpireMinutes = 30,
            RefreshExpireDays = 30 // Above threshold of 14
        };
        _optionsMock.Setup(o => o.Value).Returns(settings);

        // Act
        var factory = new JwtTokenFactory(_optionsMock.Object, _loggerMock.Object);

        // Assert
        _loggerMock.Verify(
            l => l.Warning(
                It.Is<string>(s => s.Contains("JWT refresh token expiration is set to")),
                It.IsAny<int>(),
                It.IsAny<int>()),
            Times.Once());
    }

    [Fact]
    public void CreateToken_ValidClaims_ReturnsJwtToken()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, "TestUser"),
            new Claim(ClaimTypes.Role, "User")
        };
        var factory = new JwtTokenFactory(_optionsMock.Object, _loggerMock.Object);

        // Act
        var result = factory.CreateToken(claims);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.True(result.AccessTokenExpiration > DateTime.UtcNow);
        Assert.True(result.RefreshTokenExpiration > DateTime.UtcNow);

        // Verify the access token
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);
        Assert.Equal(_jwtSettings.Issuer, token.Issuer);
        Assert.Equal(_jwtSettings.Audience, token.Audiences.Single());
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Name && c.Value == "TestUser");
        Assert.Contains(token.Claims, c => c.Type == ClaimTypes.Role && c.Value == "User");
        Assert.True(token.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public void CreateToken_EmptyClaims_ReturnsJwtToken()
    {
        // Arrange
        var claims = new List<Claim>();
        var factory = new JwtTokenFactory(_optionsMock.Object, _loggerMock.Object);

        // Act
        var result = factory.CreateToken(claims);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.True(result.AccessTokenExpiration > DateTime.UtcNow);
        Assert.True(result.RefreshTokenExpiration > DateTime.UtcNow);

        // Verify the access token
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.AccessToken);
        Assert.Equal(_jwtSettings.Issuer, token.Issuer);
        Assert.Equal(_jwtSettings.Audience, token.Audiences.Single());
        Assert.Equal(3, token.Claims.Count());
    }

    [Fact]
    public void CreateToken_NullClaims_ThrowsArgumentNullException()
    {
        // Arrange
        var factory = new JwtTokenFactory(_optionsMock.Object, _loggerMock.Object);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.CreateToken(null!));
    }

    [Fact]
    public void CreateToken_ShortKey_ThrowsSecurityTokenInvalidSignatureException()
    {
        // Arrange
        var settings = new JwtSettings
        {
            Key = "ShortKey", // Too short for HMAC-SHA256
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessExpireMinutes = 30,
            RefreshExpireDays = 7
        };
        _optionsMock.Setup(o => o.Value).Returns(settings);
        var factory = new JwtTokenFactory(_optionsMock.Object, _loggerMock.Object);
        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "TestUser") };

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => factory.CreateToken(claims));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueToken()
    {
        // Arrange
        var factory = new JwtTokenFactory(_optionsMock.Object, _loggerMock.Object);
        var methodInfo = typeof(JwtTokenFactory).GetMethod("GenerateRefreshToken", BindingFlags.NonPublic | BindingFlags.Instance);

        // Act
        var token1 = (string)methodInfo!.Invoke(factory, null)!;
        var token2 = (string)methodInfo!.Invoke(factory, null)!;

        // Assert
        Assert.NotNull(token1);
        Assert.NotNull(token2);
        Assert.NotEqual(token1, token2); // Tokens should be unique
        Assert.False(token1.Contains("=") || token1.Contains("+") || token1.Contains("/"));
        Assert.False(token2.Contains("=") || token2.Contains("+") || token2.Contains("/"));
        Assert.True(token1.Length >= 80); // Approximate length for 64-byte Base64 without padding
    }
}
