namespace Currencies.Infrastructure;

/// <summary>
/// Defines the contract for handling unsupported currency symbols in exchange rate data.
/// </summary>
public interface IUnsupportedSymbolsHandler
{
    /// <summary>
    /// Gets the read-only collection of unsupported currency symbols.
    /// </summary>    
    IReadOnlySet<string> UnsupportedSymbols { get; }

    /// <summary>
    /// Removes unsupported currency symbols from a dictionary of exchange rates.
    /// </summary>
    /// <param name="dictionary">The dictionary containing currency symbols and their exchange rates.</param>    
    void StripUnsupportedSymbols(IDictionary<string, decimal> dictionary);
}
