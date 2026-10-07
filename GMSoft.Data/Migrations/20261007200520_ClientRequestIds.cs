using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClientRequestIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "VehicleLoads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "Deliveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VehicleLoads_ClientRequestId",
                table: "VehicleLoads",
                column: "ClientRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_ClientRequestId",
                table: "Deliveries",
                column: "ClientRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VehicleLoads_ClientRequestId",
                table: "VehicleLoads");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_ClientRequestId",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "VehicleLoads");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Deliveries");
        }
    }
}
