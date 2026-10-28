using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThorntonEnergy.Api.Dtos;
using ThorntonEnergy.Api.Models;

namespace ThorntonEnergy.Api.Services;

// Two ways this API talks to the Customers service ("Services:Customers" in configuration):

// 1) Login and register, before anyone is signed in: authenticated with a service token (ServiceTokenHandler).
public interface ICustomerAccounts
{
    // The account's id and status, or null if there is no such customer
    Task<CustomerAccount?> GetAsync(int customerId);

    // The customer whose account number AND email match, or null
    Task<CustomerAccount?> FindAsync(string accountNumber, string email);
}

public record CustomerAccount(int Id, AccountStatus Status);

public class CustomerAccountsClient(HttpClient http) : ICustomerAccounts
{
    // The Customers service writes enums as "active", "suspended", ...
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public Task<CustomerAccount?> GetAsync(int customerId) =>
        GetOrNullAsync($"api/internal/customers/{customerId}");

    public Task<CustomerAccount?> FindAsync(string accountNumber, string email) =>
        GetOrNullAsync($"api/internal/customers/lookup?accountNumber={Uri.EscapeDataString(accountNumber)}&email={Uri.EscapeDataString(email)}");

    private async Task<CustomerAccount?> GetOrNullAsync(string url)
    {
        using var response = await http.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode(); // anything else (down, 401, 500) -> HttpRequestException -> 503
        return await response.Content.ReadFromJsonAsync<CustomerAccount>(Json);
    }
}

// Puts a fresh service token on every outgoing request (no user token exists during login or register)
public class ServiceTokenHandler(ITokenService tokens) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new("Bearer", tokens.CreateServiceToken().Token);
        return base.SendAsync(request, cancellationToken);
    }
}

// 2) The admin dashboard: the admin's own token is passed along (see ServiceClients), like Billing and Outages.
public interface ICustomersClient
{
    Task<CustomerSummaryDto> GetSummaryAsync();
    Task<List<AuditLogDto>> GetActivityAsync(int take);
}

public class CustomersClient(HttpClient http) : ICustomersClient
{
    public async Task<CustomerSummaryDto> GetSummaryAsync() =>
        await http.GetFromJsonAsync<CustomerSummaryDto>("api/customers/summary")
        ?? throw new HttpRequestException("Customers returned an empty summary.");

    public async Task<List<AuditLogDto>> GetActivityAsync(int take) =>
        await http.GetFromJsonAsync<List<AuditLogDto>>($"api/customers/activity?take={take}") ?? [];
}
