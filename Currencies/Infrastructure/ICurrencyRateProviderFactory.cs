namespace Currencies.Infrastructure;

using System.Collections.Generic;

public interface ICurrencyRateProviderFactory
{
    IReadOnlySet<string> AvailableProviders { get; }
    ICurrencyRateProvider CreateProvider(string providerName);
}
