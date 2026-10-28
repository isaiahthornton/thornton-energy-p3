namespace ThorntonEnergy.Shared.Data;

// A thin wrapper over one EF table, so services don't depend on DbContext directly (easy to fake in tests)
public interface IRepository<T> where T : class
{
    IQueryable<T> Query();                 // start a LINQ query (add .AsNoTracking() for read-only lists)
    Task<T?> GetByIdAsync(int id);
    void Add(T entity);
    void Remove(T entity);
    Task SaveChangesAsync();
}
