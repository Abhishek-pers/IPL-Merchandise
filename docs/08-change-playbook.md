# 08 · Change playbook: how to make common changes cleanly

For the live review: each recipe lists **exactly which files to touch**, in order. Recipes marked **(config)** need no code.
After any code change run `dotnet test backend/tests/IplStore.UnitTests` (it takes seconds).

---

### A. Tune a parameter **(config)**
Edit `backend/src/IplStore.Api/appsettings.json`, or set an env var (`Section__Property`). Pricing, cart, catalogue and
checkout options are read through `IOptionsMonitor`, so **no restart is needed** when you edit the JSON file.

| Ask from the panel | Setting |
|---|---|
| "GST is now 12%" | `Pricing:TaxRate = 0.12` |
| "Free shipping above ₹1,499" | `Pricing:FreeShippingThreshold = 1499` |
| "Max 4 jerseys per customer" | `Cart:MaxQuantityPerLine = 4` |
| "Allow adding more than in stock" | `Cart:ValidateStockOnAdd = false` |
| "Retry DB 5 times, max 5 s" | `Resilience:MaxRetryCount = 5`, `Resilience:MaxRetryDelayMilliseconds = 5000` |
| "Treat lock timeouts as transient" | `Resilience:AdditionalTransientErrorCodes = ["55P03"]` |
| "20 products per page" | `Paging:DefaultPageSize = 20` |
| "Throttle to 50 req / 10 s" | `RateLimiting:PermitLimit = 50` |
| "Use the read replica" | `Database:ReadReplicaConnectionString = "..."` |

Invalid values (for example `TaxRate = 2`) stop the app at start-up with a clear message (`ValidateOnStart`).

### B. Add a new product type (e.g. "Mug", "Bat") **(data)**
Categories are rows, not an enum. Add `database/migrations/V004__add_bat_category.sql`:
```sql
INSERT INTO product_categories (id, code, name) VALUES ('c0000000-0000-0000-0000-000000000006', 'BAT', 'Signed Bat');
```
The filter drop-down, search and API pick it up automatically.

### C. Add a new search filter (e.g. "size = XL")
1. `Application/Catalog/SearchProductsQuery.cs` and `ProductSearchCriteria.cs`: add `string? Size` to both.
2. `Application/Catalog/CatalogService.cs → BuildCriteria`: pass it through (and validate it).
3. `Infrastructure/Queries/CatalogFilters/SizeFilter.cs`: new class
   ```csharp
   internal sealed class SizeFilter : ICatalogFilter
   {
       public IQueryable<CatalogItem> Apply(IQueryable<CatalogItem> q, ProductSearchCriteria c) =>
           c.Size is null ? q : q.Where(x => EF.Functions.JsonContains(x.Attributes, $"{{\"sizes\":[\"{c.Size}\"]}}"));
   }
   ```
4. `Infrastructure/DependencyInjection.cs`: `services.AddSingleton<ICatalogFilter, SizeFilter>();`
5. `Api/Contracts/Requests.cs`: add `Size` to `SearchProductsRequest` and map it in `ToQuery()`.
6. Unit test for `BuildCriteria`. Optionally an integration test.

No existing filter, query or controller logic changes (Open/Closed).

### D. Add a new sort order (e.g. "Best sellers")
1. `ProductSort` enum: add `BestSelling`.
2. `Infrastructure/Queries/CatalogQueries.cs → CatalogSorting.Apply`: add one switch arm.
3. Frontend `ProductListPage.tsx → SORTS`: add the label.

### E. Change how prices are calculated (e.g. "10% off everything during the IPL final")
Strategy pattern. Create `Application/Pricing/FestivePricingPolicy.cs : IPricingPolicy` (it can wrap `StandardPricingPolicy` as a decorator),
then change **one line** in `Application/DependencyInjection.cs`:
```csharp
services.AddSingleton<IPricingPolicy, FestivePricingPolicy>();
```
Cart preview and checkout both use it automatically. Add a test next to `StandardPricingPolicyTests`.
New pricing *input* (coupon code)? Add it to `PricingRequest` (parameter object), so no signature changes.

