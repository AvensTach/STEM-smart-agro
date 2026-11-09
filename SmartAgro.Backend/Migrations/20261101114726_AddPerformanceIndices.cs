using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartAgro.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SensorReadingsHistory_CreatedAt",
                table: "SensorReadingsHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_CreatedAt",
                table: "SensorReadings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SensorReadings_SensorId",
                table: "SensorReadings",
                column: "SensorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SensorReadingsHistory_CreatedAt",
                table: "SensorReadingsHistory");

            migrationBuilder.DropIndex(
                name: "IX_SensorReadings_CreatedAt",
                table: "SensorReadings");

            migrationBuilder.DropIndex(
                name: "IX_SensorReadings_SensorId",
                table: "SensorReadings");
        }
    }
}
