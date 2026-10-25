using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Newsletters.Dal.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "newsletter");

            migrationBuilder.CreateTable(
                name: "admins",
                schema: "newsletter",
                columns: table => new
                {
                    admin_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    admin_login = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    admin_password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    admin_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    admin_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admins", x => x.admin_id);
                    table.CheckConstraint("ck_admins_row_version", "row_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "users_copy",
                schema: "newsletter",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    user_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "account-events"),
                    source_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "account-events"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users_copy", x => x.user_id);
                    table.CheckConstraint("ck_users_copy_row_version", "row_version > 0");
                });

            migrationBuilder.CreateTable(
                name: "newsletters",
                schema: "newsletter",
                columns: table => new
                {
                    newsletter_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    admin_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    main_info = table.Column<string>(type: "text", nullable: false),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    published_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_newsletters", x => x.newsletter_id);
                    table.CheckConstraint("ck_newsletters_publish_time", "is_hidden OR published_at IS NOT NULL");
                    table.CheckConstraint("ck_newsletters_row_version", "row_version > 0");
                    table.ForeignKey(
                        name: "FK_newsletters_admins_admin_id",
                        column: x => x.admin_id,
                        principalSchema: "newsletter",
                        principalTable: "admins",
                        principalColumn: "admin_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "newsletter_delivery_settings",
                schema: "newsletter",
                columns: table => new
                {
                    newsletter_id = table.Column<long>(type: "bigint", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    recipient_settings = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{\"subscribers\": true}'::jsonb"),
                    reply_to_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_newsletter_delivery_settings", x => x.newsletter_id);
                    table.CheckConstraint("ck_newsletter_delivery_settings_row_version", "row_version > 0");
                    table.ForeignKey(
                        name: "FK_newsletter_delivery_settings_newsletters_newsletter_id",
                        column: x => x.newsletter_id,
                        principalSchema: "newsletter",
                        principalTable: "newsletters",
                        principalColumn: "newsletter_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "subscriptions",
                schema: "newsletter",
                columns: table => new
                {
                    subscription_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    newsletter_id = table.Column<long>(type: "bigint", nullable: false),
                    sub_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    consent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    unsubscribe_token = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    user_email_snapshot = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_subscriptions", x => x.subscription_id);
                    table.CheckConstraint("ck_subscriptions_row_version", "row_version > 0");
                    table.CheckConstraint("ck_subscriptions_status", "sub_status IN ('Active', 'Unsubscribed')");
                    table.ForeignKey(
                        name: "FK_subscriptions_newsletters_newsletter_id",
                        column: x => x.newsletter_id,
                        principalSchema: "newsletter",
                        principalTable: "newsletters",
                        principalColumn: "newsletter_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_subscriptions_users_copy_user_id",
                        column: x => x.user_id,
                        principalSchema: "newsletter",
                        principalTable: "users_copy",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_comments",
                schema: "newsletter",
                columns: table => new
                {
                    comment_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    newsletter_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_comment_id = table.Column<long>(type: "bigint", nullable: true),
                    comment_text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 1L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_comments", x => x.comment_id);
                    table.CheckConstraint("ck_user_comments_row_version", "row_version > 0");
                    table.ForeignKey(
                        name: "FK_user_comments_newsletters_newsletter_id",
                        column: x => x.newsletter_id,
                        principalSchema: "newsletter",
                        principalTable: "newsletters",
                        principalColumn: "newsletter_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_comments_user_comments_parent_comment_id",
                        column: x => x.parent_comment_id,
                        principalSchema: "newsletter",
                        principalTable: "user_comments",
                        principalColumn: "comment_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_comments_users_copy_user_id",
                        column: x => x.user_id,
                        principalSchema: "newsletter",
                        principalTable: "users_copy",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "newsletter",
                table: "admins",
                columns: new[] { "admin_id", "created_at", "created_by", "admin_email", "admin_login", "admin_name", "admin_password_hash", "row_version", "updated_at", "updated_by" },
                values: new object[] { 4001L, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "seed", "adm1@gmail.com", "editor_demo", "Demo Editor", "demo-password-hash", 1L, null, null });

            migrationBuilder.InsertData(
                schema: "newsletter",
                table: "users_copy",
                columns: new[] { "user_id", "created_at", "created_by", "user_email", "row_version", "source_updated_at", "updated_at", "updated_by", "user_name" },
                values: new object[] { 1001L, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "account-events", "alex@example.test", 1L, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "account-events", "alex_player" });

            migrationBuilder.CreateIndex(
                name: "admins_admin_login_key",
                schema: "newsletter",
                table: "admins",
                column: "admin_login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_newsletters_admin_history",
                schema: "newsletter",
                table: "newsletters",
                columns: new[] { "admin_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_newsletters_public_feed",
                schema: "newsletter",
                table: "newsletters",
                column: "published_at",
                descending: new bool[0],
                filter: "is_hidden = FALSE AND is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_subscriptions_delivery",
                schema: "newsletter",
                table: "subscriptions",
                columns: new[] { "newsletter_id", "user_id" },
                filter: "sub_status = 'Active' AND is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "subscriptions_unsubscribe_token_key",
                schema: "newsletter",
                table: "subscriptions",
                column: "unsubscribe_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_subscriptions_user_newsletter",
                schema: "newsletter",
                table: "subscriptions",
                columns: new[] { "user_id", "newsletter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_comments_newsletter_thread",
                schema: "newsletter",
                table: "user_comments",
                columns: new[] { "newsletter_id", "created_at" },
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_user_comments_parent_comment_id",
                schema: "newsletter",
                table: "user_comments",
                column: "parent_comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_comments_user_id",
                schema: "newsletter",
                table: "user_comments",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "newsletter_delivery_settings",
                schema: "newsletter");

            migrationBuilder.DropTable(
                name: "subscriptions",
                schema: "newsletter");

            migrationBuilder.DropTable(
                name: "user_comments",
                schema: "newsletter");

            migrationBuilder.DropTable(
                name: "newsletters",
                schema: "newsletter");

            migrationBuilder.DropTable(
                name: "users_copy",
                schema: "newsletter");

            migrationBuilder.DropTable(
                name: "admins",
                schema: "newsletter");
        }
    }
}
