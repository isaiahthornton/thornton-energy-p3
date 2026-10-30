using ThorntonEnergy.Billing.Api.Models;

namespace ThorntonEnergy.Billing.Api.Services;

// The billing math in one place. Static and pure (no database, no clock), so it is easy to unit test.
public static class BillingRules
{
    public const int DaysUntilDue = 21;

    // Energy = kWh x rate, service = the plan's monthly fee. Each step is rounded to cents like a real bill.
    public static (decimal Energy, decimal Service, decimal Total) Calculate(int kwh, RatePlanPrices plan)
    {
        var energy = Math.Round(kwh * plan.PricePerKwh, 2, MidpointRounding.AwayFromZero);
        var service = Math.Round(plan.MonthlyFee, 2, MidpointRounding.AwayFromZero);
        var total = Math.Round(energy + service, 2, MidpointRounding.AwayFromZero);
        return (energy, service, total);
    }

    public static DateOnly DueDate(DateOnly periodEnd) => periodEnd.AddDays(DaysUntilDue);

    // "Overdue" is worked out from the due date every time, never stored, so it cannot go stale
    public static BillStatus EffectiveStatus(Bill bill, DateOnly today) =>
        bill.Status != BillStatus.Paid && bill.DueDate < today ? BillStatus.Overdue : bill.Status;
}
