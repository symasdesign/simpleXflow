using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpleXflow.Infrastructure.Persistence.Migrations.Postgres
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
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousLogicXml",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousName",
                table: "Projects",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PreviousUpdatedUtc",
                table: "Projects",
                type: "timestamp with time zone",
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
