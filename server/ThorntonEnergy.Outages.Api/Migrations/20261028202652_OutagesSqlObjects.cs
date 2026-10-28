using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThorntonEnergy.Outages.Api.Migrations
{
    /// <inheritdoc />
    // The SQL objects EF cannot create from the model: the outage audit trigger and the outage summary view.
    // Same SQL as the monolith's AdminInsight migration, limited to outages.
    public partial class OutagesSqlObjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TRIGGER dbo.trg_Outages_Audit ON dbo.Outages
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF UPDATE(Status)
        INSERT INTO dbo.AuditLogs (TableName, RecordId, ColumnName, OldValue, NewValue, ChangedAt, ChangedBy)
        SELECT N'Outages', i.Id, N'Status', CAST(d.Status AS nvarchar(100)), CAST(i.Status AS nvarchar(100)),
               CAST(SYSUTCDATETIME() AT TIME ZONE N'UTC' AT TIME ZONE N'Eastern Standard Time' AS datetime2), CAST(SESSION_CONTEXT(N'AppUser') AS nvarchar(256))
        FROM inserted i
        JOIN deleted d ON d.Id = i.Id
        WHERE ISNULL(CAST(i.Status AS nvarchar(100)), N'') <> ISNULL(CAST(d.Status AS nvarchar(100)), N'');
END");

            migrationBuilder.Sql(@"
CREATE VIEW dbo.vw_OutageSummary
AS
SELECT
    (SELECT COUNT(*) FROM dbo.Outages WHERE Status IN (N'Reported', N'InProgress'))       AS OpenOutages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW dbo.vw_OutageSummary");
            migrationBuilder.Sql("DROP TRIGGER dbo.trg_Outages_Audit");
        }
    }
}
