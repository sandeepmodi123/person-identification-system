using Microsoft.EntityFrameworkCore;
using PersonIdentificationSystem.API.DTOs;
using PersonIdentificationSystem.API.Models.Entities;

namespace PersonIdentificationSystem.API.Repositories;

public interface IDetectionRepository : IRepository<Detection>
{
    Task<(List<Detection> Items, int Total)> GetPagedAsync(DetectionFilterRequest filter, CancellationToken ct = default);
    Task<Detection?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
}

public class DetectionRepository : BaseRepository<Detection>, IDetectionRepository
{
    public DetectionRepository(ApplicationDbContext context) : base(context) { }

    public async Task<(List<Detection> Items, int Total)> GetPagedAsync(
        DetectionFilterRequest filter, CancellationToken ct = default)
    {
        var query = _dbSet
            .Include(d => d.Stream)
            .Include(d => d.Person)
            .AsQueryable();

        if (filter.StreamId.HasValue)
            query = query.Where(d => d.StreamId == filter.StreamId.Value);

        if (filter.PersonId.HasValue)
            query = query.Where(d => d.PersonId == filter.PersonId.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(d => d.DetectionTimestamp >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(d => d.DetectionTimestamp <= filter.ToDate.Value);

        if (filter.MinConfidence.HasValue)
            query = query.Where(d => d.ConfidenceScore >= filter.MinConfidence.Value);

        if (filter.IsVerified.HasValue)
            query = query.Where(d => d.IsVerified == filter.IsVerified.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.DetectionTimestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Detection?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => await _dbSet
            .Include(d => d.Stream)
            .Include(d => d.Person)
            .ThenInclude(p => p!.Photos)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
}
