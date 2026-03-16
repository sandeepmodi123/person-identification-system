using Microsoft.EntityFrameworkCore;
using PersonIdentificationSystem.API.Models.Entities;

namespace PersonIdentificationSystem.API.Repositories;

public interface INotificationLogRepository : IRepository<NotificationLog>
{
    Task<(List<NotificationLog> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? status, Guid? detectionId, CancellationToken ct = default);
}

public class NotificationLogRepository : BaseRepository<NotificationLog>, INotificationLogRepository
{
    public NotificationLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<(List<NotificationLog> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? status, Guid? detectionId, CancellationToken ct = default)
    {
        var query = _dbSet.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(n => n.Status == status);

        if (detectionId.HasValue)
            query = query.Where(n => n.DetectionId == detectionId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.SentTimestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
