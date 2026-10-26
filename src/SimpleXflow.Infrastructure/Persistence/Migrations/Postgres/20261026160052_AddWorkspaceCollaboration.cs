using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SimpleXflow.Infrastructure.Persistence.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddWorkspaceCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ProjectVersions",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "ProjectVersions",
                type: "character varying(240)",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlannedReleaseUtc",
                table: "ProjectVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReleasedBy",
                table: "ProjectVersions",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleasedUtc",
                table: "ProjectVersions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FolderPath",
                table: "Projects",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastEditedBy",
                table: "Projects",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Revision",
                table: "Projects",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkspaceProjectId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ModelEditingPresences",
                columns: table => new
                {
                    ModelId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LastSeenUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelEditingPresences", x => new { x.ModelId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ModelEditingPresences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ModelEditingPresences_Projects_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspaces_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Workspaces_Tenants_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    AssigneeUserId = table.Column<string>(type: "text", nullable: true),
                    DueUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ModelId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkPackages_AspNetUsers_AssigneeUserId",
                        column: x => x.AssigneeUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkPackages_Projects_ModelId",
                        column: x => x.ModelId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkPackages_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceMembers",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMembers", x => new { x.WorkspaceId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkspaceMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceMembers_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceProjects_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModelFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelFolders_WorkspaceProjects_WorkspaceProjectId",
                        column: x => x.WorkspaceProjectId,
                        principalTable: "WorkspaceProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.DropIndex(name: "IX_Projects_TenantId_Name", table: "Projects");
            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_Name",
                table: "Projects",
                columns: new[] { "TenantId", "Name" },
                unique: true,
                filter: "\"WorkspaceProjectId\" IS NULL");
            migrationBuilder.CreateIndex(
                name: "IX_Projects_WorkspaceProjectId_FolderPath_Name",
                table: "Projects",
                columns: new[] { "WorkspaceProjectId", "FolderPath", "Name" },
                unique: true,
                filter: "\"WorkspaceProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ModelEditingPresences_UserId",
                table: "ModelEditingPresences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelFolders_WorkspaceProjectId_Path",
                table: "ModelFolders",
                columns: new[] { "WorkspaceProjectId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkPackages_AssigneeUserId",
                table: "WorkPackages",
                column: "AssigneeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkPackages_ModelId",
                table: "WorkPackages",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkPackages_WorkspaceId",
                table: "WorkPackages",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_TokenHash",
                table: "WorkspaceInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_WorkspaceId",
                table: "WorkspaceInvitations",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_UserId",
                table: "WorkspaceMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceProjects_WorkspaceId_Name",
                table: "WorkspaceProjects",
                columns: new[] { "WorkspaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_OrganizationId",
                table: "Workspaces",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_OwnerUserId",
                table: "Workspaces",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_WorkspaceProjects_WorkspaceProjectId",
                table: "Projects",
                column: "WorkspaceProjectId",
                principalTable: "WorkspaceProjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_WorkspaceProjects_WorkspaceProjectId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ModelEditingPresences");

            migrationBuilder.DropTable(
                name: "ModelFolders");

            migrationBuilder.DropTable(
                name: "WorkPackages");

            migrationBuilder.DropTable(
                name: "WorkspaceInvitations");

            migrationBuilder.DropTable(
                name: "WorkspaceMembers");

            migrationBuilder.DropTable(
                name: "WorkspaceProjects");

            migrationBuilder.DropTable(
                name: "Workspaces");

            migrationBuilder.DropIndex(name: "IX_Projects_WorkspaceProjectId_FolderPath_Name", table: "Projects");
            migrationBuilder.DropIndex(name: "IX_Projects_TenantId_Name", table: "Projects");
            migrationBuilder.CreateIndex(
                name: "IX_Projects_TenantId_Name",
                table: "Projects",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ProjectVersions");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "ProjectVersions");

            migrationBuilder.DropColumn(
                name: "PlannedReleaseUtc",
                table: "ProjectVersions");

            migrationBuilder.DropColumn(
                name: "ReleasedBy",
                table: "ProjectVersions");

            migrationBuilder.DropColumn(
                name: "ReleasedUtc",
                table: "ProjectVersions");

            migrationBuilder.DropColumn(
                name: "FolderPath",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "LastEditedBy",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Revision",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "WorkspaceProjectId",
                table: "Projects");
        }
    }
}
