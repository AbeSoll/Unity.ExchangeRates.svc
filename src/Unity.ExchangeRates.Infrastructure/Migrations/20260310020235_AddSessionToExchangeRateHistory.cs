using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unity.ExchangeRates.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionToExchangeRateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Session",
                table: "ExchangeRateHistory",
                type: "nvarchar(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateHistory_RateDate_Session_CurrencyCode",
                table: "ExchangeRateHistory",
                columns: new[] { "RateDate", "Session", "CurrencyCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_CreatedOn",
                table: "AuditLog",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_ResponseStatusCode",
                table: "AuditLog",
                column: "ResponseStatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_TraceId",
                table: "AuditLog",
                column: "TraceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeRateHistory_RateDate_Session_CurrencyCode",
                table: "ExchangeRateHistory");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_CreatedOn",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_ResponseStatusCode",
                table: "AuditLog");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_TraceId",
                table: "AuditLog");

            migrationBuilder.DropColumn(
                name: "Session",
                table: "ExchangeRateHistory");
        }
    }
}
