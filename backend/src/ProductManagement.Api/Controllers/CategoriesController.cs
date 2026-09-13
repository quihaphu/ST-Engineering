using Microsoft.AspNetCore.Mvc;
using ProductManagement.Api.Dtos;
using ProductManagement.Api.Services;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly ProductService _products;

    public CategoriesController(ProductService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        return Ok(await _products.GetCategoriesAsync(cancellationToken));
    }
}
