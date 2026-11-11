using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class VehicleKilometersCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "KilometersCheckPending",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "KilometersCheckedAt",
                table: "Vehicles",
                type: "timestamp with time zone",
                nullable: true);

            // El km de los camiones que ya existen no es confiable: la próxima salida de
            // cada uno pide leerlo del tablero y la oficina lo revisa.
            migrationBuilder.Sql("UPDATE \"Vehicles\" SET \"KilometersCheckPending\" = true;");

            migrationBuilder.CreateTable(
                name: "VehicleKilometersReadings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliverySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kilometers = table.Column<int>(type: "integer", nullable: false),
                    PreviousKilometers = table.Column<int>(type: "integer", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleKilometersReadings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleKilometersReadings_DeliverySessions_DeliverySessionId",
                        column: x => x.DeliverySessionId,
                        principalTable: "DeliverySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VehicleKilometersReadings_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleKilometersReadings_DeliverySessionId",
                table: "VehicleKilometersReadings",
                column: "DeliverySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleKilometersReadings_VehicleId_Status",
                table: "VehicleKilometersReadings",
                columns: new[] { "VehicleId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleKilometersReadings");

            migrationBuilder.DropColumn(
                name: "KilometersCheckPending",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "KilometersCheckedAt",
                table: "Vehicles");
        }
    }
}
