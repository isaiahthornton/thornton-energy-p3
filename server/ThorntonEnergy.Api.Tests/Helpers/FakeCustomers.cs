using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;
using ThorntonEnergy.Api.Services;

namespace ThorntonEnergy.Api.Tests.Helpers;

// Stands in for the Customers service's internal endpoints (login and register), so tests never make HTTP calls.
// Jane (1) and John (2) have logins in TestData; customer 3 has no login yet, so a test can register it.
public class FakeCustomerAccounts : ICustomerAccounts
{
    private readonly Dictionary<int, (string AccountNumber, string Email, AccountStatus Status)> _accounts = new()
    {
        [1] = ("TE-100001", "jane@test.com", AccountStatus.Active),
        [2] = ("TE-100002", "john@test.com", AccountStatus.Active),
        [3] = ("TE-100003", "new@test.com", AccountStatus.Active),
    };

    // Pretend the Customers service is not responding (the real client throws HttpRequestException -> 503)
    public bool IsDown { get; set; }

    // What an admin suspending or restoring an account in the Customers service would change
    public void SetStatus(int customerId, AccountStatus status)
    {
        lock (_accounts) _accounts[customerId] = _accounts[customerId] with { Status = status };
    }

    public Task<CustomerAccount?> GetAsync(int customerId)
    {
        ThrowIfDown();
        lock (_accounts)
            return Task.FromResult(_accounts.TryGetValue(customerId, out var a) ? new CustomerAccount(customerId, a.Status) : null);
    }

    public Task<CustomerAccount?> FindAsync(string accountNumber, string email)
    {
        ThrowIfDown();
        lock (_accounts)
        {
            var match = _accounts.FirstOrDefault(a => a.Value.AccountNumber == accountNumber && a.Value.Email == email);
            return Task.FromResult(match.Value.Email is null ? null : new CustomerAccount(match.Key, match.Value.Status));
        }
    }

    private void ThrowIfDown()
    {
        if (IsDown) throw new HttpRequestException("Customers service is down (test).");
    }
}

// Stands in for the Customers service's admin reports. Set Activity to control the customer changes it returns.
public class FakeCustomersClient : ICustomersClient
{
    public List<AuditLogDto> Activity { get; set; } = [];

    public Task<CustomerSummaryDto> GetSummaryAsync() => Task.FromResult(new CustomerSummaryDto(ActiveCustomers: 2));

    // Like the real service: newest first, at most "take" rows
    public Task<List<AuditLogDto>> GetActivityAsync(int take) =>
        Task.FromResult(Activity.OrderByDescending(a => a.ChangedAt).Take(take).ToList());
}
