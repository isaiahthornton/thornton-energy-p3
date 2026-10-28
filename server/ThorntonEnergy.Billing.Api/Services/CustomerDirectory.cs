using System.Net;
using System.Net.Http.Json;
using ThorntonEnergy.Billing.Api.Models;

namespace ThorntonEnergy.Billing.Api.Services;

// What Billing needs to know about customers, which another service owns ("Services:Customers" in configuration;
// the Customers service, which owns customers and their rate plans). An interface, so tests can use a fake instead of HTTP.
public interface ICustomerDirectory
{
    // The prices of the customer's current rate plan, or null if there is no such customer
    Task<RatePlanPrices?> GetRatePlanPricesAsync(int customerId);
}

// Calls the existing GET api/customers/{id} and GET api/ratePlans/{id} endpoints with the caller's own token
// (only admins create bills, and both endpoints allow admins). Errors surface as HttpRequestException -> 503.
public class CustomerDirectoryClient(HttpClient http) : ICustomerDirectory
{
    // Only the fields Billing reads; the rest of the JSON is ignored
    private record CustomerResponse(int Id, int RatePlanId);
    private record RatePlanResponse(int Id, decimal PricePerKwh, decimal MonthlyFee);

    public async Task<RatePlanPrices?> GetRatePlanPricesAsync(int customerId)
    {
        using var response = await http.GetAsync($"api/customers/{customerId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var customer = await response.Content.ReadFromJsonAsync<CustomerResponse>()
            ?? throw new HttpRequestException($"Empty response for customer {customerId}.");
        var plan = await http.GetFromJsonAsync<RatePlanResponse>($"api/ratePlans/{customer.RatePlanId}")
            ?? throw new HttpRequestException($"Empty response for rate plan {customer.RatePlanId}.");

        return new RatePlanPrices(plan.PricePerKwh, plan.MonthlyFee);
    }
}
