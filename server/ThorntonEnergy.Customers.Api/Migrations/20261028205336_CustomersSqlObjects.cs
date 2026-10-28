using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThorntonEnergy.Customers.Api.Migrations
{
    /// <inheritdoc />
    // The SQL objects EF cannot create from the model: the customer audit trigger and the customer summary view.
    // Same SQL as the monolith's AdminInsight migration, limited to customers.
    public partial class CustomersSqlObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TRIGGER dbo.trg_Customers_Audit ON dbo.Customers
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Status)
        INSERT INTO dbo.AuditLogs (TableName, RecordId, ColumnName, OldValue, NewValue, ChangedAt, ChangedBy)
        SELECT N'Customers', i.Id, N'Status', CAST(d.Status AS nvarchar(100)), CAST(i.Status AS nvarchar(100)),
               CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS datetime2), CAST(SESSION_CONTEXT(N'AppUser') AS nvarchar(256))
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE ISNULL(CAST(i.Status AS nvarchar(100)), N'') <> ISNULL(CAST(d.Status AS nvarchar(100)), N'');
    IF UPDATE(RatePlanId)
        INSERT INTO dbo.AuditLogs (TableName, RecordId, ColumnName, OldValue, NewValue, ChangedAt, ChangedBy)
        SELECT N'Customers', i.Id, N'RatePlanId', CAST(d.RatePlanId AS nvarchar(100)), CAST(i.RatePlanId AS nvarchar(100)),
               CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS datetime2), CAST(SESSION_CONTEXT(N'AppUser') AS nvarchar(256))
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE ISNULL(CAST(i.RatePlanId AS nvarchar(100)), N'') <> ISNULL(CAST(d.RatePlanId AS nvarchar(100)), N'');
END");

            migrationBuilder.Sql(@"
CREATE VIEW dbo.vw_CustomerSummary
AS
SELECT
    (SELECT COUNT(*) FROM dbo.Customers WHERE Status = N'Active')                         AS ActiveCustomers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW dbo.vw_CustomerSummary");
            migrationBuilder.Sql("DROP TRIGGER dbo.trg_Customers_Audit");
        }
    }
}
