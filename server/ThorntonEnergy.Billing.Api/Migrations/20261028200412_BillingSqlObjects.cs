using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThorntonEnergy.Billing.Api.Migrations
{
    /// <inheritdoc />
    // The SQL objects EF cannot create from the model: the bill audit trigger, the billing summary view and the
    // monthly usage procedure. Same SQL as the monolith's AdminInsight migration, limited to bills.
    public partial class BillingSqlObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.Sql(@"CREATE VIEW dbo.vw_BillingSummary
AS
SELECT
    (SELECT ISNULL(SUM(AmountDue), 0) FROM dbo.Bills WHERE Status <> N'Paid')             AS UnpaidBalance,
    (SELECT ISNULL(SUM(AmountDue), 0) FROM dbo.Bills
      WHERE Status <> N'Paid' AND DueDate < t.Today)                                      AS OverdueBalance,
    (SELECT ISNULL(SUM(AmountDue), 0) FROM dbo.Bills
      WHERE Status = N'Paid' AND PaidDate >= t.MonthStart AND PaidDate < DATEADD(MONTH, 1, t.MonthStart)) AS RevenueThisMonth,
    (SELECT CAST(ISNULL(AVG(CAST(KwhUsed AS decimal(18, 2))), 0) AS decimal(18, 1)) FROM dbo.Bills
      WHERE PeriodStart >= DATEADD(MONTH, -1, t.MonthStart) AND PeriodStart < t.MonthStart) AS AvgKwhLastMonth
FROM (SELECT CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS date) AS Today,
             DATEFROMPARTS(YEAR(CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS date)), MONTH(CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS date)), 1) AS MonthStart) AS t");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE dbo.usp_GetMonthlyUsage");
            migrationBuilder.Sql("DROP VIEW dbo.vw_BillingSummary");
            migrationBuilder.Sql("DROP TRIGGER dbo.trg_Bills_Audit");
        }
    }
}
