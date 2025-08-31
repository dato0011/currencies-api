namespace Currencies.Infrastructure.Implementations;

public class CurrencyRateProviderFactory : ICurrencyRateProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlySet<string> _providers;

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

    public IReadOnlySet<string> AvailableProviders => _providers;

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
