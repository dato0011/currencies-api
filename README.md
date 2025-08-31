## Currencies API

#### Run The Project
`docker-compose up -d --build`

#### Authenticate
```bash
curl --location 'http://localhost:8080/api/v1/Auth/Login' \
--header 'Content-Type: application/json' \
--data '{
    "Username": "admin",
    "Password": "111"
}'
```
Or you can use user/222 for non-admin account

### Refresh Access Token
```bash
curl --location 'http://localhost:8080/api/v1/Auth/Refresh' \
--header 'Content-Type: application/json' \
--data '{
    "RefreshToken": "{TOKEN}"
}'
```

#### Retrieve Latest Exchange Rates
```bash
curl --location 'http://localhost:8080/api/v1/Currencies/Latest?symbols=CAD%2CCHF' \
--header 'Authorization: Bearer {TOKEN}'
```

#### Retrieve Historical Rates
```bash
curl --location 'http://localhost:8080/api/v1/Currencies?startDate=1999-01-05&endDate=2000-01-04&page=3&pageSize=50&symbols=EUR%2CUSD%2CSEK' \
--header 'Authorization: Bearer {TOKEN}'
```

#### Features Implemented
* Retrieve Latest Exchange Rates
* 🛇 Currency Conversion (WebAPI Action is missing. Conversion is implemented in [FrankfurterRateProvider](https://github.com/dato0011/currencies-api/blob/master/Currencies/Infrastructure/Implementations/FrankfurterRateProvider.cs#L89)). 
* ✓ Caching using Redis. Also sets Cache-Control headers for CDN/Proxy caching as well
* ✓ Retry policies with exponential backoff
* ✓ Circuit breaker to gracefully handle API outages
* ✓ DI for service abstractions
* ✓ Factory pattern to dynamically select the currency provider based on the request
* ✓ Allows for future integration with multiple exchange rate providers
* ✓ JWT authentication
* ✓ Role-based access control (RBAC) for API endpoints. Historical endpoint requires Admin role.
* ✓ Different API throttling policies for authenticated and anonymous users. Although this is something I would've configured on API Gateway level.
* ✓ Structured logging with Serilog with Seq sink configured
* ✓ Logging of common request items (client IP, client id etc) via logging middleware
* ✓ Correlates requests against all third party rate API providers by applying inbound `X-Correlation-Id` (Generated from middleware if missing) header to outgoing HTTP requests. A named HttpMessageHandler has been created for that. Logs has been enriched with CorrelationId from [RequestLoggingMiddleware](https://github.com/dato0011/currencies-api/blob/master/Currencies/Infrastructure/Middlewares/RequestLoggingMiddleware.cs).
* ✓ Implemented distributed tracing by open telemetry. Currently only AspNetCore and HttpClient instrumentations are available. I wanted to implement Redis as well, but turned out OpenTelemetry.Instrumentation.StackExchangeRedis package isn't able to instrument Microsoft.Extensions.Caching.StackExchangeRedis which I'm using in this project.
* 🛇 Achieve 90%+ unit test coverage. https://github.com/dato0011/currencies-api/tree/master/Currencies.Test/coveragereport
* 🛇 Implement integration tests to verify API interactions.
* ✓ Provide test coverage reports.
* ✓ Ensure the API supports deployment in multiple environments (Dev, Test, Prod).
* ✓ Support horizontal scaling for handling large request volumes.
* ✓ Implement API versioning for future-proofing.

I didn't attempt to achieve 90% coverage and implement integration tests (would've used TestContainers) as the excercise is already big enough to assess one's skillset as is. 

#### Known Issues
- Serilog.Sinks.OpenTelemetry is not compatible with Jaeger logs collector to provide contextual logs. A separated OT collector has to be deployed to achieve that.
- Jaeger integration doesn't work when running the application from docker-compose. Didn't dig deep as I already spent quite some time on the project. To see distributed tracing, either enable console logger from Program.cs, or run the project without docker-compose (Development profile).

```bash
docker run -d -p 5341:5341 -e ACCEPT_EULA=y -e SEQ_FIRSTRUN_NOAUTHENTICATION=true datalust/seq
docker run -d -p 6379:6379 redis
docker run -d -p 6831:6831/udp -p 16686:16686 -p 4317:4317 -p 4318:4318 jaegertracing/all-in-one:latest
cd Currencies && dotnet run
```

Then authenticate and retrieve exchange rates.
Now you should be able to see:
- Dist trace: http://localhost:16686/
- Logs: http://localhost:5341/