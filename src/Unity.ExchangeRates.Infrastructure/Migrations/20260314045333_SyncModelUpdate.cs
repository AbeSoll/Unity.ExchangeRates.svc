using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Unity.ExchangeRates.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE [name] = N'UserAgent'
                      AND [object_id] = OBJECT_ID(N'[AuditLog]')
                )
                BEGIN
                    DECLARE @var sysname;
                    SELECT @var = [d].[name]
                    FROM [sys].[default_constraints] [d]
                    INNER JOIN [sys].[columns] [c]
                        ON [d].[parent_column_id] = [c].[column_id]
                       AND [d].[parent_object_id] = [c].[object_id]
                    WHERE [d].[parent_object_id] = OBJECT_ID(N'[AuditLog]')
                      AND [c].[name] = N'UserAgent';

                    IF @var IS NOT NULL
                        EXEC(N'ALTER TABLE [AuditLog] DROP CONSTRAINT [' + @var + '];');

                    ALTER TABLE [AuditLog] DROP COLUMN [UserAgent];
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "AuditLog",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
