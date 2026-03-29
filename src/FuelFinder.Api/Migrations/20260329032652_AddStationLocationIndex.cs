using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelFinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStationLocationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Stations_Latitude_Longitude",
                table: "Stations",
                columns: new[] { "Latitude", "Longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Stations_Latitude_Longitude",
                table: "Stations");
        }
    }
}
