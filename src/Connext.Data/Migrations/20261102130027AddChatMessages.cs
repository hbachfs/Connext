using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

public partial class AddChatMessages : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable(
			name: "ChatMessages",
			columns: table => new
			{
				Id = table.Column<long>(type: "bigint", nullable: false)
					.Annotation("SqlServer:Identity", "1, 1"),
				SenderId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
				ReceiverId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
				Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
				SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
				IsSeen = table.Column<bool>(type: "bit", nullable: false),
				SeenAt = table.Column<DateTime>(type: "datetime2", nullable: true)
			},
			constraints: table =>
			{
				table.PrimaryKey("PK_ChatMessages", x => x.Id);
			});

		migrationBuilder.CreateIndex(
			name: "IX_ChatMessages_SenderId_ReceiverId_SentAt",
			table: "ChatMessages",
			columns: new[] { "SenderId", "ReceiverId", "SentAt" });

		migrationBuilder.CreateIndex(
			name: "IX_ChatMessages_ReceiverId_IsSeen",
			table: "ChatMessages",
			columns: new[] { "ReceiverId", "IsSeen" });
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable(name: "ChatMessages");
	}
}