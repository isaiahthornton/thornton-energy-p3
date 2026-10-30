using System.Net;
using System.Net.Http.Json;
using ThorntonEnergy.Outages.Api.Models;

namespace ThorntonEnergy.Outages.Api.Services;

// What Outages needs to know about customers, which another service owns ("Services:Customers" in configuration;
// the Customers service, which owns customers and their rate plans). An interface, so tests can use a fake instead of HTTP.
public interface ICustomerDirectory
{
    // Does this customer exist? (an admin reporting an outage on someone's behalf)
    Task<bool> ExistsAsync(int customerId);

    // Name and map location of every customer that has coordinates, by customer id (for the outage map)
    Task<Dictionary<int, CustomerLocation>> GetLocationsAsync();
}

// Calls the existing GET api/customers and GET api/customers/{id} endpoints with the caller's own token
// (both uses are admin-only, and admins may read any customer). Errors surface as HttpRequestException -> 503.
public class CustomerDirectoryClient(HttpClient http) : ICustomerDirectory
{
    // Only the fields Outages reads; the rest of the JSON is ignored
    private record CustomerResponse(int Id, string FirstName, string LastName, decimal? Latitude, decimal? Longitude);

    public async Task<bool> ExistsAsync(int customerId)
    {
        using var response = await http.GetAsync($"api/customers/{customerId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    // One call for the whole map, instead of one per outage
    public async Task<Dictionary<int, CustomerLocation>> GetLocationsAsync()
    {
        var customers = await http.GetFromJsonAsync<List<CustomerResponse>>("api/customers") ?? [];
        return customers
            .Where(c => c.Latitude is not null && c.Longitude is not null)
            .ToDictionary(c => c.Id, c => new CustomerLocation($"{c.FirstName} {c.LastName}", c.Latitude!.Value, c.Longitude!.Value));
    }
}
