namespace Currencies.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Request payload used to obtain a new access token via a previously issued refresh token.
/// </summary>
/// <param name="RefreshToken">
/// The opaque refresh token previously issued by the authorization server.
/// </param>
public record RefreshTokenRequestModel(string RefreshToken);

/// <summary>
/// Request payload that carries user credentials for an authentication attempt.
/// </summary>
/// <param name="Username">The unique username or login identifier.</param>
/// <param name="Password">The user's clear-text password to be validated by the auth service.</param>
public record LoginRequestModel(string Username, string Password);

/// <summary>
/// Response payload returned after a successful authentication or token refresh.
/// </summary>
/// <param name="AccessToken">
/// The short‑lived JWT access token used to authorize API requests.
/// </param>
/// <param name="RefreshToken">
/// The long‑lived token that can be exchanged for a new access token when the current one expires.
/// </param>
/// <param name="AccessTokenExpirationUtc">
/// UTC timestamp indicating when the <see cref="AccessToken"/> expires.
/// </param>
/// <param name="RefreshTokenExpirationUtc">
/// UTC timestamp indicating when the <see cref="RefreshToken"/> expires.
/// </param>
public record JwtTokenResponseModel(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpirationUtc,
    DateTime RefreshTokenExpirationUtc);

/// <summary>
/// Base model that adds a universal expiration timestamp to derived payloads.
/// </summary>
public record ModelWithExpiration
{
    /// <summary>
    /// The UTC timestamp indicating when the model data becomes stale or invalid.
    /// </summary>
    public DateTime Expires { get; init; }
}

/// <summary>
/// Represents latest currency exchange rates for a given base currency,
/// optionally augmented with an expiration time via <see cref="ModelWithExpiration"/>.
/// </summary>
public record CurrencyRate : ModelWithExpiration
{
    /// <summary>
    /// The ISO 4217 code of the base currency (e.g., "USD").
    /// </summary>
    public required string Base { get; init; }

    /// <summary>
    /// A map of currency ISO codes to their decimal exchange rate relative to <see cref="Base"/>.
    /// </summary>
    public required Dictionary<string, decimal> Rates { get; init; }

    /// <summary>
    /// The effective date (in UTC) for which the <see cref="Rates"/> were calculated.
    /// </summary>
    public DateTime Date { get; init; }
}

/// <summary>
/// Represents historical currency exchange rates over a date range for a base currency.
/// </summary>
public record HistoricalRates : ModelWithExpiration
{
    /// <summary>
    /// The ISO 4217 code of the base currency (e.g., "EUR").
    /// </summary>
    public required string Base { get; init; }

    /// <summary>
    /// A nested map where the first key is the ISO currency code and the second key is the date (yyyy-MM-dd),
    /// mapping to the decimal exchange rate for that date.
    /// </summary>
    public required Dictionary<string, Dictionary<string, decimal>> Rates { get; init; }

    /// <summary>
    /// The inclusive start date (UTC) of the historical range.
    /// Serialized as <c>start_date</c> for wire compatibility.
    /// </summary>
    [JsonPropertyName("start_date")]
    public DateTime StartDate { get; init; }

    /// <summary>
    /// The inclusive end date (UTC) of the historical range.
    /// Serialized as <c>end_date</c> for wire compatibility.
    /// </summary>
    [JsonPropertyName("end_date")]
    public DateTime EndDate { get; init; }
}

/// <summary>
/// Request parameters used to query paginated historical exchange rates.
/// </summary>
public record HistoricalRatesRequest
{
    /// <summary>
    /// Inclusive start date of the period to query in <c>yyyy-MM-dd</c> format.
    /// </summary>
    public required string StartDate { get; set; }

    /// <summary>
    /// Inclusive end date of the period to query in <c>yyyy-MM-dd</c> format.
    /// </summary>
    public required string EndDate { get; set; }

    /// <summary>
    /// Optional base currency ISO 4217 code (defaults to the provider's default base if omitted).
    /// </summary>
    public string? Base { get; set; }

    /// <summary>
    /// Optional data provider identifier if multiple backends are supported.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Optional comma-separated list of target currency ISO codes to filter results (e.g., "USD,EUR,GBP").
    /// </summary>
    public string? Symbols { get; set; }

    /// <summary>
    /// 1-based page number to return. Defaults to 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Optional page size (number of records per page). When null, a server default is used.
    /// </summary>
    public int? PageSize { get; set; }
}

/// <summary>
/// Wire response model for a single day's currency rates.
/// </summary>
public record CurrencyRateResponseModel
{
    /// <summary>
    /// The ISO 4217 code of the base currency for the response.
    /// </summary>
    public required string Base { get; init; }

    /// <summary>
    /// A map of currency ISO codes to their decimal exchange rate relative to <see cref="Base"/>.
    /// </summary>
    public required Dictionary<string, decimal> Rates { get; init; }

    /// <summary>
    /// The effective date (UTC) for which the <see cref="Rates"/> apply.
    /// </summary>
    public DateTime Date { get; init; }
}

/// <summary>
/// Wire response model for paginated historical currency rates,
/// containing both the data and pagination metadata.
/// </summary>
public class HistoricalRatesResponseModel
{
    /// <summary>
    /// The ISO 4217 code of the base currency for the response.
    /// </summary>
    public required string Base { get; init; }

    /// <summary>
    /// A nested map where the first key is the ISO currency code and the second key is the date (yyyy-MM-dd),
    /// mapping to the decimal exchange rate for that date.
    /// </summary>
    public required Dictionary<string, Dictionary<string, decimal>> Rates { get; init; }

    /// <summary>
    /// The inclusive start date (UTC) of the historical range represented in the response.
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// The inclusive end date (UTC) of the historical range represented in the response.
    /// </summary>
    public DateTime EndDate { get; init; }

    /// <summary>
    /// The current page index (1-based) of the paginated result set.
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// The number of records returned per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// The total number of pages available for the query.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// The total number of records available for the query across all pages.
    /// </summary>
    public int TotalRecords { get; set; }
}
