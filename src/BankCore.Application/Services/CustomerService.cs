using BankCore.Application.Abstractions;
using BankCore.Application.Common;
using BankCore.Application.Dtos;
using BankCore.Application.Mapping;
using BankCore.Domain.Common;
using BankCore.Domain.Entities;
using BankCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankCore.Application.Services;

public sealed class CustomerService(
    IBankDbContext db,
    IAccountNumberGenerator numberGenerator,
    IAuditService audit,
    IClock clock,
    ILogger<CustomerService> logger)
{
    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var nic = SriLankanNic.Normalize(request.Nic);

        if (await db.Customers.AnyAsync(c => c.Nic == nic, cancellationToken))
        {
            throw new ConflictException("DUPLICATE_NIC", "A customer with this NIC is already registered.");
        }

        var customerNumber = await numberGenerator.NextCustomerNumberAsync(cancellationToken);
        var customer = new Customer(
            customerNumber, request.FullName, nic, request.Email, request.PhoneNumber, clock.UtcNow);

        db.Customers.Add(customer);
        audit.Record("CUSTOMER_CREATED", nameof(Customer), customer.Id.ToString(), $"CustomerNumber={customerNumber}");
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Customer {CustomerNumber} onboarded", customerNumber);
        return customer.ToResponse();
    }

    public async Task<CustomerResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Include(c => c.Accounts)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException("Customer", id.ToString());

        return customer.ToResponse();
    }

    public async Task<PagedResult<CustomerResponse>> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var upperTerm = term.ToUpperInvariant();
            query = query.Where(c =>
                c.FullName.Contains(term) || c.Nic == upperTerm || c.CustomerNumber == upperTerm);
        }

        var total = await query.CountAsync(cancellationToken);
        var customers = await query
            .OrderBy(c => c.FullName)
            .ThenBy(c => c.CustomerNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Accounts)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerResponse>(
            customers.Select(c => c.ToResponse()).ToList(), page, pageSize, total);
    }
}
