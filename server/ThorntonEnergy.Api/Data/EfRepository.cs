using Microsoft.EntityFrameworkCore;
using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Api.Data;

// One class for every entity: registered as an open generic, so IRepository<Bill> gets EfRepository<Bill>
public class EfRepository<T>(AppDbContext db) : IRepository<T> where T : class
{
    public IQueryable<T> Query() => db.Set<T>();

    public async Task<T?> GetByIdAsync(int id) => await db.Set<T>().FindAsync(id);

    public void Add(T entity) => db.Set<T>().Add(entity);

    public void Remove(T entity) => db.Set<T>().Remove(entity);

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
