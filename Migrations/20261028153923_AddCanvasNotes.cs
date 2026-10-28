using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollabSpace.Migrations
{
    /// <inheritdoc />
    public partial class AddCanvasNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanvasNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    Content = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    X = table.Column<double>(type: "double", nullable: false),
                    Y = table.Column<double>(type: "double", nullable: false),
                    Width = table.Column<double>(type: "double", nullable: false),
                    Height = table.Column<double>(type: "double", nullable: false),
                    Color = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanvasNotes_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasNotes_RoomId",
                table: "CanvasNotes",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasNotes_RoomId_Id",
                table: "CanvasNotes",
                columns: new[] { "RoomId", "Id" });
            migrationBuilder.Sql(
                """
                INSERT INTO CanvasNotes
                    (RoomId, Content, X, Y, Width, Height, Color, CreatedAt, UpdatedAt)
                SELECT
                    RoomId,
                    Content,
                    100.0,
                    100.0,
                    320.0,
                    220.0,
                    '#fff3a3',
                    UpdatedAt,
                    UpdatedAt
                FROM RoomNotes;
                """);
		}

		/// <inheritdoc />
		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.DropTable(
					name: "CanvasNotes");
		}
	}
}
