namespace ProductManagement.Api.Dtos;

public record CategoryDto(Guid Id, string Name);

public record AttributeDefinitionDto(Guid Id, string Name, string DataType);

public record ProductVariantDto(
    Guid? Id,
    string Sku,
    string Size,
    string Color,
    decimal Price,
    int StockQuantity);

public record ProductAttributeValueDto(
    Guid AttributeDefinitionId,
    string? AttributeName,
    string Value);

public record ProductListItemDto(
    Guid Id,
    string Name,
    string CategoryName,
    decimal? MinPrice,
    int TotalStock,
    int VariantCount);

public record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);

public record ProductDetailDto(
    Guid Id,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    string RowVersion,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductAttributeValueDto> Attributes);

public record CreateProductRequest(
    string Name,
    string? Description,
    Guid CategoryId,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductAttributeValueDto>? Attributes);

public record UpdateProductRequest(
    string Name,
    string? Description,
    Guid CategoryId,
    string RowVersion,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductAttributeValueDto>? Attributes);
