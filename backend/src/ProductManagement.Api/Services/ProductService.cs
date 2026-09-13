using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProductManagement.Api.Data;
using ProductManagement.Api.Data.Entities;
using ProductManagement.Api.Dtos;
using ProductManagement.Api.Exceptions;

namespace ProductManagement.Api.Services;

public class ProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResultDto<ProductListItemDto>> GetProductsAsync(
        int page,
        int pageSize,
        string? search,
        Guid? categoryId,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 10 : pageSize;

        var query = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Name.Contains(term) ||
                p.Variants.Any(v => v.Sku.Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Name,
                p.Category.Name,
                p.Variants.Min(v => (decimal?)v.Price),
                p.Variants.Sum(v => v.StockQuantity),
                p.Variants.Count))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ProductListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<ProductDetailDto> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Variants)
            .Include(p => p.AttributeValues)
            .ThenInclude(a => a.AttributeDefinition)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException($"Product '{id}' was not found.");
        }

        return MapDetail(product);
    }

    public async Task<ProductDetailDto> CreateProductAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureAttributeDefinitionsExistAsync(request.Attributes, cancellationToken);
        await EnsureSkusAvailableAsync(request.Variants.Select(v => v.Sku), excludeProductId: null, cancellationToken);

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CategoryId = request.CategoryId,
            IsActive = true,
            Variants = request.Variants.Select(MapVariant).ToList(),
            AttributeValues = MapAttributes(request.Attributes)
        };

        _db.Products.Add(product);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueSkuViolation(ex))
        {
            throw new ConflictException("One or more SKUs already exist.");
        }

        return await GetProductAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDetailDto> UpdateProductAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureAttributeDefinitionsExistAsync(request.Attributes, cancellationToken);
        await EnsureSkusAvailableAsync(request.Variants.Select(v => v.Sku), excludeProductId: id, cancellationToken);

        byte[] incomingRowVersion;
        try
        {
            incomingRowVersion = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["rowVersion"] = ["RowVersion must be a valid base64 string."]
            });
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var product = await _db.Products
            .Include(p => p.Variants)
            .Include(p => p.AttributeValues)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException($"Product '{id}' was not found.");
        }

        // Ensure Product row participates in optimistic concurrency even for child-only changes.
        _db.Entry(product).Property(p => p.RowVersion).OriginalValue = incomingRowVersion;

        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.CategoryId = request.CategoryId;

        // Touch a Product scalar so EF always issues an UPDATE against Products (rowversion check).
        _db.Entry(product).Property(p => p.Name).IsModified = true;

        // Delete children first so re-inserting the same SKUs does not trip the unique index
        // when EF would otherwise INSERT before DELETE in one SaveChanges batch.
        _db.ProductVariants.RemoveRange(product.Variants.ToList());
        _db.ProductAttributeValues.RemoveRange(product.AttributeValues.ToList());
        product.Variants.Clear();
        product.AttributeValues.Clear();

        try
        {
            await _db.SaveChangesAsync(cancellationToken);

            foreach (var variant in request.Variants.Select(MapVariant))
            {
                variant.ProductId = product.Id;
                _db.ProductVariants.Add(variant);
            }

            foreach (var attribute in MapAttributes(request.Attributes))
            {
                attribute.ProductId = product.Id;
                _db.ProductAttributeValues.Add(attribute);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("The product was modified by another user. Refresh and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueSkuViolation(ex))
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new ConflictException("One or more SKUs already exist.");
        }

        return await GetProductAsync(id, cancellationToken);
    }

    public async Task DeleteProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive, cancellationToken);
        if (product is null)
        {
            throw new NotFoundException($"Product '{id}' was not found.");
        }

        product.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AttributeDefinitionDto>> GetAttributeDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.AttributeDefinitions
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AttributeDefinitionDto(a.Id, a.Name, a.DataType))
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var exists = await _db.Categories.AsNoTracking().AnyAsync(c => c.Id == categoryId, cancellationToken);
        if (!exists)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["categoryId"] = ["Category does not exist."]
            });
        }
    }

    private async Task EnsureAttributeDefinitionsExistAsync(
        IReadOnlyList<ProductAttributeValueDto>? attributes,
        CancellationToken cancellationToken)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return;
        }

        var ids = attributes.Select(a => a.AttributeDefinitionId).Distinct().ToList();
        var existingCount = await _db.AttributeDefinitions
            .AsNoTracking()
            .CountAsync(a => ids.Contains(a.Id), cancellationToken);

        if (existingCount != ids.Count)
        {
            throw new ValidationAppException(new Dictionary<string, string[]>
            {
                ["attributes"] = ["One or more attribute definitions do not exist."]
            });
        }
    }

    private async Task EnsureSkusAvailableAsync(
        IEnumerable<string> skus,
        Guid? excludeProductId,
        CancellationToken cancellationToken)
    {
        var normalized = skus.Select(s => s.Trim()).ToList();
        var query = _db.ProductVariants.AsNoTracking().Where(v => normalized.Contains(v.Sku));
        if (excludeProductId.HasValue)
        {
            query = query.Where(v => v.ProductId != excludeProductId.Value);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new ConflictException("One or more SKUs already exist.");
        }
    }

    private static ProductVariant MapVariant(ProductVariantDto dto) => new()
    {
        Id = Guid.NewGuid(),
        Sku = dto.Sku.Trim(),
        Size = dto.Size.Trim(),
        Color = dto.Color.Trim(),
        Price = dto.Price,
        StockQuantity = dto.StockQuantity
    };

    private static List<ProductAttributeValue> MapAttributes(IReadOnlyList<ProductAttributeValueDto>? attributes)
    {
        if (attributes is null || attributes.Count == 0)
        {
            return new List<ProductAttributeValue>();
        }

        return attributes.Select(a => new ProductAttributeValue
        {
            Id = Guid.NewGuid(),
            AttributeDefinitionId = a.AttributeDefinitionId,
            Value = a.Value.Trim()
        }).ToList();
    }

    private static ProductDetailDto MapDetail(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.CategoryId,
        product.Category.Name,
        Convert.ToBase64String(product.RowVersion),
        product.Variants
            .OrderBy(v => v.Sku)
            .Select(v => new ProductVariantDto(v.Id, v.Sku, v.Size, v.Color, v.Price, v.StockQuantity))
            .ToList(),
        product.AttributeValues
            .OrderBy(a => a.AttributeDefinition.Name)
            .Select(a => new ProductAttributeValueDto(
                a.AttributeDefinitionId,
                a.AttributeDefinition.Name,
                a.Value))
            .ToList());

    private static bool IsUniqueSkuViolation(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sql &&
               (sql.Number == 2601 || sql.Number == 2627) &&
               sql.Message.Contains("Sku", StringComparison.OrdinalIgnoreCase);
    }
}
