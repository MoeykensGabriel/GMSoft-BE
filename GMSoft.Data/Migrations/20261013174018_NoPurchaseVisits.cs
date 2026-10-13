using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class NoPurchaseVisits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NoPurchaseReason",
                table: "Deliveries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "NoPurchaseRequestIds",
                table: "Deliveries",
                type: "uuid[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NoPurchaseReason",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "NoPurchaseRequestIds",
                table: "Deliveries");
        }
    }
}
