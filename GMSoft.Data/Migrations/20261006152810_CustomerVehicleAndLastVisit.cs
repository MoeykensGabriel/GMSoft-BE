using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerVehicleAndLastVisit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastVisitAt",
                table: "Customers",
                type: "timestamp with time zone",
                nullable: true);

            // Recuperar la última visita real desde el historial. No inferir camión habitual.
            migrationBuilder.Sql("""
                UPDATE "Customers" AS c
                SET "LastVisitAt" = d."LastVisitAt"
                FROM (
                    SELECT "CustomerId", MAX("DeliveredAt") AS "LastVisitAt"
                    FROM "Deliveries"
                    GROUP BY "CustomerId"
                ) AS d
                WHERE c."Id" = d."CustomerId";
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "VehicleId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_VehicleId_ZoneId_RouteOrder",
                table: "Customers",
                columns: new[] { "VehicleId", "ZoneId", "RouteOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Vehicles_VehicleId",
                table: "Customers",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Vehicles_VehicleId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_VehicleId_ZoneId_RouteOrder",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "LastVisitAt",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "Customers");
        }
    }
}
