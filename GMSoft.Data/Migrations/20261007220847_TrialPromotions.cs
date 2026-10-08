using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class TrialPromotions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContactName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VisitDays = table.Column<int[]>(type: "integer[]", nullable: false),
                    VehicleId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliverySessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PickupDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CloseClientRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByDriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosingSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Promotions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Promotions_DeliverySessions_ClosingSessionId",
                        column: x => x.ClosingSessionId,
                        principalTable: "DeliverySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Promotions_DeliverySessions_DeliverySessionId",
                        column: x => x.DeliverySessionId,
                        principalTable: "DeliverySessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Promotions_Drivers_ClosedByDriverId",
                        column: x => x.ClosedByDriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Promotions_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Promotions_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Promotions_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PickupDays = table.Column<int>(type: "integer", nullable: false, defaultValue: 7)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionSettings", x => x.Id);
                    table.CheckConstraint("CK_PromotionSettings_PickupDays", "\"PickupDays\" > 0");
                    table.CheckConstraint("CK_PromotionSettings_Singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateTable(
                name: "PromotionContainerMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContainerMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionContainerMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PromotionContainerMovements_ContainerMovements_ContainerMov~",
                        column: x => x.ContainerMovementId,
                        principalTable: "ContainerMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromotionContainerMovements_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromotionLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ContainersLoaned = table.Column<int>(type: "integer", nullable: false),
                    ContainersReturned = table.Column<int>(type: "integer", nullable: false),
                    ContainersLost = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionLines", x => x.Id);
                    table.CheckConstraint("CK_PromotionLines_Quantities", "\"Quantity\" > 0 AND \"ContainersLoaned\" >= 0 AND \"ContainersLoaned\" <= \"Quantity\" AND \"ContainersReturned\" >= 0 AND \"ContainersLost\" >= 0 AND \"ContainersReturned\" + \"ContainersLost\" <= \"ContainersLoaned\"");
                    table.ForeignKey(
                        name: "FK_PromotionLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromotionLines_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionContainerMovements_ContainerMovementId",
                table: "PromotionContainerMovements",
                column: "ContainerMovementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PromotionContainerMovements_PromotionId",
                table: "PromotionContainerMovements",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionLines_ProductId",
                table: "PromotionLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_PromotionLines_PromotionId_ProductId",
                table: "PromotionLines",
                columns: new[] { "PromotionId", "ProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ClientRequestId",
                table: "Promotions",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CloseClientRequestId",
                table: "Promotions",
                column: "CloseClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ClosedByDriverId",
                table: "Promotions",
                column: "ClosedByDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ClosingSessionId",
                table: "Promotions",
                column: "ClosingSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_CustomerId",
                table: "Promotions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_DeliverySessionId",
                table: "Promotions",
                column: "DeliverySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_DriverId",
                table: "Promotions",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_RegisteredAt",
                table: "Promotions",
                column: "RegisteredAt");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_VehicleId_Status_PickupDate",
                table: "Promotions",
                columns: new[] { "VehicleId", "Status", "PickupDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_ZoneId",
                table: "Promotions",
                column: "ZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromotionContainerMovements");

            migrationBuilder.DropTable(
                name: "PromotionLines");

            migrationBuilder.DropTable(
                name: "PromotionSettings");

            migrationBuilder.DropTable(
                name: "Promotions");
        }
    }
}
