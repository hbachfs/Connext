using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connext.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSecurityAndMediaStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @NormalizedPhones TABLE (Id uniqueidentifier, PhoneNumber nvarchar(20));
                INSERT INTO @NormalizedPhones (Id, PhoneNumber)
                SELECT Id,
                    CASE
                        WHEN Digits LIKE '0084%' THEN '+84' + SUBSTRING(Digits, 5, 20)
                        WHEN Digits LIKE '84%' THEN '+' + Digits
                        WHEN Digits LIKE '0%' THEN '+84' + SUBSTRING(Digits, 2, 20)
                        ELSE '+' + Digits
                    END
                FROM
                (
                    SELECT Id,
                        REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                            PhoneNumber, '+', ''), ' ', ''), '-', ''), '(', ''), ')', ''), '.', ''), '/', '') AS Digits
                    FROM Users
                ) AS cleaned;

                IF EXISTS (
                    SELECT PhoneNumber FROM @NormalizedPhones
                    GROUP BY PhoneNumber HAVING COUNT(*) > 1
                )
                    THROW 51000, 'Phone normalization would create duplicate phone numbers. Resolve duplicates before applying this migration.', 1;

                UPDATE userRow
                SET PhoneNumber = normalized.PhoneNumber
                FROM Users AS userRow
                INNER JOIN @NormalizedPhones AS normalized ON normalized.Id = userRow.Id
                """);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MediaAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Extension = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaAssets_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaAssets_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetGrants",
                columns: table => new
                {
                    TokenId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetGrants", x => x.TokenId);
                    table.ForeignKey(
                        name: "FK_PasswordResetGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetOtps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAttempts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetOtps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetOtps_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_ConversationId_Kind_CreatedAt",
                table: "MediaAssets",
                columns: new[] { "ConversationId", "Kind", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_UploadedById",
                table: "MediaAssets",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetGrants_UserId",
                table: "PasswordResetGrants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetOtps_UserId_CreatedAt",
                table: "PasswordResetOtps",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaAssets");

            migrationBuilder.DropTable(
                name: "PasswordResetGrants");

            migrationBuilder.DropTable(
                name: "PasswordResetOtps");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Users");
        }
    }
}
