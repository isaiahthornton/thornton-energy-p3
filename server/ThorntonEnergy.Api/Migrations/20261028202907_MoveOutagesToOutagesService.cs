using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThorntonEnergy.Api.Migrations
{
    /// <inheritdoc />
    public partial class MoveOutagesToOutagesService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Outages moved to the Outages service (ThorntonOutages database). First take outages out of the KPI view,
            // then drop the table (its audit trigger, trg_Outages_Audit, is dropped with it).
            // The open-outage count now comes from that service's vw_OutageSummary.
            migrationBuilder.Sql(@"
ALTER VIEW dbo.vw_AdminKpis
AS
SELECT
    (SELECT COUNT(*) FROM dbo.Customers WHERE Status = N'Active')                         AS ActiveCustomers");

            migrationBuilder.DropTable(
                name: "Outages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Outages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeReported = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeResolved = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Outages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Outages_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Outages_CustomerId",
                table: "Outages",
                column: "CustomerId");

            // Put back the trigger (as the AdminInsight migration created it) and the two-column view
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
ALTER VIEW dbo.vw_AdminKpis
AS
SELECT
    (SELECT COUNT(*) FROM dbo.Customers WHERE Status = N'Active')                         AS ActiveCustomers,
    (SELECT COUNT(*) FROM dbo.Outages WHERE Status IN (N'Reported', N'InProgress'))       AS OpenOutages");
        }
    }
}
