using ThorntonEnergy.Shared.Data;

namespace ThorntonEnergy.Api.Data;

// The shared repository, bound to the monolith's AppDbContext. Registered as an open generic,
// so IRepository<Bill> gets EfRepository<Bill>.
public class EfRepository<T>(AppDbContext db) : EfRepository<AppDbContext, T>(db) where T : class;
