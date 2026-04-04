using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FuelFinder.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                table: "Stations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StationPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FuelType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PricePerLitreCents = table.Column<decimal>(type: "decimal(10,1)", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StationPrices_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stations_State_ExternalId",
                table: "Stations",
                columns: new[] { "State", "ExternalId" });

            migrationBuilder.CreateIndex(
                name: "IX_StationPrices_RecordedAtUtc",
                table: "StationPrices",
                column: "RecordedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_StationPrices_Source",
                table: "StationPrices",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_StationPrices_StationId_FuelType",
                table: "StationPrices",
                columns: new[] { "StationId", "FuelType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StationPrices");

            migrationBuilder.DropIndex(
                name: "IX_Stations_State_ExternalId",
                table: "Stations");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "Stations");
        }
    }
}
