using ThorntonEnergy.Billing.Api.Models;
using ThorntonEnergy.Billing.Api.Services;

namespace ThorntonEnergy.Billing.Api.Tests;

public class BillingRulesTests
{
    [Theory]
    [InlineData(1000, 0.18, 20, 180.00, 20.00, 200.00)]  // simple whole numbers
    [InlineData(0, 0.15, 15, 0.00, 15.00, 15.00)]        // no usage: only the monthly fee
    [InlineData(333, 0.145, 10, 48.29, 10.00, 58.29)]    // 48.285 rounds UP to 48.29 (away from zero, like a bill)
    [InlineData(1, 0.005, 0, 0.01, 0.00, 0.01)]          // half a cent rounds up
    public void Calculate_PricesUsageFromThePlan(int kwh, double price, double fee, double energy, double service, double total)
    {
        // Arrange
        var plan = new RatePlanPrices((decimal)price, (decimal)fee);

        // Act
        var result = BillingRules.Calculate(kwh, plan);

        // Assert
        Assert.Equal((decimal)energy, result.Energy);
        Assert.Equal((decimal)service, result.Service);
        Assert.Equal((decimal)total, result.Total);
    }

    [Theory]
    [InlineData("2026-01-31", "2026-02-21")] // 21 days later, across a month end
    [InlineData("2026-12-15", "2027-01-05")] // across a year end
    public void DueDate_Is21DaysAfterThePeriodEnds(string periodEnd, string expected)
    {
        // Act
        var due = BillingRules.DueDate(DateOnly.Parse(periodEnd));

        // Assert
        Assert.Equal(DateOnly.Parse(expected), due);
    }

    [Theory]
    [InlineData(BillStatus.Unpaid, "2026-02-28", "2026-03-01", BillStatus.Overdue)] // unpaid and past due
    [InlineData(BillStatus.Unpaid, "2026-03-01", "2026-03-01", BillStatus.Unpaid)]  // due today is not late yet
    [InlineData(BillStatus.Unpaid, "2026-03-01", "2026-02-01", BillStatus.Unpaid)]  // not due yet
    [InlineData(BillStatus.Paid, "2026-03-01", "2026-04-01", BillStatus.Paid)]      // paid bills are never overdue
    public void EffectiveStatus_IsOverdueOnlyWhenUnpaidAndPastDue(BillStatus stored, string dueDate, string today, BillStatus expected)
    {
        // Arrange
        var bill = new Bill { Status = stored, DueDate = DateOnly.Parse(dueDate) };

        // Act
        var status = BillingRules.EffectiveStatus(bill, DateOnly.Parse(today));

        // Assert
        Assert.Equal(expected, status);
    }
}
