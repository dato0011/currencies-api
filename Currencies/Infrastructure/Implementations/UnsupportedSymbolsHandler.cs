
namespace Currencies.Infrastructure.Implementations;

/// <summary>
/// Handles the management and removal of unsupported currency symbols from exchange rate data.
/// </summary>
public class UnsupportedSymbolsHandler : IUnsupportedSymbolsHandler
{
    private readonly HashSet<string> _unsupportedSymbols = ["TRY", "PLN", "THB", "MXN"]; // Move to config

    /// <summary>
    /// Gets the read-only collection of unsupported currency symbols.
    /// </summary>    
    public IReadOnlySet<string> UnsupportedSymbols => _unsupportedSymbols;

    /// <summary>
    /// Removes unsupported currency symbols from the provided dictionary of exchange rates.
    /// </summary>
    /// <param name="dictionary">The dictionary containing currency symbols and their exchange rates.</param>
    public void StripUnsupportedSymbols(IDictionary<string, decimal> dictionary)
    {
        foreach (var symbol in _unsupportedSymbols)
        {
            dictionary.Remove(symbol);
        }
    }
}
