using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTravelPlaners.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelCachingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAt",
                table: "PlacesCache",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedQuery",
                table: "PlacesCache",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExternalApiCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CacheKey = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalApiCaches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlacesCache_NormalizedQuery",
                table: "PlacesCache",
                column: "NormalizedQuery");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalApiCaches_CacheKey_Source",
                table: "ExternalApiCaches",
                columns: new[] { "CacheKey", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalApiCaches");

            migrationBuilder.DropIndex(
                name: "IX_PlacesCache_NormalizedQuery",
                table: "PlacesCache");

            migrationBuilder.DropColumn(
                name: "LastUsedAt",
                table: "PlacesCache");

            migrationBuilder.DropColumn(
                name: "NormalizedQuery",
                table: "PlacesCache");
        }
    }
}
