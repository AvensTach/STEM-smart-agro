using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartAgro.Backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialNew : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SensorId",
                table: "SensorReadings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Sensors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sensors", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Sensors",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Теплиця 1 (Томати)" },
                    { 2, "Поле 3 (Пшениця)" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Sensors");

            migrationBuilder.DropColumn(
                name: "SensorId",
                table: "SensorReadings");
        }
    }
}
