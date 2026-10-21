using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_VehicleScheduleDays",
                table: "VehicleScheduleDays");

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "VehicleScheduleDays",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_VehicleScheduleDays",
                table: "VehicleScheduleDays",
                columns: new[] { "VehicleId", "Day", "Position" });

            migrationBuilder.CreateTable(
                name: "DeliverySessionZones",
                columns: table => new
                {
                    DeliverySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliverySessionZones", x => new { x.DeliverySessionId, x.Position });
                    table.CheckConstraint("CK_DeliverySessionZones_Position", "\"Position\" >= 0");
                    table.ForeignKey(
                        name: "FK_DeliverySessionZones_DeliverySessions_DeliverySessionId",
                        column: x => x.DeliverySessionId,
                        principalTable: "DeliverySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliverySessionZones_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_VehicleScheduleDays_Position",
                table: "VehicleScheduleDays",
                sql: "\"Position\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_DeliverySessionZones_DeliverySessionId_ZoneId",
                table: "DeliverySessionZones",
                columns: new[] { "DeliverySessionId", "ZoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliverySessionZones_ZoneId",
                table: "DeliverySessionZones",
                column: "ZoneId");

            // Las salidas que ya existen (también las anuladas) recorrieron una sola zona:
            // queda como su primera y única. Sin esto, ninguna salida abierta tendría zonas.
            migrationBuilder.Sql("""
                INSERT INTO "DeliverySessionZones" ("DeliverySessionId", "ZoneId", "Position")
                SELECT "Id", "ZoneId", 0 FROM "DeliverySessions";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliverySessionZones");

            migrationBuilder.DropPrimaryKey(
                name: "PK_VehicleScheduleDays",
                table: "VehicleScheduleDays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_VehicleScheduleDays_Position",
                table: "VehicleScheduleDays");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "VehicleScheduleDays");

            migrationBuilder.AddPrimaryKey(
                name: "PK_VehicleScheduleDays",
                table: "VehicleScheduleDays",
                columns: new[] { "VehicleId", "Day" });
        }
    }
}
