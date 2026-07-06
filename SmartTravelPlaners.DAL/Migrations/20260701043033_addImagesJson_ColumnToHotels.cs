using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTravelPlaners.DAL.Migrations
{
    /// <inheritdoc />
    public partial class addImagesJson_ColumnToHotels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagesJson",
                table: "Hotels",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagesJson",
                table: "Hotels");
        }
    }
}
