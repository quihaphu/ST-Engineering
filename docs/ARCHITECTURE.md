# Architecture

## Stack

- Backend: ASP.NET Core 8 Web API, EF Core, SQL Server, FluentValidation, Swagger, ProblemDetails
- Frontend: React + TypeScript + Vite + Material UI + TanStack Query + React Hook Form
- Runtime: Docker Compose (SQL Server + API + frontend)
- Tests: xUnit + WebApplicationFactory + Testcontainers MsSql

## Layering

Controllers → `ProductService` → `AppDbContext` → SQL Server.

No MediatR, CQRS, or generic repository. DTOs are returned from the API; EF entities are not exposed.

## Database

| Table | Role |
|-------|------|
| Categories | Seeded lookup |
| Products | Aggregate root; `RowVersion` concurrency token; soft delete via `IsActive` |
| ProductVariants | SKU (unique), size, color, price, stock |
| AttributeDefinitions | Seeded extensible attribute keys |
| ProductAttributeValues | Product-scoped string values |

### Consistency

- Create/update writes Product + Variants + Attributes in one EF `SaveChanges` / transaction.
- `PUT` sets `OriginalValue` for `Product.RowVersion` and marks the Product row modified so **child-only** updates still hit optimistic concurrency.
- Stale version → `409 Conflict`; no child rows persisted.

### Future cache-aside (not implemented)

1. On `GET /api/products/{id}`, read Redis key `product:{id}`; on miss load from SQL and set TTL.
2. On successful `PUT`/`DELETE`, invalidate `product:{id}` (and optionally list cache keys).
3. Keep SQL Server as source of truth; never use cache for write path.

## Scalability

- SQL Server is the source of truth so product writes stay strongly consistent (one transaction per aggregate, unique SKU, `rowversion`).
- The API is stateless and can scale out behind a load balancer; list queries are paged (`page` / `pageSize`, max 100) and filtered/indexed (`CategoryId`, `IsActive`, unique `Sku`).
- New product features use `AttributeDefinition` + `ProductAttributeValue` instead of adding columns per attribute.
- Cache is not in the write path. If read traffic grows, add Redis cache-aside for `GET /api/products/{id}` and invalidate on PUT/DELETE (see below). Do not cache list pages until there is a measured need.

## File upload (out of MVP)

Image upload is intentionally omitted. The assessment is product CRUD with consistency, not blob storage, CDN, or virus scanning. Adding uploads now would expand scope (multipart endpoints, object storage, auth on writes).

Future approach: store images in object storage (S3/Azure Blob); persist only URL/key on the product or a child table; upload via a dedicated endpoint or pre-signed URL; never put binary bytes in SQL Server.

## API surface

```text
GET    /api/products
GET    /api/products/{id}
POST   /api/products
PUT    /api/products/{id}
DELETE /api/products/{id}

GET    /api/categories
GET    /api/attribute-definitions
```
