using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class VehicleKilometersCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VehicleKilometersCorrections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousKilometers = table.Column<int>(type: "integer", nullable: false),
                    NewKilometers = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CorrectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CorrectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleKilometersCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VehicleKilometersCorrections_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VehicleKilometersCorrections_VehicleId_CorrectedAt",
                table: "VehicleKilometersCorrections",
                columns: new[] { "VehicleId", "CorrectedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VehicleKilometersCorrections");
        }
    }
}
