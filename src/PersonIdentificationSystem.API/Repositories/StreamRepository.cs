using Microsoft.EntityFrameworkCore;
using PersonIdentificationSystem.API.Models.Entities;

namespace PersonIdentificationSystem.API.Repositories;

public interface IStreamRepository : IRepository<RTSPStream>
{
    Task<List<RTSPStream>> GetActiveStreamsAsync(CancellationToken ct = default);
}

public class StreamRepository : BaseRepository<RTSPStream>, IStreamRepository
{
    public StreamRepository(ApplicationDbContext context) : base(context) { }

    public async Task<List<RTSPStream>> GetActiveStreamsAsync(CancellationToken ct = default)
        => await _dbSet.Where(s => s.IsActive).ToListAsync(ct);
}
