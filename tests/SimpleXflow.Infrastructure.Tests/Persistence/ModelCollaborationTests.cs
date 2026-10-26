using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Projects;
using SimpleXflow.Domain.Workspaces;
using SimpleXflow.Infrastructure.Identity;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Projects;
using SimpleXflow.Infrastructure.Tests.Fakes;
using SimpleXflow.Infrastructure.Tests.Fixtures;
using SimpleXflow.Infrastructure.Workspaces;

namespace SimpleXflow.Infrastructure.Tests.Persistence;

public sealed class ModelCollaborationTests : IDisposable
{
    private readonly SqliteDatabase database = new();
    private readonly string owner = "owner";
    private readonly string guest = "guest";

    [Theory]
    [InlineData("Editor", true)]
    [InlineData("Viewer", false)]
    public async Task SharedMembers_ReadModelButOnlyEditorsCanSave(string role, bool canEdit)
    {
        var (workspace, project, model) = await ArrangeAsync(role);
        await using var context = database.CreateContext(database.TenantB, guest);
        var service = Service(context, database.TenantB, guest);
        var detail = await service.GetProjectAsync(model);
        Assert.NotNull(detail);
        Assert.Equal(workspace, detail.WorkspaceId);
        Assert.Equal(canEdit, detail.CanEdit);
        Assert.False(detail.CanManage);
        Assert.Single(await service.GetProjectsAsync());
        if (canEdit)
        {
            Assert.Equal(1, await service.UpdateProjectAsync(model, new("Shared", "<changed />", "<logic />", detail.Revision)));
            Assert.Equal("guest@example.test", (await service.GetProjectAsync(model))!.LastEditedBy);
            Assert.Equal(2, (await service.GetVersionsAsync(model)).Count);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateProjectAsync(model, new("Shared", "<changed />", null, detail.Revision)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateProjectAsync(new("Illegal", "<model />", WorkspaceProjectId: project)));
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteProjectAsync(model, 0));
    }

    [Fact]
    public async Task SameOrganizationWithoutMembership_CannotReadOrModifyWorkspaceModel()
    {
        var (_, _, model) = await ArrangeAsync();
        await using (var setup = database.CreateContext())
        {
            setup.Users.Add(User("colleague", database.TenantA));
            await setup.SaveChangesAsync();
        }
        await using var context = database.CreateContext(database.TenantA, "colleague");
        var service = Service(context, database.TenantA, "colleague");
        Assert.Empty(await service.GetProjectsAsync());
        Assert.Null(await service.GetProjectAsync(model));
        Assert.Empty(await context.ProjectVersions.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateProjectAsync(model, new("Illegal", "<xml />", null, 0)));
    }

