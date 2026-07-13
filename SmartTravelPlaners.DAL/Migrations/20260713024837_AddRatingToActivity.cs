using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTravelPlaners.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingToActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Rating",
                table: "Hotels",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Rating",
                table: "Hotels");
        }
    }
}
