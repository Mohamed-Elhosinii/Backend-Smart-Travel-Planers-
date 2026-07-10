using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTravelPlaners.DAL.Migrations
{
    /// <inheritdoc />
    public partial class confirmcost_column : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConfirmedCost",
                table: "Trips",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfirmedCost",
                table: "Trips");
        }
    }
}
