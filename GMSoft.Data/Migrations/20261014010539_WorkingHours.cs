using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkingHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOutsideWorkingHours",
                table: "VehicleLoads",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutsideWorkingHours",
                table: "SessionRestocks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutsideWorkingHours",
                table: "SessionCashSettlements",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ClosedOutsideWorkingHours",
                table: "Promotions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutsideWorkingHours",
                table: "Promotions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                table: "DeliverySessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ClosedOutsideWorkingHours",
                table: "DeliverySessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OpenedByUserId",
                table: "DeliverySessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OpenedOutsideWorkingHours",
                table: "DeliverySessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOutsideWorkingHours",
                table: "Deliveries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "WorkingHoursSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Days = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingHoursSettings", x => x.Id);
                    table.CheckConstraint("CK_WorkingHoursSettings_Singleton", "\"Id\" = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkingHoursSettings");

            migrationBuilder.DropColumn(
                name: "IsOutsideWorkingHours",
                table: "VehicleLoads");

            migrationBuilder.DropColumn(
                name: "IsOutsideWorkingHours",
                table: "SessionRestocks");

            migrationBuilder.DropColumn(
                name: "IsOutsideWorkingHours",
                table: "SessionCashSettlements");

            migrationBuilder.DropColumn(
                name: "ClosedOutsideWorkingHours",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "IsOutsideWorkingHours",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "DeliverySessions");

            migrationBuilder.DropColumn(
                name: "ClosedOutsideWorkingHours",
                table: "DeliverySessions");

            migrationBuilder.DropColumn(
                name: "OpenedByUserId",
                table: "DeliverySessions");

            migrationBuilder.DropColumn(
                name: "OpenedOutsideWorkingHours",
                table: "DeliverySessions");

            migrationBuilder.DropColumn(
                name: "IsOutsideWorkingHours",
                table: "Deliveries");
        }
    }
}
