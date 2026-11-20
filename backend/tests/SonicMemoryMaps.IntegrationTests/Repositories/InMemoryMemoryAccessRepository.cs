
using SonicMemoryMaps.Application.Abstractions;
using SonicMemoryMaps.Domain.Entities;

namespace SonicMemoryMaps.IntegrationTests.Repositories;

public sealed class InMemoryMemoryAccessRepository : IMemoryAccessRepository
{
    public Memory? MemoryToReturn { get; set; }

    public int GetCallCount { get; private set; }

    public Guid? LastMemoryId { get; private set; }

    public Guid? LastRequestingUserId { get; private set; }

    public Task<Memory?> GetByIdForPlaybackAsync(
        Guid memoryId,
        Guid requestingUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        GetCallCount++;
        LastMemoryId = memoryId;
        LastRequestingUserId = requestingUserId;

        if (MemoryToReturn is null ||
            MemoryToReturn.Id != memoryId)
        {
            return Task.FromResult<Memory?>(null);
        }

        return Task.FromResult<Memory?>(MemoryToReturn);
    }
}
