using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTravelPlaners.DAL.Migrations
{
    /// <inheritdoc />
    public partial class FixChatSessionTripIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_TripId",
                table: "ChatSessions");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_TripId",
                table: "ChatSessions",
                column: "TripId",
                unique: true,
                filter: "[TripId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_TripId",
                table: "ChatSessions");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_TripId",
                table: "ChatSessions",
                column: "TripId",
                unique: true);
        }
    }
}