using Microsoft.EntityFrameworkCore;

namespace ThorntonEnergy.Shared.Data;

// One class for every entity in any service's DbContext: IRepository<ContactMessage> -> EfRepository<MessagesDbContext, ContactMessage>
public class EfRepository<TContext, T>(TContext db) : IRepository<T>
    where TContext : DbContext
    where T : class
{
    public IQueryable<T> Query() => db.Set<T>();

    public async Task<T?> GetByIdAsync(int id) => await db.Set<T>().FindAsync(id);

    public void Add(T entity) => db.Set<T>().Add(entity);

    public void Remove(T entity) => db.Set<T>().Remove(entity);

    public Task SaveChangesAsync() => db.SaveChangesAsync();
}
