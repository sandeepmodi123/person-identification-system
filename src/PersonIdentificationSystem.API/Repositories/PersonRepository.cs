using Microsoft.EntityFrameworkCore;
using PersonIdentificationSystem.API.Models.Entities;

namespace PersonIdentificationSystem.API.Repositories;

public interface IPersonRepository : IRepository<Person>
{
    Task<(List<Person> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search, string? riskLevel, bool? isActive, CancellationToken ct = default);
    Task<Person?> GetWithPhotosAsync(Guid id, CancellationToken ct = default);
}

public class PersonRepository : BaseRepository<Person>, IPersonRepository
{
    public PersonRepository(ApplicationDbContext context) : base(context) { }

    public async Task<(List<Person> Items, int Total)> GetPagedAsync(
        int page, int pageSize, string? search, string? riskLevel, bool? isActive, CancellationToken ct = default)
    {
        var query = _dbSet.Include(p => p.Photos).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()));

        if (!string.IsNullOrWhiteSpace(riskLevel))
            query = query.Where(p => p.RiskLevel == riskLevel);

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.DateAdded)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<Person?> GetWithPhotosAsync(Guid id, CancellationToken ct = default)
        => await _dbSet.Include(p => p.Photos).FirstOrDefaultAsync(p => p.Id == id, ct);
}
