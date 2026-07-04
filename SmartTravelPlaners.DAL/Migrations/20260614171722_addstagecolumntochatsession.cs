using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTravelPlaners.DAL.Migrations
{
    /// <inheritdoc />
    public partial class addstagecolumntochatsession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_TripId",
                table: "ChatSessions");

            migrationBuilder.AlterColumn<Guid>(
                name: "TripId",
                table: "ChatSessions",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

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

            migrationBuilder.AlterColumn<Guid>(
                name: "TripId",
                table: "ChatSessions",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_TripId",
                table: "ChatSessions",
                column: "TripId",
                unique: true);
        }
    }
}
