using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Newsletters.Dal.Migrations
{
    /// <inheritdoc />
    public partial class AlignForeignKeyDeleteBehavior : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_newsletter_delivery_settings_newsletters_newsletter_id",
                schema: "newsletter",
                table: "newsletter_delivery_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_newsletters_admins_admin_id",
                schema: "newsletter",
                table: "newsletters");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_newsletters_newsletter_id",
                schema: "newsletter",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_users_copy_user_id",
                schema: "newsletter",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_user_comments_newsletters_newsletter_id",
                schema: "newsletter",
                table: "user_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_user_comments_user_comments_parent_comment_id",
                schema: "newsletter",
                table: "user_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_user_comments_users_copy_user_id",
                schema: "newsletter",
                table: "user_comments");

            migrationBuilder.AddForeignKey(
                name: "FK_newsletter_delivery_settings_newsletters_newsletter_id",
                schema: "newsletter",
                table: "newsletter_delivery_settings",
                column: "newsletter_id",
                principalSchema: "newsletter",
                principalTable: "newsletters",
                principalColumn: "newsletter_id");

            migrationBuilder.AddForeignKey(
                name: "FK_newsletters_admins_admin_id",
                schema: "newsletter",
                table: "newsletters",
                column: "admin_id",
                principalSchema: "newsletter",
                principalTable: "admins",
                principalColumn: "admin_id");

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_newsletters_newsletter_id",
                schema: "newsletter",
                table: "subscriptions",
                column: "newsletter_id",
                principalSchema: "newsletter",
                principalTable: "newsletters",
                principalColumn: "newsletter_id");

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_users_copy_user_id",
                schema: "newsletter",
                table: "subscriptions",
                column: "user_id",
                principalSchema: "newsletter",
                principalTable: "users_copy",
                principalColumn: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_comments_newsletters_newsletter_id",
                schema: "newsletter",
                table: "user_comments",
                column: "newsletter_id",
                principalSchema: "newsletter",
                principalTable: "newsletters",
                principalColumn: "newsletter_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_comments_user_comments_parent_comment_id",
                schema: "newsletter",
                table: "user_comments",
                column: "parent_comment_id",
                principalSchema: "newsletter",
                principalTable: "user_comments",
                principalColumn: "comment_id");

            migrationBuilder.AddForeignKey(
                name: "FK_user_comments_users_copy_user_id",
                schema: "newsletter",
                table: "user_comments",
                column: "user_id",
                principalSchema: "newsletter",
                principalTable: "users_copy",
                principalColumn: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_newsletter_delivery_settings_newsletters_newsletter_id",
                schema: "newsletter",
                table: "newsletter_delivery_settings");

            migrationBuilder.DropForeignKey(
                name: "FK_newsletters_admins_admin_id",
                schema: "newsletter",
                table: "newsletters");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_newsletters_newsletter_id",
                schema: "newsletter",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_subscriptions_users_copy_user_id",
                schema: "newsletter",
                table: "subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_user_comments_newsletters_newsletter_id",
                schema: "newsletter",
                table: "user_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_user_comments_user_comments_parent_comment_id",
                schema: "newsletter",
                table: "user_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_user_comments_users_copy_user_id",
                schema: "newsletter",
                table: "user_comments");

            migrationBuilder.AddForeignKey(
                name: "FK_newsletter_delivery_settings_newsletters_newsletter_id",
                schema: "newsletter",
                table: "newsletter_delivery_settings",
                column: "newsletter_id",
                principalSchema: "newsletter",
                principalTable: "newsletters",
                principalColumn: "newsletter_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_newsletters_admins_admin_id",
                schema: "newsletter",
                table: "newsletters",
                column: "admin_id",
                principalSchema: "newsletter",
                principalTable: "admins",
                principalColumn: "admin_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_newsletters_newsletter_id",
                schema: "newsletter",
                table: "subscriptions",
                column: "newsletter_id",
                principalSchema: "newsletter",
                principalTable: "newsletters",
                principalColumn: "newsletter_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_subscriptions_users_copy_user_id",
                schema: "newsletter",
                table: "subscriptions",
                column: "user_id",
                principalSchema: "newsletter",
                principalTable: "users_copy",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_comments_newsletters_newsletter_id",
                schema: "newsletter",
                table: "user_comments",
                column: "newsletter_id",
                principalSchema: "newsletter",
                principalTable: "newsletters",
                principalColumn: "newsletter_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_comments_user_comments_parent_comment_id",
                schema: "newsletter",
                table: "user_comments",
                column: "parent_comment_id",
                principalSchema: "newsletter",
                principalTable: "user_comments",
                principalColumn: "comment_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_user_comments_users_copy_user_id",
                schema: "newsletter",
                table: "user_comments",
                column: "user_id",
                principalSchema: "newsletter",
                principalTable: "users_copy",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
