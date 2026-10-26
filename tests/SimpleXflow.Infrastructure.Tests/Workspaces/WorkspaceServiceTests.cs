using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Workspaces;
using SimpleXflow.Domain.Workspaces;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Tests.Fakes;
using SimpleXflow.Infrastructure.Tests.Fixtures;
using SimpleXflow.Infrastructure.Workspaces;

namespace SimpleXflow.Infrastructure.Tests.Workspaces;

public sealed class WorkspaceServiceTests : IDisposable
{
    private readonly SqliteDatabase database = new();
    private readonly List<ApplicationDbContext> contexts = [];

    private WorkspaceService Service(string userId)
    {
        var tenantId = userId is "owner" or "admin" ? database.TenantA : database.TenantB;
        var context = database.CreateContext(tenantId, userId);
        contexts.Add(context);
        return new(context, new TestTenantContext(tenantId, userId));
    }

    private async Task<Guid> SeedAsync()
    {
        await using var setup = database.CreateContext();
        foreach (var id in new[] { "owner", "admin", "external", "unverified", "outsider" })
        {
            setup.Users.Add(new ApplicationUser
            {
                Id = id, UserName = id, Email = $"{id}@example.test", NormalizedEmail = $"{id}@example.test".ToUpperInvariant(),
                TenantId = id is "owner" or "admin" ? database.TenantA : database.TenantB,
                EmailConfirmed = id != "unverified"
            });
        }
        await setup.SaveChangesAsync();
        var workspaceId = await Service("owner").CreateWorkspaceAsync("Shared workspace");
        setup.WorkspaceMembers.Add(new() { WorkspaceId = workspaceId, UserId = "admin", Role = "Admin" });
        await setup.SaveChangesAsync();
        return workspaceId;
    }

