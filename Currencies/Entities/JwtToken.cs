public record JwtToken
{
    public required string AccessToken { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime AccessTokenExpiration { get; init; }
    public required DateTime RefreshTokenExpiration { get; init; }
}
