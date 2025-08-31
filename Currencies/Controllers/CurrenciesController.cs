namespace Currency.Controllers;

using Currencies.Infrastructure;
using Currencies.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
[EnableRateLimiting("AuthenticatedUserPolicy")]
public class CurrenciesController : ControllerBase
{
    const string DEFAULT_BASE = Constants.SymbolEUR;
    const string DEFAULT_PROVIDER = Constants.ProviderFrankfurter;

    private readonly ICurrencyRateProviderFactory _currencyRateFactory;
    private readonly IUnsupportedSymbolsHandler _unsupportedSymbolsHandler;
    private readonly ILogger _logger;

    public CurrenciesController(ICurrencyRateProviderFactory currencyRateFactory, ILogger logger, IUnsupportedSymbolsHandler unsupportedSymbolsHandler)
    {
        _currencyRateFactory = currencyRateFactory ?? throw new ArgumentNullException(nameof(currencyRateFactory));
        _unsupportedSymbolsHandler = unsupportedSymbolsHandler ?? throw new ArgumentNullException(nameof(unsupportedSymbolsHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet("Latest")]
    [Authorize]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Any, VaryByQueryKeys = ["base", "provider", "symbols"])]
    public async Task<ActionResult<CurrencyRateResponseModel>> Latest(
        [FromQuery] string @base = DEFAULT_BASE,
        [FromQuery] string provider = DEFAULT_PROVIDER,
        [FromQuery] string[]? symbols = null)
    {
        if (!_currencyRateFactory.AvailableProviders.Contains(provider))
        {
            _logger.Debug("Invalid provider {Provider}", provider);
            return BadRequest($"Invalid provider: {provider}. Supported providers: {string.Join(',', _currencyRateFactory.AvailableProviders)}");
        }

        if (symbols is not null)
        {
            var unsupported = symbols.Where(s => _unsupportedSymbolsHandler.UnsupportedSymbols.Contains(s)).ToArray();
            if (unsupported.Length > 0)
            {
                _logger.Debug("Received request for unsupported symbol(s) {Symbols}", unsupported);
                return BadRequest($"Symbols: [{string.Join(',', _unsupportedSymbolsHandler.UnsupportedSymbols)}] are not supported");
            }
        }

        var ratesProvider = _currencyRateFactory.CreateProvider(provider);
        var result = await ratesProvider.GetRatesAsync(@base, symbols);
        _unsupportedSymbolsHandler.StripUnsupportedSymbols(result.Rates);

        return Ok(new CurrencyRateResponseModel // Use Mapster/AutoMapper in real projects
        {
            Base = result.Base,
            Rates = result.Rates,
            Date = result.Date
        });
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<HistoricalRatesResponseModel>> Get([FromQuery] HistoricalRatesRequest request)
    {
        const int MAX_PAGE_SIZE = 100;
        const int MAX_DATE_RANGE = 365;
        const int DEFAULT_PAGE_SIZE = 50;

        if (!DateTime.TryParse(request.StartDate, out var startDate) ||
            !DateTime.TryParse(request.EndDate, out var endDate))
        {
            return BadRequest("Invalid date format. Use YYYY-MM-DD.");
        }

        if (endDate < startDate) return BadRequest("End date must be after start date.");
        if ((endDate - startDate).TotalDays > MAX_DATE_RANGE) return BadRequest($"Date range cannot exceed {MAX_DATE_RANGE} days.");

        if (request.Page < 1) return BadRequest("Page must be greater than 0.");
        if (request.PageSize < 1 || request.PageSize > MAX_PAGE_SIZE) return BadRequest($"PageSize must be between 1 and {MAX_PAGE_SIZE}.");

        string[]? symbols = request.Symbols?.Split(',');
        if (symbols is not null)
        {
            var unsupported = symbols.Where(s => _unsupportedSymbolsHandler.UnsupportedSymbols.Contains(s)).ToArray();
            if (unsupported.Length > 0)
            {
                _logger.Debug("Received request for unsupported symbol(s) {Symbols}", unsupported);
                return BadRequest($"Symbols: [{string.Join(',', _unsupportedSymbolsHandler.UnsupportedSymbols)}] are not supported");
            }
        }

        var pageSize = request.PageSize ?? DEFAULT_PAGE_SIZE;
        var providerName = string.IsNullOrWhiteSpace(request.Provider) ? DEFAULT_PROVIDER : request.Provider;
        var ratesProvider = _currencyRateFactory.CreateProvider(providerName);
        var historicalRates = await ratesProvider.GetHistoricalRatesAsync(startDate, endDate, request.Base, symbols);
        foreach (var rates in historicalRates.Rates.Values)
        {
            _unsupportedSymbolsHandler.StripUnsupportedSymbols(rates);
        }

        var allRates = historicalRates.Rates.OrderBy(r => DateTime.Parse(r.Key)).ToArray(); // Just in case
        var totalRecords = allRates.Length;
        var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);
        var paginatedRates = allRates
            .Skip((request.Page - 1) * pageSize)
            .Take(pageSize)
            .ToDictionary(r => r.Key, r => r.Value);

        var result = new HistoricalRatesResponseModel
        {
            Base = historicalRates.Base,
            StartDate = historicalRates.StartDate,
            EndDate = historicalRates.EndDate,
            Rates = paginatedRates,
            CurrentPage = request.Page,
            PageSize = pageSize,
            TotalPages = totalPages,
            TotalRecords = totalRecords
        };

        return Ok(result);
    }

    private bool ContainsUnsupportedSymbol(string[]? symbols)
    {
        if (symbols is not null)
        {
            var unsupported = symbols.Where(s => _unsupportedSymbolsHandler.UnsupportedSymbols.Contains(s)).ToArray();
            if (unsupported.Length > 0) return true;
        }
        return false;
    }
}
