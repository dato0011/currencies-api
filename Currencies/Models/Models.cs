namespace Currencies.Models;

using System.Text.Json.Serialization;

public record RefreshTokenRequestModel(string RefreshToken);

public record LoginRequestModel(string Username, string Password);

public record JwtTokenResponseModel(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpirationUtc,
    DateTime RefreshTokenExpirationUtc);

public record ModelWithExpiration
{
    public DateTime Expires { get; init; }
}

public record CurrencyRate : ModelWithExpiration
{
    public required string Base { get; init; }
    public required Dictionary<string, decimal> Rates { get; init; }
    public DateTime Date { get; init; }
}

public record HistoricalRates : ModelWithExpiration
{
    public required string Base { get; init; }
    public required Dictionary<string, Dictionary<string, decimal>> Rates { get; init; }
    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; init; }
    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; init; }
}

public record HistoricalRatesRequest
{
    public required string StartDate { get; set; }
    public required string EndDate { get; set; }
    public string? Base { get; set; }
    public string? Provider { get; set; }
    public string? Symbols { get; set; }
    public int Page { get; set; } = 1;
    public int? PageSize { get; set; }
}

public record CurrencyRateResponseModel
{
    public required string Base { get; init; }
    public required Dictionary<string, decimal> Rates { get; init; }
    public DateTime Date { get; init; }
}

public class HistoricalRatesResponseModel
{
    public required string Base { get; init; }
    public required Dictionary<string, Dictionary<string, decimal>> Rates { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalRecords { get; set; }
}