### F. Add a column / field end-to-end (e.g. "player name" on products)
1. `database/migrations/V004__product_player_name.sql`:
   `ALTER TABLE products ADD COLUMN player_name varchar(100);` + `ALTER TABLE product_catalog ADD COLUMN player_name varchar(100);` + `CREATE OR REPLACE FUNCTION project_product` (include the column).
2. `Domain/Catalog/Product.cs` (+ constructor) and `Infrastructure/Persistence/ReadModels/CatalogItem.cs`: add a property. Snake-case mapping is automatic.
3. `Application/Catalog/CatalogDtos.cs` + projection in `CatalogQueries`.
4. Frontend `types.ts` and the details page.

Never edit V001–V003: the migrator rejects modified scripts (checksum).

### G. Add real authentication
1. Add JWT bearer auth in `CompositionRoot.AddApiLayer`.
2. Create `ClaimsCurrentCustomerAccessor : ICurrentCustomerAccessor` (reads the `sub` or `oid` claim).
3. Replace the registration: `services.AddScoped<ICurrentCustomerAccessor, ClaimsCurrentCustomerAccessor>();`
4. Add `[Authorize]` next to `[RequiresCustomer]`.

Controllers and use cases are unchanged.

### H. Add a new use case (e.g. "cancel order")
1. Domain: `Order.Cancel(now)` with its state-transition rule and a new error code in `DomainErrorCodes`.
2. Application: `CancelOrderCommand`, method on `IOrderService` (or a new `IOrderCancellationService`). Use `IUnitOfWork` + a repository method.
3. Infrastructure: implement the repository method.
4. Api: `POST /api/v1/orders/{id}/cancel` in `OrdersController` (one line).
5. Tests: domain rule + use case with the fakes.

### I. Swap a query to Dapper (performance)
Implement `ICatalogQueries` with Dapper in Infrastructure and change the registration in `Infrastructure/DependencyInjection.cs`.
The application layer doesn't change, and the integration tests prove behaviour is identical.

### J. Switch the database (e.g. SQL Server)
Touches only Infrastructure + `database/`: swap `UseNpgsql` for `UseSqlServer`, translate the SQL scripts (`ON CONFLICT` → `MERGE`,
`FOR UPDATE` → `WITH (UPDLOCK, ROWLOCK)`, triggers, `xmin` → `rowversion`, `ILIKE` → `LIKE` with a CI collation).
Domain, Application, Api and the unit tests are untouched. That is the payoff of the ports.

### K. Change retry behaviour in the browser
`frontend/src/config.ts → retry` (`maxAttempts`, delays, statuses). Retry *eligibility* rules live in `httpClient.ts → isRetryable`.

---

## Where things live (cheat sheet)

| I want to change… | File |
|---|---|
| A business rule on the cart | `backend/src/IplStore.Domain/Carts/Cart.cs` |
| Checkout steps | `backend/src/IplStore.Application/Orders/CheckoutService.cs` |
| Transaction / retry mechanics | `backend/src/IplStore.Infrastructure/Persistence/EfUnitOfWork.cs` |
| Locking / SQL for the cart | `backend/src/IplStore.Infrastructure/Repositories/CartRepository.cs` |
| Stock reservation SQL | `backend/src/IplStore.Infrastructure/Repositories/ProductRepository.cs` |
| Error → HTTP status mapping | `backend/src/IplStore.Api/ErrorHandling/GlobalExceptionHandler.cs` |
| DI wiring / options binding | `backend/src/IplStore.Api/Composition/CompositionRoot.cs` |
| Schema | `database/migrations/` |
| Cloud resources | `infra/terraform/*.tf` |
| Pipelines | `.github/workflows/ci.yml` (CI), `.github/workflows/cd.yml` (deploy to Azure dev); see docs/13 |