    [Fact]
    public async Task StaleSave_IsRejectedAndDoesNotOverwriteModelOrAddHistory()
    {
        var (_, _, model) = await ArrangeAsync();
        await using var first = database.CreateContext(database.TenantA, owner);
        await using var second = database.CreateContext(database.TenantB, guest);
        var firstService = Service(first, database.TenantA, owner);
        var secondService = Service(second, database.TenantB, guest);
        var stale = await secondService.GetProjectAsync(model);
        Assert.Equal(1, await firstService.UpdateProjectAsync(model, new("Shared", "<first />", null, 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => secondService.UpdateProjectAsync(model, new("Shared", "<stale />", null, stale!.Revision)));
        Assert.Equal("<first />", (await secondService.GetProjectAsync(model))!.BpmnXml);
        Assert.Equal(2, (await secondService.GetVersionsAsync(model)).Count);
        await Assert.ThrowsAsync<InvalidOperationException>(() => secondService.UpdateProjectAsync(model, new("Shared", "<no-revision />", null)));
    }

    [Fact]
    public async Task RevokingMember_RemovesAccessInAlreadyOpenCircuit()
    {
        var (workspace, _, model) = await ArrangeAsync();
        await using var context = database.CreateContext(database.TenantB, guest);
        var service = Service(context, database.TenantB, guest);
        Assert.NotNull(await service.GetProjectAsync(model));
        await using (var admin = database.CreateContext(database.TenantA, owner))
        {
            await new WorkspaceService(admin, new TestTenantContext(database.TenantA, owner)).RemoveMemberAsync(workspace, guest);
        }
        Assert.Null(await service.GetProjectAsync(model));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateProjectAsync(model, new("Shared", "<revoked />", null, 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.HeartbeatAsync(model));
    }

    [Fact]
    public async Task RestoreAndRelease_RequireAdminAndPreserveCompleteHistory()
    {
        var (_, _, model) = await ArrangeAsync();
        await using var context = database.CreateContext(database.TenantA, owner);
        var service = Service(context, database.TenantA, owner);
        var original = Assert.Single(await service.GetVersionsAsync(model));
        await service.UpdateProjectAsync(model, new("Shared", "<changed />", null, 0));
        Assert.Equal(2, await service.RestoreVersionAsync(model, original.Id, 1));
        Assert.Equal("<original />", (await service.GetProjectAsync(model))!.BpmnXml);
        Assert.Equal(3, (await service.GetVersionsAsync(model)).Count);
        await service.ReleaseVersionAsync(model, original.Id);
        Assert.NotNull((await service.GetVersionsAsync(model)).Single(item => item.Id == original.Id).ReleasedUtc);
        await using var guestContext = database.CreateContext(database.TenantB, guest);
        var guestService = Service(guestContext, database.TenantB, guest);
        await Assert.ThrowsAsync<InvalidOperationException>(() => guestService.RestoreVersionAsync(model, original.Id, 2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => guestService.ReleaseVersionAsync(model, original.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => guestService.CreateVersionAsync(model, "Release", DateTimeOffset.UtcNow.AddDays(1)));
    }

    [Fact]
    public async Task DeleteChecksRevision_AndCascadesVersionsAndPresence()
    {
        var (_, _, model) = await ArrangeAsync();
        await using var context = database.CreateContext(database.TenantA, owner);
        var service = Service(context, database.TenantA, owner);
        await service.HeartbeatAsync(model);
        await service.UpdateProjectAsync(model, new("Shared", "<changed />", null, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteProjectAsync(model, 0));
        await service.DeleteProjectAsync(model, 1);
        await using var verification = database.CreateContext();
        Assert.Empty(await verification.Projects.ToListAsync());
        Assert.Empty(await verification.ProjectVersions.ToListAsync());
        Assert.Empty(await verification.ModelEditingPresences.ToListAsync());
    }

    [Fact]
    public async Task PresenceShowsActiveOtherEditorsAndExpires()
    {
        var (_, _, model) = await ArrangeAsync();
        await using var ownerContext = database.CreateContext(database.TenantA, owner);
        await using var guestContext = database.CreateContext(database.TenantB, guest);
        var ownerService = Service(ownerContext, database.TenantA, owner);
        var guestService = Service(guestContext, database.TenantB, guest);
        Assert.Empty(await ownerService.HeartbeatAsync(model));
        Assert.Equal(owner, Assert.Single(await guestService.HeartbeatAsync(model)).UserId);
        Assert.Equal(guest, Assert.Single(await ownerService.HeartbeatAsync(model)).UserId);
        await using (var setup = database.CreateContext())
        {
            (await setup.ModelEditingPresences.SingleAsync(item => item.UserId == guest)).LastSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
            await setup.SaveChangesAsync();
        }
        Assert.Empty(await ownerService.HeartbeatAsync(model));
        await ownerService.LeaveModelAsync(model);
        Assert.Empty(await guestService.HeartbeatAsync(model));
    }

    [Fact]
    public async Task DefaultCreateForAuthenticatedUser_AlwaysCreatesContainedModel()
    {
        await SeedUsersAsync();
        await using var context = database.CreateContext(database.TenantA, owner);
        var service = Service(context, database.TenantA, owner);
        var model = await service.CreateProjectAsync(new("Demo", "<demo />"));
        var detail = await service.GetProjectAsync(model);
        Assert.NotNull(detail!.WorkspaceId);
        Assert.NotNull(detail.WorkspaceProjectId);
        Assert.Single(await context.Workspaces.ToListAsync());
        Assert.Single(await context.WorkspaceMembers.ToListAsync());
        await service.CreateProjectAsync(new("Second", "<second />"));
        Assert.Single(await context.Workspaces.ToListAsync());
        Assert.Single(await context.WorkspaceProjects.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentOperationsOnSameCircuit_AreSerialized()
    {
        var (workspace, _, model) = await ArrangeAsync();
        await using var context = database.CreateContext(database.TenantA, owner);
        var service = Service(context, database.TenantA, owner);
        var workspaces = new WorkspaceService(context, new TestTenantContext(database.TenantA, owner));
        await Task.WhenAll(service.HeartbeatAsync(model), service.GetVersionsAsync(model), workspaces.GetWorkspaceAsync(workspace), service.GetProjectsAsync());
    }

    [Fact]
    public async Task ModelNames_AreUniqueOnlyWithinProjectAndFolder()
    {
        var (workspace, project, _) = await ArrangeAsync();
        await using var context = database.CreateContext(database.TenantA, owner);
        var service = Service(context, database.TenantA, owner);
        var workspaces = new WorkspaceService(context, new TestTenantContext(database.TenantA, owner));
        var otherProject = await workspaces.CreateProjectAsync(workspace, "Other project");
        var inFolder = await service.CreateProjectAsync(new("Shared", "<folder />", WorkspaceProjectId: project, FolderPath: "Design"));
        var inProject = await service.CreateProjectAsync(new("Shared", "<project />", WorkspaceProjectId: otherProject));
        var sameLocation = await service.CreateProjectAsync(new("Shared", "<duplicate />", WorkspaceProjectId: project));
        Assert.Equal("Shared", (await service.GetProjectAsync(inFolder))!.Name);
        Assert.Equal("Shared", (await service.GetProjectAsync(inProject))!.Name);
        Assert.Equal("Shared (1)", (await service.GetProjectAsync(sameLocation))!.Name);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateProjectAsync(inFolder,
            new("Shared", "<move />", null, 0, "")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateProjectAsync(sameLocation,
            new("Shared", "<rename />", null, 0)));
        context.Projects.Add(new SimpleXflow.Domain.Projects.FlowProject(database.TenantA, "Shared", "<invalid />") { WorkspaceProjectId = project });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private async Task<(Guid Workspace, Guid Project, Guid Model)> ArrangeAsync(string role = "Editor")
    {
        await SeedUsersAsync();
        await using var context = database.CreateContext(database.TenantA, owner);
        var workspaces = new WorkspaceService(context, new TestTenantContext(database.TenantA, owner));
        var workspace = await workspaces.CreateWorkspaceAsync("Team");
        context.WorkspaceMembers.Add(new WorkspaceMember { WorkspaceId = workspace, UserId = guest, Role = role });
        await context.SaveChangesAsync();
        var project = await workspaces.CreateProjectAsync(workspace, "Project");
        var model = await Service(context, database.TenantA, owner).CreateProjectAsync(new("Shared", "<original />", WorkspaceProjectId: project));
        return (workspace, project, model);
    }

    private async Task SeedUsersAsync()
    {
        await using var context = database.CreateContext();
        context.Users.AddRange(User(owner, database.TenantA), User(guest, database.TenantB));
        await context.SaveChangesAsync();
    }

    private static ApplicationUser User(string id, Guid tenantId) => new()
    {
        Id = id, UserName = id + "@example.test", Email = id + "@example.test", NormalizedEmail = (id + "@example.test").ToUpperInvariant(),
        NormalizedUserName = (id + "@example.test").ToUpperInvariant(), TenantId = tenantId, EmailConfirmed = true
    };
    private static FlowProjectService Service(ApplicationDbContext context, Guid tenant, string user) => new(context, new TestTenantContext(tenant, user));
    public void Dispose() => database.Dispose();
}
