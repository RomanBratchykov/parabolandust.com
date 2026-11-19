using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Newsletters.Dal.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountEventInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "source_version",
                schema: "newsletter",
                table: "users_copy",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "processed_events",
                schema: "newsletter",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_events", x => x.event_id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_copy_source_version",
                schema: "newsletter",
                table: "users_copy",
                sql: "source_version >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "processed_events",
                schema: "newsletter");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_copy_source_version",
                schema: "newsletter",
                table: "users_copy");

            migrationBuilder.DropColumn(
                name: "source_version",
                schema: "newsletter",
                table: "users_copy");
        }
    }
}
