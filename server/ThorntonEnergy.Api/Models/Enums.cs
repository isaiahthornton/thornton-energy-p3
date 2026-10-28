namespace ThorntonEnergy.Api.Models;

// A customer account's status as the Customers service reports it (login and register refuse anything but Active)
public enum AccountStatus { Active, Inactive, Suspended, Closed }