    [Fact]
    public async Task Invitation_AddsExternalMemberWithoutChangingTheirOrganization_AndCannotBeReused()
    {
        var workspaceId = await SeedAsync();
        var invitation = await Service("owner").CreateInvitationAsync(workspaceId, "external@example.test", "Editor");
        Assert.Equal(workspaceId, await Service("external").AcceptInvitationAsync(invitation.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service("external").AcceptInvitationAsync(invitation.Token));
        var visible = Assert.Single(await Service("external").GetWorkspacesAsync());
        Assert.True(visible.IsShared);
        Assert.Equal(database.TenantA, visible.OrganizationId);
        Assert.Equal("Editor", visible.Role);
        await using var verify = database.CreateContext();
        Assert.Equal(database.TenantB, (await verify.Users.SingleAsync(user => user.Id == "external")).TenantId);
        Assert.Empty(await Service("outsider").GetWorkspacesAsync());
        Assert.Null(await Service("outsider").GetWorkspaceAsync(workspaceId));
    }

    [Fact]
    public async Task Invitation_RejectsUnverifiedAccountWrongAddressAndRevokedToken()
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        var unverified = await owner.CreateInvitationAsync(workspaceId, "unverified@example.test", "Editor");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service("unverified").AcceptInvitationAsync(unverified.Token));
        var invitation = await owner.CreateInvitationAsync(workspaceId, "external@example.test", "Editor");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service("outsider").AcceptInvitationAsync(invitation.Token));
        await owner.RevokeInvitationAsync(workspaceId, invitation.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service("external").AcceptInvitationAsync(invitation.Token));
        Assert.Empty(await Service("external").GetWorkspacesAsync());
    }

    [Fact]
    public async Task Invitation_RejectsExpiredToken()
    {
        var workspaceId = await SeedAsync();
        var invitation = await Service("owner").CreateInvitationAsync(workspaceId, "external@example.test", "Viewer");
        await using (var setup = database.CreateContext())
        {
            (await setup.WorkspaceInvitations.SingleAsync(item => item.Id == invitation.Id)).ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await setup.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service("external").AcceptInvitationAsync(invitation.Token));
        Assert.Empty(await Service("external").GetWorkspacesAsync());
    }

    [Fact]
    public async Task Admin_CannotChangeOwnerOrAdminOrReplaceOwnersAdminInvitation()
    {
        var workspaceId = await SeedAsync();
        var invitation = await Service("owner").CreateInvitationAsync(workspaceId, "external@example.test", "Admin");
        var admin = Service("admin");
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SetMemberRoleAsync(workspaceId, "owner", "Editor"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.SetMemberRoleAsync(workspaceId, "admin", "Viewer"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.RemoveMemberAsync(workspaceId, "owner"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.RevokeInvitationAsync(workspaceId, invitation.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => admin.CreateInvitationAsync(workspaceId, "external@example.test", "Editor"));
        await Service("external").AcceptInvitationAsync(invitation.Token);
        Assert.Equal("Admin", Assert.Single(await Service("external").GetWorkspacesAsync()).Role);
    }

    [Fact]
    public async Task Assignee_CanUpdateStatusButCannotDelegateOrChangeTheDeadline()
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        var invitation = await owner.CreateInvitationAsync(workspaceId, "external@example.test", "Editor");
        await Service("external").AcceptInvitationAsync(invitation.Token);
        var request = new WorkPackageRequest("Review model", "external", DateTimeOffset.UtcNow.AddDays(2));
        var packageId = await owner.CreateWorkPackageAsync(workspaceId, request);
        var assignee = Service("external");
        await Assert.ThrowsAsync<InvalidOperationException>(() => assignee.UpdateWorkPackageAsync(workspaceId, packageId, request with { DueUtc = request.DueUtc!.Value.AddDays(1) }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => assignee.CreateInvitationAsync(workspaceId, "outsider@example.test", "Viewer"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CreateWorkPackageAsync(workspaceId, request with { AssigneeUserId = "outsider" }));
        await assignee.UpdateWorkPackageAsync(workspaceId, packageId, request with { Status = "Done" });
        Assert.Equal("Done", Assert.Single((await owner.GetWorkspaceAsync(workspaceId))!.WorkPackages).Status);
        await owner.RemoveMemberAsync(workspaceId, "external");
        Assert.Null(Assert.Single((await owner.GetWorkspaceAsync(workspaceId))!.WorkPackages).AssigneeUserId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => assignee.UpdateWorkPackageAsync(workspaceId, packageId, request));
    }

    [Theory]
    [InlineData("models/ .. /outside")]
    [InlineData("models/ . /inside")]
    [InlineData("C:\\private")]
    public async Task Folder_RejectsTraversalAfterNormalization(string path)
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        var projectId = await owner.CreateProjectAsync(workspaceId, "Models");
        await Assert.ThrowsAsync<ArgumentException>(() => owner.CreateFolderAsync(projectId, path));
        await using var verify = database.CreateContext();
        Assert.Empty(await verify.ModelFolders.ToListAsync());
    }

    [Fact]
    public async Task LegacyMigration_PreservesModelsAndExistingOrganizationAccess_AndRunsOnce()
    {
        await SeedAsync();
        var modelId = await database.SeedProjectAsync(database.TenantA, "Legacy model", "<legacy/>");
        var owner = Service("owner");
        var first = await owner.GetWorkspacesAsync();
        var imported = Assert.Single(first, workspace => workspace.Name == "My workspace");
        Assert.Equal(first.Count, (await owner.GetWorkspacesAsync()).Count);
        var admin = await Service("admin").GetWorkspaceAsync(imported.Id);
        Assert.Equal("Editor", admin!.Role);
        Assert.Null(await Service("external").GetWorkspaceAsync(imported.Id));
        await using var verify = database.CreateContext();
        var model = await verify.Projects.SingleAsync(item => item.Id == modelId);
        Assert.Equal("<legacy/>", model.BpmnXml);
        Assert.NotNull(model.WorkspaceProjectId);
        Assert.Equal(1, model.Revision);
        Assert.Single(await verify.WorkspaceProjects.Where(project => project.WorkspaceId == imported.Id).ToListAsync());
    }

    public void Dispose()
    {
        foreach (var context in contexts) context.Dispose();
        database.Dispose();
    }
}
