using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Workspaces;
using SimpleXflow.Domain.Workspaces;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Projects;
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

    [Fact]
    public async Task InvitationReplacement_RevokesOldTokenAndStoresOnlyItsHash()
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        var old = await owner.CreateInvitationAsync(workspaceId, "external@example.test", "Editor");
        var replacement = await owner.CreateInvitationAsync(workspaceId, " EXTERNAL@example.test ", "Viewer");

        Assert.NotEqual(old.Token, replacement.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service("external").AcceptInvitationAsync(old.Token));
        Assert.Equal(replacement.Id, Assert.Single((await owner.GetWorkspaceAsync(workspaceId))!.Invitations).Id);
        await Service("external").AcceptInvitationAsync(replacement.Token);
        Assert.Equal("Viewer", Assert.Single(await Service("external").GetWorkspacesAsync()).Role);

        await using var verify = database.CreateContext();
        var persisted = await verify.WorkspaceInvitations.SingleAsync(item => item.Id == replacement.Id);
        Assert.NotEqual(replacement.Token, persisted.TokenHash);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(replacement.Token))), persisted.TokenHash);
        Assert.NotNull(persisted.AcceptedUtc);
    }

    [Theory]
    [InlineData("Viewer")]
    [InlineData("Editor")]
    public async Task NonManagers_CannotChangeMembershipOrSeePendingInvitations(string role)
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        await Service("external").AcceptInvitationAsync((await owner.CreateInvitationAsync(workspaceId, "external@example.test", role)).Token);
        var pending = await owner.CreateInvitationAsync(workspaceId, "outsider@example.test", "Editor");
        var member = Service("external");

        Assert.Empty((await member.GetWorkspaceAsync(workspaceId))!.Invitations);
        await Assert.ThrowsAsync<InvalidOperationException>(() => member.SetMemberRoleAsync(workspaceId, "external", "Admin"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => member.RemoveMemberAsync(workspaceId, "admin"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => member.RevokeInvitationAsync(workspaceId, pending.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => member.CreateWorkPackageAsync(workspaceId, new("Unauthorized", "external", null)));

        var unchanged = (await owner.GetWorkspaceAsync(workspaceId))!;
        Assert.Equal(role, unchanged.Members.Single(item => item.UserId == "external").Role);
        Assert.Contains(unchanged.Members, item => item.UserId == "admin" && item.Role == "Admin");
        Assert.Equal(pending.Id, Assert.Single(unchanged.Invitations).Id);
        Assert.Empty(unchanged.WorkPackages);
    }

    [Fact]
    public async Task Viewer_CannotCreateProjectsOrFolders()
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        var projectId = await owner.CreateProjectAsync(workspaceId, "Existing");
        await Service("external").AcceptInvitationAsync((await owner.CreateInvitationAsync(workspaceId, "external@example.test", "Viewer")).Token);
        var viewer = Service("external");

        await Assert.ThrowsAsync<InvalidOperationException>(() => viewer.CreateProjectAsync(workspaceId, "Unauthorized"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => viewer.CreateFolderAsync(projectId, "Unauthorized"));

        var project = Assert.Single((await owner.GetWorkspaceAsync(workspaceId))!.Projects);
        Assert.Equal(projectId, project.Id);
        Assert.Empty(project.Folders);
    }

    [Fact]
    public async Task FolderHierarchy_NormalizesSeparatorsAndCreatesEachAncestorOnce()
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        var projectId = await owner.CreateProjectAsync(workspaceId, "Models");

        await owner.CreateFolderAsync(projectId, " Design \\ Processes // Draft ");
        await owner.CreateFolderAsync(projectId, "Design/Processes/Draft");
        await owner.CreateFolderAsync(projectId, "Design/Processes/Released");

        Assert.Equal(new[] { "Design", "Design/Processes", "Design/Processes/Draft", "Design/Processes/Released" },
            Assert.Single((await owner.GetWorkspaceAsync(workspaceId))!.Projects).Folders);
        await using var verify = database.CreateContext();
        Assert.Equal(4, await verify.ModelFolders.CountAsync());
    }

    [Fact]
    public async Task ProjectNames_AreUniqueWithinWorkspaceButReusableAcrossWorkspaces()
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        await owner.CreateProjectAsync(workspaceId, "Models");
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CreateProjectAsync(workspaceId, " Models "));
        var otherWorkspace = await owner.CreateWorkspaceAsync("Other workspace");
        await owner.CreateProjectAsync(otherWorkspace, "Models");

        Assert.Single((await owner.GetWorkspaceAsync(workspaceId))!.Projects);
        Assert.Single((await owner.GetWorkspaceAsync(otherWorkspace))!.Projects);
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("Assignee")]
    [InlineData("Description")]
    [InlineData("Model")]
    public async Task Assignee_CannotChangeDelegatedScope(string changedField)
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        await Service("external").AcceptInvitationAsync((await owner.CreateInvitationAsync(workspaceId, "external@example.test", "Editor")).Token);
        var request = new WorkPackageRequest("Review", "external", DateTimeOffset.UtcNow.AddDays(1), Description: "Check the design");
        var id = await owner.CreateWorkPackageAsync(workspaceId, request);
        var changed = changedField switch
        {
            "Title" => request with { Title = "Different work" },
            "Assignee" => request with { AssigneeUserId = "admin" },
            "Description" => request with { Description = "Different scope" },
            _ => request with { ModelId = Guid.NewGuid() }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => Service("external").UpdateWorkPackageAsync(workspaceId, id, changed));

        var persisted = Assert.Single((await owner.GetWorkspaceAsync(workspaceId))!.WorkPackages);
        Assert.Equal(request.Title, persisted.Title);
        Assert.Equal(request.AssigneeUserId, persisted.AssigneeUserId);
        Assert.Equal(request.Description, persisted.Description);
        Assert.Null(persisted.ModelId);
        Assert.Equal(request.DueUtc, persisted.DueUtc);
    }

    [Fact]
    public async Task WorkPackage_CannotReferenceModelOrPackageFromAnotherWorkspace()
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        var otherWorkspace = await owner.CreateWorkspaceAsync("Other");
        var otherProject = await owner.CreateProjectAsync(otherWorkspace, "Other models");
        await using var context = database.CreateContext(database.TenantA, "owner");
        var models = new FlowProjectService(context, new TestTenantContext(database.TenantA, "owner"));
        var otherModel = await models.CreateProjectAsync(new("Other model", "<other />", WorkspaceProjectId: otherProject));
        var request = new WorkPackageRequest("Review", "owner", null, ModelId: otherModel);
        var otherPackage = await owner.CreateWorkPackageAsync(otherWorkspace, request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.CreateWorkPackageAsync(workspaceId, request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.UpdateWorkPackageAsync(workspaceId, otherPackage, request with { Title = "Changed" }));

        Assert.Empty((await owner.GetWorkspaceAsync(workspaceId))!.WorkPackages);
        Assert.Equal("Review", Assert.Single((await owner.GetWorkspaceAsync(otherWorkspace))!.WorkPackages).Title);
    }

    [Theory]
    [InlineData("Unknown", 0)]
    [InlineData("Open", 4001)]
    public async Task InvalidWorkPackageUpdate_PreservesExistingData(string status, int descriptionLength)
    {
        var workspaceId = await SeedAsync();
        var owner = Service("owner");
        var request = new WorkPackageRequest("Review", "owner", DateTimeOffset.UtcNow.AddDays(1));
        var id = await owner.CreateWorkPackageAsync(workspaceId, request);

        await Assert.ThrowsAsync<ArgumentException>(() => owner.UpdateWorkPackageAsync(workspaceId, id,
            request with { Title = "Must not be persisted", Status = status, Description = new string('x', descriptionLength) }));
        await owner.CreateProjectAsync(workspaceId, "Save another entity");

        var persisted = Assert.Single((await Service("owner").GetWorkspaceAsync(workspaceId))!.WorkPackages);
        Assert.Equal(request.Title, persisted.Title);
        Assert.Equal(request.Status, persisted.Status);
        Assert.Null(persisted.Description);
    }

    public void Dispose()
    {
        foreach (var context in contexts) context.Dispose();
        database.Dispose();
    }
}
