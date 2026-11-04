using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ThorntonEnergy.Chat.Api.Services;

/// <summary>What the assistant knows about the signed-in customer before they type anything.</summary>
public sealed record CustomerSnapshot(string? FirstName, string Text);

/// <summary>
/// Reads the customer's own profile, plan, meters, bills and outages (using their own login token)
/// and turns them into a short text block for the assistant's instructions.
/// If one service is down, that part is simply left out.
/// </summary>
public sealed class CustomerSnapshotService(HttpClient httpClient, IConfiguration configuration)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // Only the fields the assistant needs. Dates and statuses stay strings, so nothing breaks if a service adds a value.
    private sealed record ProfileRow(string FirstName, string LastName, string AccountNumber, string Email,
        string Phone, string ServiceAddress, int RatePlanId, string Status);
    private sealed record PlanRow(int Id, string Name, decimal PricePerKwh, decimal MonthlyFee);
    private sealed record MeterRow(string MeterNumber, string InstallationDate, string Status);
    private sealed record BillRow(string PeriodStart, string PeriodEnd, string DueDate, int KwhUsed,
        decimal AmountDue, string Status);
    private sealed record OutageRow(string Address, string Description, string TimeReported, string Status);

    /// <summary>Just the first name, for the greeting when the chat opens.</summary>
    public async Task<string?> GetFirstNameAsync(int customerId, string token, CancellationToken cancellationToken)
    {
        var profile = await GetAsync<ProfileRow>("Customers", $"api/customers/{customerId}", token, cancellationToken);
        return string.IsNullOrWhiteSpace(profile?.FirstName) ? null : profile.FirstName.Trim();
    }

    public async Task<CustomerSnapshot> BuildAsync(int customerId, string token, CancellationToken cancellationToken)
    {
        // All five reads run at the same time
        var profileTask = GetAsync<ProfileRow>("Customers", $"api/customers/{customerId}", token, cancellationToken);
        var plansTask = GetAsync<List<PlanRow>>("Customers", "api/ratePlans", token, cancellationToken);
        var metersTask = GetAsync<List<MeterRow>>("Metering", "api/meters", token, cancellationToken);
        var billsTask = GetAsync<List<BillRow>>("Billing", "api/bills", token, cancellationToken);
        var outagesTask = GetAsync<List<OutageRow>>("Outages", "api/outages", token, cancellationToken);
        await Task.WhenAll(profileTask, plansTask, metersTask, billsTask, outagesTask);

        var profile = profileTask.Result;
        var text = new StringBuilder();
        if (profile is null)
        {
            text.AppendLine("The account record could not be loaded right now. Use the tools for account questions.");
            return new CustomerSnapshot(null, text.ToString());
        }

        text.AppendLine($"Name: {Clean(profile.FirstName)} {Clean(profile.LastName)}");
        text.AppendLine($"Account number: {Clean(profile.AccountNumber)}");
        text.AppendLine($"Account status: {Clean(profile.Status)}");
        text.AppendLine($"Service address: {Clean(profile.ServiceAddress)}");
        text.AppendLine($"Email: {Clean(profile.Email)}   Phone: {Clean(profile.Phone)}");

        var plan = plansTask.Result?.FirstOrDefault(p => p.Id == profile.RatePlanId);
        if (plan is not null)
            text.AppendLine($"Rate plan: {Clean(plan.Name)} ({Money(plan.PricePerKwh, 4)} per kWh, {Money(plan.MonthlyFee)} monthly fee)");

        if (metersTask.Result is { } meters)
            text.AppendLine(meters.Count == 0
                ? "Meters: none on file"
                : "Meters: " + string.Join("; ", meters.Select(m =>
                    $"{Clean(m.MeterNumber)} ({Clean(m.Status)}, installed {Clean(m.InstallationDate)})")));

        if (billsTask.Result is { } bills)
        {
            var owing = bills.Where(b => !b.Status.Equals("paid", StringComparison.OrdinalIgnoreCase)).ToList();
            text.AppendLine($"Unpaid balance: {Money(owing.Sum(b => b.AmountDue))} across {owing.Count} bill(s)");
            foreach (var b in bills.OrderByDescending(b => b.PeriodStart).Take(4))
                text.AppendLine($"Bill: {Clean(b.PeriodStart)} to {Clean(b.PeriodEnd)}, {b.KwhUsed} kWh, " +
                                $"{Money(b.AmountDue)}, {Clean(b.Status)}, due {Clean(b.DueDate)}");
        }

        if (outagesTask.Result is { } outages)
        {
            if (outages.Count == 0)
                text.AppendLine("Outage reports: none");
            foreach (var o in outages.OrderByDescending(o => o.TimeReported).Take(3))
                text.AppendLine($"Outage: reported {Clean(o.TimeReported)}, {Clean(o.Address)}, " +
                                $"{Clean(o.Status)}, \"{Clean(o.Description, 120)}\"");
        }

        return new CustomerSnapshot(string.IsNullOrWhiteSpace(profile.FirstName) ? null : profile.FirstName.Trim(),
            text.ToString());
    }

    private async Task<T?> GetAsync<T>(string service, string path, string token, CancellationToken cancellationToken)
        where T : class
    {
        if (!Uri.TryCreate(configuration[$"Services:{service}"], UriKind.Absolute, out var baseUri))
            return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null; // a slow or broken service just means less context, not a failed chat
        }
    }

    private static string Money(decimal amount, int decimals = 2) =>
        "$" + amount.ToString("N" + decimals, Invariant);

    // Account data is shown to the model as plain text: one line, no line breaks, capped length
    private static string Clean(string? value, int max = 200)
    {
        var line = string.Join(' ', (value ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        return line.Length <= max ? line : line[..max] + "…";
    }
}
