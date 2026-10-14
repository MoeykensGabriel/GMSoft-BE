using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class CustomerActivitySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerActivitySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    RedAfterMissedWeeks = table.Column<int>(type: "integer", nullable: false),
                    BlackAfterMissedWeeks = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerActivitySettings", x => x.Id);
                    table.CheckConstraint("CK_CustomerActivitySettings_Order", "\"RedAfterMissedWeeks\" > 0 AND \"BlackAfterMissedWeeks\" > \"RedAfterMissedWeeks\"");
                    table.CheckConstraint("CK_CustomerActivitySettings_Singleton", "\"Id\" = 1");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerActivitySettings");
        }
    }
}
