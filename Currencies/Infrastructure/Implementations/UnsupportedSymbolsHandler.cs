
namespace Currencies.Infrastructure.Implementations;

public class UnsupportedSymbolsHandler : IUnsupportedSymbolsHandler
{
    private readonly HashSet<string> _unsupportedSymbols = ["TRY", "PLN", "THB", "MXN"]; // Move to config
    public IReadOnlySet<string> UnsupportedSymbols => _unsupportedSymbols;

    public void StripUnsupportedSymbols(IDictionary<string, decimal> dictionary)
    {
        foreach (var symbol in _unsupportedSymbols)
        {
            dictionary.Remove(symbol);
        }
    }
}
