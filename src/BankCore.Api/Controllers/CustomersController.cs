using BankCore.Api.Authentication;
using BankCore.Application.Common;
using BankCore.Application.Dtos;
using BankCore.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankCore.Api.Controllers;

[ApiController]
[Route("api/v1/customers")]
[Produces("application/json")]
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    /// <summary>Onboards a new customer (KYC basics: name, NIC, contact details).</summary>
    [HttpPost]
    [Authorize(Policy = Policies.Onboard)]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerResponse>> Create(
        CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var created = await customers.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Gets a customer with their accounts.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.ViewAccounts)]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await customers.GetAsync(id, cancellationToken));

    /// <summary>Searches customers by name, NIC or customer number.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ViewAccounts)]
    [ProducesResponseType<PagedResult<CustomerResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CustomerResponse>>> Search(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await customers.SearchAsync(search, page, pageSize, cancellationToken));
}
