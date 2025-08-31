namespace Currencies.Infrastructure;

public interface IUnsupportedSymbolsHandler
{
    IReadOnlySet<string> UnsupportedSymbols { get; }
    void StripUnsupportedSymbols(IDictionary<string, decimal> dictionary);
}