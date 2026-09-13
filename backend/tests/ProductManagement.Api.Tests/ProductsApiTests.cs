using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ProductManagement.Api.Data;
using ProductManagement.Api.Dtos;

namespace ProductManagement.Api.Tests;

public class ProductsApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _client;
    private readonly ApiFactory _factory;

    public ProductsApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_valid_product_returns_201()
    {
        var request = NewProductRequest(sku: $"SKU-{Guid.NewGuid():N}"[..20]);
        var response = await _client.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<ProductDetailDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be(request.Name);
        body.Variants.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_invalid_request_returns_400()
    {
        var request = new CreateProductRequest(
            Name: "",
            Description: null,
            CategoryId: SeedData.CategoryMen,
            Variants: [new ProductVariantDto(null, "SKU-X", "M", "Blue", 10, 1)],
            Attributes: null);

        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_negative_price_or_stock_returns_400()
    {
        var negativePrice = NewProductRequest(
            sku: $"SKU-NEG-{Guid.NewGuid():N}"[..24],
            price: -1,
            stock: 1);
        (await _client.PostAsJsonAsync("/api/products", negativePrice))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var negativeStock = NewProductRequest(
            sku: $"SKU-STK-{Guid.NewGuid():N}"[..24],
            price: 10,
            stock: -5);
        (await _client.PostAsJsonAsync("/api/products", negativeStock))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_duplicate_attribute_returns_400()
    {
        var duplicateAttributes = new List<ProductAttributeValueDto>
        {
            new(SeedData.AttrMaterial, null, "Cotton"),
            new(SeedData.AttrMaterial, null, "Wool")
        };

        var create = new CreateProductRequest(
            Name: "Dup Attr Create",
            Description: null,
            CategoryId: SeedData.CategoryMen,
            Variants: [new ProductVariantDto(null, $"SKU-ATTR-{Guid.NewGuid():N}"[..24], "M", "Blue", 10, 1)],
            Attributes: duplicateAttributes);

        (await _client.PostAsJsonAsync("/api/products", create))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var created = await CreateAsync(NewProductRequest(sku: $"SKU-ATTRU-{Guid.NewGuid():N}"[..24]));
        var update = new UpdateProductRequest(
            Name: created.Name,
            Description: created.Description,
            CategoryId: created.CategoryId,
            RowVersion: created.RowVersion,
            Variants: created.Variants,
            Attributes: duplicateAttributes);

        (await _client.PutAsJsonAsync($"/api/products/{created.Id}", update))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_duplicate_sku_returns_409()
    {
        var sku = $"SKU-DUP-{Guid.NewGuid():N}"[..24];
        var first = NewProductRequest(sku: sku);
        (await _client.PostAsJsonAsync("/api/products", first)).EnsureSuccessStatusCode();

        var second = NewProductRequest(sku: sku, name: "Another product");
        var response = await _client.PostAsJsonAsync("/api/products", second);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_missing_product_returns_404()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_product_succeeds()
    {
        var created = await CreateAsync(NewProductRequest(sku: $"SKU-UPD-{Guid.NewGuid():N}"[..24]));
        var update = new UpdateProductRequest(
            Name: "Updated Name",
            Description: "Updated",
            CategoryId: SeedData.CategoryWomen,
            RowVersion: created.RowVersion,
            Variants:
            [
                new ProductVariantDto(null, created.Variants[0].Sku!, "L", "Red", 29.99m, 5)
            ],
            Attributes:
            [
                new ProductAttributeValueDto(SeedData.AttrBrand, null, "Acme")
            ]);

        var response = await _client.PutAsJsonAsync($"/api/products/{created.Id}", update);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductDetailDto>(JsonOptions);
        body!.Name.Should().Be("Updated Name");
        body.CategoryId.Should().Be(SeedData.CategoryWomen);
        body.Variants[0].StockQuantity.Should().Be(5);
    }

    [Fact]
    public async Task Stale_concurrent_update_returns_409()
    {
        var created = await CreateAsync(NewProductRequest(sku: $"SKU-CON-{Guid.NewGuid():N}"[..24]));
        var staleVersion = created.RowVersion;

        var firstUpdate = new UpdateProductRequest(
            Name: "Client A",
            Description: created.Description,
            CategoryId: created.CategoryId,
            RowVersion: created.RowVersion,
            Variants: created.Variants.Select(v =>
                new ProductVariantDto(null, v.Sku!, v.Size, v.Color, v.Price, v.StockQuantity + 1)).ToList(),
            Attributes: null);

        (await _client.PutAsJsonAsync($"/api/products/{created.Id}", firstUpdate))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var staleUpdate = new UpdateProductRequest(
            Name: "Client B",
            Description: created.Description,
            CategoryId: created.CategoryId,
            RowVersion: staleVersion,
            Variants: created.Variants.Select(v =>
                new ProductVariantDto(null, v.Sku!, v.Size, v.Color, v.Price, 99)).ToList(),
            Attributes: null);

        var response = await _client.PutAsJsonAsync($"/api/products/{created.Id}", staleUpdate);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var current = await _client.GetFromJsonAsync<ProductDetailDto>($"/api/products/{created.Id}", JsonOptions);
        current!.Name.Should().Be("Client A");
        current.Variants[0].StockQuantity.Should().Be(created.Variants[0].StockQuantity + 1);
    }

    [Fact]
    public async Task Failed_aggregate_write_does_not_leave_partial_data()
    {
        var goodSku = $"SKU-OK-{Guid.NewGuid():N}"[..24];
        var existing = await CreateAsync(NewProductRequest(sku: goodSku));

        // Second variant SKU collides with existing product → entire create must roll back.
        var request = new CreateProductRequest(
            Name: "Partial Should Not Persist",
            Description: null,
            CategoryId: SeedData.CategoryMen,
            Variants:
            [
                new ProductVariantDto(null, $"SKU-NEW-{Guid.NewGuid():N}"[..24], "M", "Black", 10, 1),
                new ProductVariantDto(null, existing.Variants[0].Sku!, "L", "White", 12, 2)
            ],
            Attributes: null);

        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemDto>>(
            "/api/products?search=Partial%20Should%20Not%20Persist", JsonOptions);
        list!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_deactivates_product_and_hides_it_from_reads()
    {
        var created = await CreateAsync(NewProductRequest(sku: $"SKU-DEL-{Guid.NewGuid():N}"[..24]));

        var deleteResponse = await _client.DeleteAsync($"/api/products/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await _client.GetAsync($"/api/products/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await _client.GetFromJsonAsync<PagedResultDto<ProductListItemDto>>(
            $"/api/products?search={Uri.EscapeDataString(created.Name)}", JsonOptions);
        list!.Items.Should().NotContain(p => p.Id == created.Id);

        (await _client.DeleteAsync($"/api/products/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<ProductDetailDto> CreateAsync(CreateProductRequest request)
    {
        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDetailDto>(JsonOptions))!;
    }

    private static CreateProductRequest NewProductRequest(
        string sku,
        string name = "Test Product",
        decimal price = 19.99m,
        int stock = 10) =>
        new(
            Name: name,
            Description: "Desc",
            CategoryId: SeedData.CategoryMen,
            Variants:
            [
                new ProductVariantDto(null, sku, "M", "Blue", price, stock)
            ],
            Attributes:
            [
                new ProductAttributeValueDto(SeedData.AttrMaterial, null, "Cotton")
            ]);
}
