using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize] // el TenantContextMiddleware ya seteó el tenant a partir del JWT antes de llegar acá
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _products;

    public ProductsController(IProductRepository products)
    {
        _products = products;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var products = await _products.ListAllAsync(ct);
        return Ok(products.Select(ToDto));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        return product is null ? NotFound() : Ok(ToDto(product));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertProductRequest request, CancellationToken ct)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            ImageUrl = request.ImageUrl,
            IsActive = request.IsActive
        };

        await _products.AddAsync(product, ct);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToDto(product));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpsertProductRequest request, CancellationToken ct)
    {
        var existing = await _products.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.Price = request.Price;
        existing.ImageUrl = request.ImageUrl;
        existing.IsActive = request.IsActive;

        await _products.UpdateAsync(existing, ct);

        return Ok(ToDto(existing));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _products.DeleteAsync(id, ct);
        return NoContent();
    }

    private static ProductDto ToDto(Product p)
        => new(p.Id, p.Name, p.Description, p.Price, p.ImageUrl, p.IsActive);
}
