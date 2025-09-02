namespace Currencies.Infrastructure;

using System.Collections.Generic;

/// <summary>
/// Defines the contract for a factory that creates currency rate providers based on provider names.
/// </summary>
public interface ICurrencyRateProviderFactory
{
    /// <summary>
    /// Gets the collection of available currency rate provider names.
    /// </summary>
    IReadOnlySet<string> AvailableProviders { get; }

    /// <summary>
    /// Creates a currency rate provider instance for the specified provider name.
    /// </summary>
    /// <param name="providerName">The name of the provider to create.</param>
    /// <returns>An instance of <see cref="ICurrencyRateProvider"/> for the specified provider.</returns>    
    ICurrencyRateProvider CreateProvider(string providerName);
}
