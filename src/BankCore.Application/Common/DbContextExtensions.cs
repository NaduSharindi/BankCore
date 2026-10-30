using BankCore.Application.Abstractions;
using BankCore.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BankCore.Application.Common;

public static class DbContextExtensions
{
    /// <summary>
    /// Saves changes and converts optimistic-concurrency failures into a 409 Conflict
    /// that the client can safely retry.
    /// </summary>
    public static async Task SaveChangesWithConcurrencyCheckAsync(
        this IBankDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "CONCURRENCY_CONFLICT",
                "The account was updated by another operation. Please reload and retry.");
        }
    }
}
