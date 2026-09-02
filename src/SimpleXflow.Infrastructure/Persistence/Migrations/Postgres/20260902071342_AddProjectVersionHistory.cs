using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpleXflow.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddProjectVersionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    BpmnXml = table.Column<string>(type: "text", nullable: false),
                    LogicXml = table.Column<string>(type: "text", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectVersions_Projects_FlowProjectId",
                        column: x => x.FlowProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_FlowProjectId",
                table: "ProjectVersions",
                column: "FlowProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectVersions_TenantId_FlowProjectId_VersionNumber",
                table: "ProjectVersions",
                columns: new[] { "TenantId", "FlowProjectId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectVersions");
        }
    }
}
