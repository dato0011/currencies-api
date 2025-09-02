namespace Currencies.Infrastructure.Implementations;

/// <summary>
/// Factory for creating currency rate providers based on the specified provider name.
/// </summary>
public class CurrencyRateProviderFactory : ICurrencyRateProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlySet<string> _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CurrencyRateProviderFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving currency rate providers.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
    public CurrencyRateProviderFactory(IServiceProvider serviceProvider)
    {
        // Note: Current implementation assumes that all providers have been registered in DI container.
        // In a real-world scenario, this can be extended to dynamically load providers from assemblies or configurations.
        // MEF is one option, but for simplicity, we are using a hardcoded approach here.

        ArgumentNullException.ThrowIfNull(serviceProvider, nameof(serviceProvider));

        _serviceProvider = serviceProvider;
        _providers = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            { Constants.ProviderFrankfurter }
            // Other providers goes here
        };
    }

    /// <summary>
    /// Gets the collection of available currency rate provider names.
    /// </summary>
    public IReadOnlySet<string> AvailableProviders => _providers;

    /// <summary>
    /// Creates a currency rate provider instance based on the specified provider name.
    /// </summary>
    /// <param name="providerName">The name of the provider to create.</param>
    /// <returns>An instance of <see cref="ICurrencyRateProvider"/> for the specified provider.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="providerName"/> is null, empty, or not supported.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the provider instance cannot be created.</exception>
    public ICurrencyRateProvider CreateProvider(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName, nameof(providerName));

        providerName = providerName.ToLower();
        if (!_providers.Contains(providerName))
            throw new ArgumentException($"No provider found with name '{providerName}'.", nameof(providerName));

        var provider = _serviceProvider.GetKeyedService<ICurrencyRateProvider>(providerName);
        if (provider == null)
            throw new InvalidOperationException($"Could not create an instance of '{providerName}' provider.");

        return provider;
    }
}
