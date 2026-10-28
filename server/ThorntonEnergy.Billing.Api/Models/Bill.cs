using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ThorntonEnergy.Billing.Api.Models;

public class Bill
{
    public int Id { get; set; }

    // The customer lives in another service's database, so this is just their id: no foreign key, no Customer property.
    // Ownership checks compare it with the customerId in the caller's JWT.
    public int CustomerId { get; set; }

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly DueDate { get; set; }
    public int KwhUsed { get; set; }
    public decimal AmountDue { get; set; }
    public BillStatus Status { get; set; }
    public decimal EnergyCharge { get; set; }
    public decimal ServiceCharge { get; set; }
    public DateOnly? PaidDate { get; set; }

    // SQL Server changes this on every update. If two people edit the same bill, the second save fails (409).
    [Timestamp, JsonIgnore] public byte[] RowVersion { get; set; } = [];
}

public enum BillStatus { Unpaid, Paid, Overdue }

// The two prices a bill is calculated from. They belong to the customer's rate plan, which another service owns.
public record RatePlanPrices(decimal PricePerKwh, decimal MonthlyFee);
