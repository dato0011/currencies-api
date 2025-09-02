namespace Currencies.Infrastructure;

using System.ComponentModel;

/// <summary>
/// Defines constant values used throughout the application for dependency injection, provider names, currency symbols, and HTTP client configurations.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The key used to inject the Frankfurter API resilience policies from the dependency injection container.
    /// </summary>
    [Description("Key for injecting Frankfurter API resilience policies from DI.")]
    public const string DiKeyFraknfurterResiliencePolicy = "FrankfurterResiliencePolicies";

    /// <summary>
    /// The identifier for the Frankfurter API provider.
    /// </summary>
    public const string ProviderFrankfurter = "frankfurter";

    /// <summary>
    /// The currency symbol for Euro (EUR).
    /// </summary>
    public const string SymbolEUR = "EUR";

    /// The name used to create named HttpClient instances from IHttpClientFactory for querying third-party APIs, such as the Frankfurter API.
    /// </summary>
    [Description("Name for creating named HttpClient instances to query third-party APIs like Frankfurter.")]
    public const string HttpClientThirdPartyApi = "ThirdPartyApi";
}
