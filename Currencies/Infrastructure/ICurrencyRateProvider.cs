namespace Currencies.Infrastructure;

using Currencies.Models;

/// <summary>
/// Defines the contract for retrieving currency exchange rates and performing currency conversions.
/// </summary>
public interface ICurrencyRateProvider
{
    /// <summary>
    /// Retrieves the latest currency exchange rates for a specified base currency and optional symbols.
    /// </summary>
    /// <param name="base">The base currency symbol (optional).</param>
    /// <param name="symbols">An array of currency symbols to retrieve rates for (optional).</param>
    /// <returns>A <see cref="Task{TResult}"/> containing a <see cref="CurrencyRate"/> object with the latest exchange rates.</returns>
    Task<CurrencyRate> GetRatesAsync(string? @base = null, string[]? symbols = null);

    /// <summary>
    /// Retrieves historical currency exchange rates for a specified date range, base currency, and optional symbols.
    /// </summary>
    /// <param name="startDate">The start date for the historical rates.</param>
    /// <param name="endDate">The end date for the historical rates (optional).</param>
    /// <param name="base">The base currency symbol (optional).</param>
    /// <param name="symbols">An array of currency symbols to retrieve rates for (optional).</param>
    /// <returns>A <see cref="Task{TResult}"/> containing a <see cref="HistoricalRates"/> object with the historical exchange rates.</returns>    
    Task<HistoricalRates> GetHistoricalRatesAsync(DateTime startDate,
        DateTime? endDate,
        string? @base = null,
        string[]? symbols = null);

    /// <summary>
    /// Converts an amount from one currency to another using the latest exchange rates.
    /// </summary>
    /// <param name="from">The source currency symbol.</param>
    /// <param name="to">The target currency symbol.</param>
    /// <param name="amount">The amount to convert.</param>
    /// <returns>A <see cref="Task{TResult}"/> containing the converted amount as a decimal.</returns>
    Task<decimal> Convert(string from, string to, decimal amount);
}
