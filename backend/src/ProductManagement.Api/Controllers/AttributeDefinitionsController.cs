using Microsoft.AspNetCore.Mvc;
using ProductManagement.Api.Dtos;
using ProductManagement.Api.Services;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/attribute-definitions")]
public class AttributeDefinitionsController : ControllerBase
{
    private readonly ProductService _products;

    public AttributeDefinitionsController(ProductService products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttributeDefinitionDto>>> GetAttributeDefinitions(
        CancellationToken cancellationToken)
    {
        return Ok(await _products.GetAttributeDefinitionsAsync(cancellationToken));
    }
}
