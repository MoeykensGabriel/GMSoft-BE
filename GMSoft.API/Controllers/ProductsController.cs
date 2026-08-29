using GMSoft.Application.Common.Authorization;
using GMSoft.Application.Common.Models;
using GMSoft.Application.Features.Products.Common;
using GMSoft.Application.Features.Products.Create;
using GMSoft.Application.Features.Products.Delete;
using GMSoft.Application.Features.Products.GetById;
using GMSoft.Application.Features.Products.GetList;
using GMSoft.Application.Features.Products.Update;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GMSoft.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = AppRoles.AdminOrDriver)]
public class ProductsController : ControllerBase
{
    private readonly ISender _mediator;

    public ProductsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Catálogo paginado. El chofer también lo lee: necesita ver los productos
    /// publicados para cargar el camión y registrar entregas.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetList(
        [FromQuery] GetProductsQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetProductByIdQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<ActionResult<Guid>> Create(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>El id manda el de la ruta; el del cuerpo se ignora.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Update(
        Guid id,
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(command with { Id = id }, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = AppRoles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteProductCommand(id), cancellationToken);
        return NoContent();
    }
}
