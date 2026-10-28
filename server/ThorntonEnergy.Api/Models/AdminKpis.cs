namespace ThorntonEnergy.Api.Models;

// One row from the vw_AdminKpis SQL view. It has no table and no key: EF only reads it.
// Bill and outage numbers moved to the Billing and Outages services; AdminInsightService adds them.
public class AdminKpis
{
    public int ActiveCustomers { get; set; }
}
