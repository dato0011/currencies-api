namespace Currencies.Infrastructure.Configuration;

public class JwtSettings
{
    public required string Key { get; set; }
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required int AccessExpireMinutes { get; set; }
    public required int RefreshExpireDays { get; set; }
}