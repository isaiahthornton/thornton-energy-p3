namespace ThorntonEnergy.Api.Models;

public enum BillStatus { Unpaid, Paid, Overdue }
public enum AccountStatus { Active, Inactive, Suspended, Closed }
public enum MessageTopic { Billing, Outage, Service, Other }
public enum MessageStatus { New, Read }
public enum MeterStatus { Active, Inactive, Removed }
public enum OutageStatus { Reported, InProgress, Resolved }
