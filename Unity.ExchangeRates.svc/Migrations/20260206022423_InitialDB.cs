using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unity.ExchangeRates.svc.Migrations
{
    /// <inheritdoc />
    public partial class InitialDB : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currency",
                columns: table => new
                {
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CurrencyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UnitBase = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currency", x => x.CurrencyCode);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRateHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CurrencyCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    RateDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    BuyingRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    SellingRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    MiddleRate = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    EffectiveDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExchangeRateHistory_Currency_CurrencyCode",
                        column: x => x.CurrencyCode,
                        principalTable: "Currency",
                        principalColumn: "CurrencyCode",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateHistory_CurrencyCode",
                table: "ExchangeRateHistory",
                column: "CurrencyCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExchangeRateHistory");

            migrationBuilder.DropTable(
                name: "Currency");
        }
    }
}
