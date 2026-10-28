using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThorntonEnergy.Api.Migrations
{
    /// <inheritdoc />
    public partial class MoveBillsToBillingService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bills moved to the Billing service (ThorntonBilling database). First take bills out of the SQL objects
            // that read them, then drop the table (its audit trigger, trg_Bills_Audit, is dropped with it).
            // The bill numbers now come from Billing's vw_BillingSummary and usp_GetMonthlyUsage.
            migrationBuilder.Sql(@"
ALTER VIEW dbo.vw_AdminKpis
AS
SELECT
    (SELECT COUNT(*) FROM dbo.Customers WHERE Status = N'Active')                         AS ActiveCustomers,
    (SELECT COUNT(*) FROM dbo.Outages WHERE Status IN (N'Reported', N'InProgress'))       AS OpenOutages");

            migrationBuilder.Sql("DROP PROCEDURE dbo.usp_GetMonthlyUsage");

            migrationBuilder.DropTable(
                name: "Bills");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EnergyCharge = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    KwhUsed = table.Column<int>(type: "int", nullable: false),
                    PaidDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    ServiceCharge = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bills_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CustomerId",
                table: "Bills",
                column: "CustomerId");

            // Put back the trigger, procedure and full view exactly as the AdminInsight migration created them
            migrationBuilder.Sql(@"
CREATE TRIGGER dbo.trg_Bills_Audit ON dbo.Bills
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Status)
        INSERT INTO dbo.AuditLogs (TableName, RecordId, ColumnName, OldValue, NewValue, ChangedAt, ChangedBy)
        SELECT N'Bills', i.Id, N'Status', CAST(d.Status AS nvarchar(100)), CAST(i.Status AS nvarchar(100)),
               CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS datetime2), CAST(SESSION_CONTEXT(N'AppUser') AS nvarchar(256))
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE ISNULL(CAST(i.Status AS nvarchar(100)), N'') <> ISNULL(CAST(d.Status AS nvarchar(100)), N'');
END");

            migrationBuilder.Sql(@"
CREATE PROCEDURE dbo.usp_GetMonthlyUsage @Months int = 12
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today date = CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS date);
    DECLARE @ThisMonth date = DATEFROMPARTS(YEAR(@Today), MONTH(@Today), 1);
    DECLARE @From date = DATEADD(MONTH, -(@Months - 1), @ThisMonth);

    SELECT DATEFROMPARTS(YEAR(PeriodStart), MONTH(PeriodStart), 1) AS [Month],
           CAST(SUM(KwhUsed) AS bigint)                            AS TotalKwh,
           CAST(SUM(AmountDue) AS decimal(18, 2))                  AS TotalBilled
    FROM dbo.Bills
    WHERE PeriodStart >= @From AND PeriodStart < DATEADD(MONTH, 1, @ThisMonth)
    GROUP BY DATEFROMPARTS(YEAR(PeriodStart), MONTH(PeriodStart), 1)
    ORDER BY [Month];
END");

            migrationBuilder.Sql(@"
ALTER VIEW dbo.vw_AdminKpis
AS
SELECT
    (SELECT COUNT(*) FROM dbo.Customers WHERE Status = N'Active')                         AS ActiveCustomers,
    (SELECT ISNULL(SUM(AmountDue), 0) FROM dbo.Bills WHERE Status <> N'Paid')             AS UnpaidBalance,
    (SELECT ISNULL(SUM(AmountDue), 0) FROM dbo.Bills
      WHERE Status <> N'Paid' AND DueDate < t.Today)                                      AS OverdueBalance,
    (SELECT ISNULL(SUM(AmountDue), 0) FROM dbo.Bills
      WHERE Status = N'Paid' AND PaidDate >= t.MonthStart AND PaidDate < DATEADD(MONTH, 1, t.MonthStart)) AS RevenueThisMonth,
    (SELECT COUNT(*) FROM dbo.Outages WHERE Status IN (N'Reported', N'InProgress'))       AS OpenOutages,
    (SELECT CAST(ISNULL(AVG(CAST(KwhUsed AS decimal(18, 2))), 0) AS decimal(18, 1)) FROM dbo.Bills
      WHERE PeriodStart >= DATEADD(MONTH, -1, t.MonthStart) AND PeriodStart < t.MonthStart) AS AvgKwhLastMonth
FROM (SELECT CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS date) AS Today,
             DATEFROMPARTS(YEAR(CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS date)), MONTH(CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS date)), 1) AS MonthStart) AS t");
        }
    }
}
