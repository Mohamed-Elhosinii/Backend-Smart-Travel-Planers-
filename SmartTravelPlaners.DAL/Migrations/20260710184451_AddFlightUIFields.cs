using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTravelPlaners.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightUIFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AirlineCode",
                table: "Flights",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArrivalTerminal",
                table: "Flights",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartureTerminal",
                table: "Flights",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AirlineCode",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "ArrivalTerminal",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "DepartureTerminal",
                table: "Flights");
        }
    }
}
