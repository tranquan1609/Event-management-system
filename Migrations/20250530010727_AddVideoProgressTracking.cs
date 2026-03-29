using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACSWEBSK.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoProgressTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndTime",
                table: "Videos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "Videos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTime",
                table: "Videos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Videos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsEligibleForCertificate",
                table: "Attendees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VideoProgresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoId = table.Column<int>(type: "int", nullable: false),
                    AttendeeId = table.Column<int>(type: "int", nullable: false),
                    ViewDuration = table.Column<int>(type: "int", nullable: false),
                    ViewPercentage = table.Column<double>(type: "float", nullable: false),
                    LastViewedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    JoinTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LeaveTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalAttendanceTime = table.Column<int>(type: "int", nullable: true),
                    AttendancePercentage = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoProgresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoProgresses_Attendees_AttendeeId",
                        column: x => x.AttendeeId,
                        principalTable: "Attendees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_VideoProgresses_Videos_VideoId",
                        column: x => x.VideoId,
                        principalTable: "Videos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoProgresses_AttendeeId",
                table: "VideoProgresses",
                column: "AttendeeId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoProgresses_VideoId",
                table: "VideoProgresses",
                column: "VideoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VideoProgresses");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Videos");

            migrationBuilder.DropColumn(
                name: "IsEligibleForCertificate",
                table: "Attendees");
        }
    }
}
