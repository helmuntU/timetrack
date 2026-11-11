using Microsoft.EntityFrameworkCore;
using TimeTrack.Application.Common.Abstractions;
using TimeTrack.Application.Common.Exceptions;

namespace TimeTrack.Infrastructure.Persistence;

internal sealed class UnitOfWork(TimeTrackDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Two requests modified the same row at the same time: translate the EF exception
            // into an application exception so the API can answer 409 Conflict.
            throw new ConcurrencyConflictException(ex);
        }
    }
}
