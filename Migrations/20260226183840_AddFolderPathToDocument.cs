using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCloud.Migrations
{
    /// <inheritdoc />
    public partial class AddFolderPathToDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FolderPath",
                table: "Documents",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderPath",
                table: "Documents");
        }
    }
}
