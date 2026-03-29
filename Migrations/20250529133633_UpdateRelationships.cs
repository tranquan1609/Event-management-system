using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACSWEBSK.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Certificates_Attendees_AttendeeId",
                table: "Certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_Certificates_Events_EventId",
                table: "Certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Locations_LocationId",
                table: "Events");

            migrationBuilder.AddForeignKey(
                name: "FK_Certificates_Attendees_AttendeeId",
                table: "Certificates",
                column: "AttendeeId",
                principalTable: "Attendees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Certificates_Events_EventId",
                table: "Certificates",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Locations_LocationId",
                table: "Events",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Certificates_Attendees_AttendeeId",
                table: "Certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_Certificates_Events_EventId",
                table: "Certificates");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Locations_LocationId",
                table: "Events");

            migrationBuilder.AddForeignKey(
                name: "FK_Certificates_Attendees_AttendeeId",
                table: "Certificates",
                column: "AttendeeId",
                principalTable: "Attendees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Certificates_Events_EventId",
                table: "Certificates",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Locations_LocationId",
                table: "Events",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
