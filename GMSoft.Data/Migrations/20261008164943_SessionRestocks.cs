using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionRestocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RestockProductDetail",
                table: "SessionStockMovements",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionRestockId",
                table: "SessionStockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SessionRestocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliverySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RegisteredByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionRestocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionRestocks_DeliverySessions_DeliverySessionId",
                        column: x => x.DeliverySessionId,
                        principalTable: "DeliverySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionStockMovements_SessionRestockId",
                table: "SessionStockMovements",
                column: "SessionRestockId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionRestocks_ClientRequestId",
                table: "SessionRestocks",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionRestocks_DeliverySessionId_OccurredAt",
                table: "SessionRestocks",
                columns: new[] { "DeliverySessionId", "OccurredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_SessionStockMovements_SessionRestocks_SessionRestockId",
                table: "SessionStockMovements",
                column: "SessionRestockId",
                principalTable: "SessionRestocks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionStockMovements_SessionRestocks_SessionRestockId",
                table: "SessionStockMovements");

            migrationBuilder.DropTable(
                name: "SessionRestocks");

            migrationBuilder.DropIndex(
                name: "IX_SessionStockMovements_SessionRestockId",
                table: "SessionStockMovements");

            migrationBuilder.DropColumn(
                name: "RestockProductDetail",
                table: "SessionStockMovements");

            migrationBuilder.DropColumn(
                name: "SessionRestockId",
                table: "SessionStockMovements");
        }
    }
}
