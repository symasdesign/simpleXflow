using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpleXflow.Infrastructure.Persistence.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddProjectUndoSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousBpmnXml",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousLogicXml",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousName",
                table: "Projects",
                type: "nvarchar(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreviousUpdatedUtc",
                table: "Projects",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousBpmnXml",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PreviousLogicXml",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PreviousName",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PreviousUpdatedUtc",
                table: "Projects");
        }
    }
}
