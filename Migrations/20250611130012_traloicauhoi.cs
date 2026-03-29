using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACSWEBSK.Migrations
{
    /// <inheritdoc />
    public partial class traloicauhoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RepliedAt",
                table: "Questions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RepliedBy",
                table: "Questions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reply",
                table: "Questions",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RepliedAt",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "RepliedBy",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Reply",
                table: "Questions");
        }
    }
}
