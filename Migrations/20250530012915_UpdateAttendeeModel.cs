using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACSWEBSK.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAttendeeModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttendeeId1",
                table: "Certificates",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Attendees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_AttendeeId1",
                table: "Certificates",
                column: "AttendeeId1");

            migrationBuilder.AddForeignKey(
                name: "FK_Certificates_Attendees_AttendeeId1",
                table: "Certificates",
                column: "AttendeeId1",
                principalTable: "Attendees",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Certificates_Attendees_AttendeeId1",
                table: "Certificates");

            migrationBuilder.DropIndex(
                name: "IX_Certificates_AttendeeId1",
                table: "Certificates");

            migrationBuilder.DropColumn(
                name: "AttendeeId1",
                table: "Certificates");

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "Attendees",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);
        }
    }
}
