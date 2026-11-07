using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Connext.Data.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalFriendshipPair : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Friendships_RequesterId_AddresseeId",
                table: "Friendships");

            migrationBuilder.AddColumn<Guid>(
                name: "PairUserHighId",
                table: "Friendships",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PairUserLowId",
                table: "Friendships",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE Friendships
                SET PairUserLowId = CASE
                        WHEN LOWER(CONVERT(char(36), RequesterId)) COLLATE Latin1_General_BIN2
                           < LOWER(CONVERT(char(36), AddresseeId)) COLLATE Latin1_General_BIN2
                        THEN RequesterId ELSE AddresseeId END,
                    PairUserHighId = CASE
                        WHEN LOWER(CONVERT(char(36), RequesterId)) COLLATE Latin1_General_BIN2
                           < LOWER(CONVERT(char(36), AddresseeId)) COLLATE Latin1_General_BIN2
                        THEN AddresseeId ELSE RequesterId END
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "PairUserHighId",
                table: "Friendships",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "PairUserLowId",
                table: "Friendships",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_PairUserLowId_PairUserHighId",
                table: "Friendships",
                columns: new[] { "PairUserLowId", "PairUserHighId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequesterId",
                table: "Friendships",
                column: "RequesterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Friendships_PairUserLowId_PairUserHighId",
                table: "Friendships");

            migrationBuilder.DropIndex(
                name: "IX_Friendships_RequesterId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "PairUserHighId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "PairUserLowId",
                table: "Friendships");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequesterId_AddresseeId",
                table: "Friendships",
                columns: new[] { "RequesterId", "AddresseeId" },
                unique: true);
        }
    }
}
