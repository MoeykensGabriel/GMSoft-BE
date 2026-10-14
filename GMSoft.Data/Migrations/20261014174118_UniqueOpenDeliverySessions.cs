using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueOpenDeliverySessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No se corrigen ni eliminan datos automaticamente: el operador debe
            // resolver las salidas duplicadas antes de volver a aplicar la migracion.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "DeliverySessions" WHERE "Status" = 0
                               GROUP BY "VehicleId" HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'No se pueden crear los indices: hay mas de una salida abierta para el mismo camion. Resolver las salidas duplicadas y reintentar; no se borraron datos.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "DeliverySessions" WHERE "Status" = 0
                               GROUP BY "DriverId" HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'No se pueden crear los indices: hay mas de una salida abierta para el mismo chofer. Resolver las salidas duplicadas y reintentar; no se borraron datos.';
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_DeliverySessions_OpenDriver",
                table: "DeliverySessions",
                column: "DriverId",
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DeliverySessions_OpenVehicle",
                table: "DeliverySessions",
                column: "VehicleId",
                unique: true,
                filter: "\"Status\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeliverySessions_OpenDriver",
                table: "DeliverySessions");

            migrationBuilder.DropIndex(
                name: "IX_DeliverySessions_OpenVehicle",
                table: "DeliverySessions");
        }
    }
}
