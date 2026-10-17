using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GMSoft.Data.Migrations
{
    /// <inheritdoc />
    public partial class LendableProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanBeLent",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanBeLent",
                table: "Products");
        }
    }
}
