using ThorntonEnergy.Customers.Api.Data;
using ThorntonEnergy.Customers.Api.Models;

namespace ThorntonEnergy.Customers.Api.Tests;

// A tiny dataset: 1 plan and 3 customers on it. Jane (1) and John (2) are active; Sam (3) is suspended.
public static class TestData
{
    public static void Seed(CustomersDbContext db)
    {
        db.RatePlans.Add(new RatePlan { Id = 1, Name = "Standard", PricePerKwh = 0.10m, MonthlyFee = 10m, Description = "Test plan" });

        db.Customers.AddRange(
            NewCustomer(1, "TE-100001", "Jane", "Doe", "jane@test.com", AccountStatus.Active),
            NewCustomer(2, "TE-100002", "John", "Smith", "john@test.com", AccountStatus.Active),
            NewCustomer(3, "TE-100003", "Sam", "Stone", "sam@test.com", AccountStatus.Suspended));

        db.SaveChanges();
    }

    private static Customer NewCustomer(int id, string account, string first, string last, string email, AccountStatus status) => new()
    {
        Id = id, AccountNumber = account, FirstName = first, LastName = last, Phone = id.ToString(), Email = email,
        ServiceAddress = $"{id} Main St", RatePlanId = 1, Status = status,
    };
}
