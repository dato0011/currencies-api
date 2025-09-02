## Currencies API
A sample **.NET WebAPI** project that demonstrates how to query **currency exchange rates** (latest and historical) with authentication, caching, resiliency patterns, and observability.

## 🚀 Getting Started

#### Run The Project
`docker-compose up -d --build`

#### Swagger UI
`http://localhost:8080/swagger`

### 🔐 Authentication

#### Login
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

### 🌍 Currency Endpoints

#### Latest Exchange Rates
```bash
curl --location 'http://localhost:8080/api/v1/Currencies/Latest?symbols=CAD%2CCHF' \
--header 'Authorization: Bearer {TOKEN}'
```

#### Historical Rates
```bash
curl --location 'http://localhost:8080/api/v1/Currencies?startDate=1999-01-05&endDate=2000-01-04&page=3&pageSize=50&symbols=EUR%2CUSD%2CSEK' \
--header 'Authorization: Bearer {TOKEN}'
```

#### Features Implemented
* ✅ Retrieve latest and historical exchange rates
* ✅ Caching with Redis + Cache-Control headers
* ✅ Resilience: retry with exponential backoff & circuit breaker
* ✅ Factory pattern for dynamic provider selection
* ✅ JWT authentication with refresh tokens
* ✅ Role-based access control (RBAC) (historical rates require Admin role)
* ✅ Rate limiting policies for authenticated vs anonymous users
* ✅ Structured logging with Serilog + Seq
* ✅ Request correlation via X-Correlation-ID and logging middleware
* ✅ Distributed tracing with OpenTelemetry (AspNetCore + HttpClient)
* ✅ API versioning and environment-based configuration
* 🛇 Currency conversion endpoint (implemented in FrankfurterRateProvider, but not exposed in API)
* 🛇 90%+ test coverage and full integration tests (scope left open to focus on API design/architecture)

#### ⚠️ Known Issues
- `Serilog.Sinks.OpenTelemetry` is not compatible with Jaeger log collector — requires a separate OT collector.
- Jaeger integration does not work in Docker Compose (works when running locally with Development profile).

#### 🔧 Local Observability Setup
Run the following containers to launch the project locally (without docker-compose):
```bash
docker run -d -p 5341:5341 -e ACCEPT_EULA=y -e SEQ_FIRSTRUN_NOAUTHENTICATION=true datalust/seq
docker run -d -p 6379:6379 redis
docker run -d -p 6831:6831/udp -p 16686:16686 -p 4317:4317 -p 4318:4318 jaegertracing/all-in-one:latest
cd Currencies && dotnet run
```

Then authenticate and query exchange rates. You can explore:
- 🔎 Distributed Traces: http://localhost:16686/
- 📜 Logs: http://localhost:5341/


#### 📝 Notes
This project is designed as a learning and demonstration tool.
It focuses on N-Tier architecture, observability, and resilience rather than full production readiness (e.g., advanced testing coverage).