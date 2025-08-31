namespace Currencies.Infrastructure;

using Currencies.Models;

public interface ICurrencyRateProvider
{
    Task<CurrencyRate> GetRatesAsync(string? @base = null, string[]? symbols = null);
    Task<HistoricalRates> GetHistoricalRatesAsync(DateTime startDate,
        DateTime? endDate,
        string? @base = null,
        string[]? symbols = null);
    Task<decimal> Convert(string from, string to, decimal amount);
}
