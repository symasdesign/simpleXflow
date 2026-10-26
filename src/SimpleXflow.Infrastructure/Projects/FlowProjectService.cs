using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Application.Projects;
using SimpleXflow.Domain.Projects;
using SimpleXflow.Domain.Workspaces;
using SimpleXflow.Infrastructure.Persistence;
using SimpleXflow.Infrastructure.Workspaces;

namespace SimpleXflow.Infrastructure.Projects;

public sealed class FlowProjectService(ApplicationDbContext dbContext, ITenantContext tenantContext) : IFlowProjectService
{
    private readonly WorkspaceAccess access = new(dbContext, tenantContext);

    public async Task<IReadOnlyList<ProjectSummary>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        EnsureTenant();
        var models = await dbContext.Projects.AsNoTracking().Select(model => new ProjectSummary(model.Id, model.Name, model.UpdatedUtc,
            model.WorkspaceProjectId,
            dbContext.WorkspaceProjects.Where(project => project.Id == model.WorkspaceProjectId).Select(project => (Guid?)project.WorkspaceId).FirstOrDefault(),
            model.FolderPath)).ToListAsync(cancellationToken);
        return models.OrderByDescending(model => model.UpdatedUtc).ToList();
    }

    public async Task<ProjectDetail?> GetProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        return await GetProjectCoreAsync(id, cancellationToken);
    }

    private async Task<ProjectDetail?> GetProjectCoreAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var model = await dbContext.Projects.AsNoTracking().SingleOrDefaultAsync(model => model.Id == id, cancellationToken);
        if (model is null) return null;
        var (workspaceId, role) = await ModelRoleAsync(model, cancellationToken);
        var canUndo = await dbContext.ProjectVersions.AnyAsync(version => version.FlowProjectId == id, cancellationToken)
            || !string.IsNullOrWhiteSpace(model.PreviousBpmnXml);
        return new(model.Id, model.Name, model.BpmnXml, model.LogicXml, canUndo, model.CreatedUtc, model.UpdatedUtc,
            model.WorkspaceProjectId, workspaceId, model.FolderPath, model.Revision, WorkspaceAccess.CanEdit(role), WorkspaceAccess.CanManage(role), model.LastEditedBy);
    }

    public async Task<Guid> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        EnsureTenant();
        var ownerTenantId = tenantContext.TenantId;
        var targetProjectId = request.WorkspaceProjectId;
        if (targetProjectId is null && tenantContext.UserId is not null)
            targetProjectId = await EnsureDefaultContainerAsync(cancellationToken);
        if (targetProjectId is { } containerId)
        {
            var container = await dbContext.WorkspaceProjects.AsNoTracking().SingleOrDefaultAsync(project => project.Id == containerId, cancellationToken)
                ?? throw new InvalidOperationException("The project is unavailable.");
            WorkspaceAccess.RequireEdit(await access.RoleAsync(container.WorkspaceId, cancellationToken));
            ownerTenantId = await dbContext.Workspaces.Where(workspace => workspace.Id == container.WorkspaceId).Select(workspace => workspace.OrganizationId).SingleAsync(cancellationToken);
        }
        var folderPath = WorkspaceAccess.FolderPath(request.FolderPath);
        var name = await AvailableNameAsync(request.Name, ownerTenantId, targetProjectId, folderPath, cancellationToken);
        var model = new FlowProject(ownerTenantId, name, request.BpmnXml)
        {
            WorkspaceProjectId = targetProjectId,
            FolderPath = folderPath,
            LastEditedBy = await ActorNameAsync(cancellationToken)
        };
        model.UpdateModel(request.BpmnXml, request.LogicXml);
        dbContext.Projects.Add(model);
        dbContext.AuthorizeModelWrite(model.Id);
        if (model.WorkspaceProjectId.HasValue) await AddSnapshotAsync(model, "Created", null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return model.Id;
    }

    public async Task<long> UpdateProjectAsync(Guid id, UpdateProjectRequest request, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        var model = await EditableModelAsync(id, cancellationToken);
        CheckRevision(model, request.ExpectedRevision);
        var name = WorkspaceAccess.Name(request.Name);
        var folder = request.FolderPath is null ? model.FolderPath : WorkspaceAccess.FolderPath(request.FolderPath);
        if (model.WorkspaceProjectId.HasValue && await NameExistsAsync(model.TenantId, model.WorkspaceProjectId, folder, name, model.Id, cancellationToken))
            throw new InvalidOperationException("A model with this name already exists in the selected folder.");
        var logic = string.IsNullOrWhiteSpace(request.LogicXml) ? null : request.LogicXml.Trim();
        if (model.Name == name && model.BpmnXml == request.BpmnXml.Trim() && model.LogicXml == logic && model.FolderPath == folder) return model.Revision;
        model.UpdateProject(name, request.BpmnXml, request.LogicXml);
        model.FolderPath = folder;
        model.Revision++;
        model.LastEditedBy = await ActorNameAsync(cancellationToken);
        if (model.WorkspaceProjectId.HasValue) await AddSnapshotAsync(model, "Autosave", null, cancellationToken);
        await SaveModelAsync(cancellationToken);
        return model.Revision;
    }

    public async Task UndoProjectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        var model = await EditableModelAsync(id, cancellationToken);
        if (model.WorkspaceProjectId.HasValue) throw new InvalidOperationException("Restore a version from history so the collaboration history is preserved.");
        await dbContext.Entry(model).Collection(project => project.Versions).LoadAsync(cancellationToken);
        var existing = model.Versions.ToList();
        model.UndoLastChange();
        dbContext.ProjectVersions.RemoveRange(existing.Where(version => !model.Versions.Any(remaining => remaining.Id == version.Id)));
        model.Revision++;
        await SaveModelAsync(cancellationToken);
    }

    public async Task DeleteProjectAsync(Guid id, long expectedRevision, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        var model = await EditableModelAsync(id, cancellationToken, manage: true);
        CheckRevision(model, expectedRevision);
        dbContext.Projects.Remove(model);
        await SaveModelAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModelVersionSummary>> GetVersionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        if (await GetProjectCoreAsync(id, cancellationToken) is null) throw new InvalidOperationException("The model is unavailable.");
        return await dbContext.ProjectVersions.AsNoTracking().Where(version => version.FlowProjectId == id).OrderByDescending(version => version.VersionNumber)
            .Select(version => new ModelVersionSummary(version.Id, version.VersionNumber, version.Label == "" ? version.Name : version.Label,
                version.CreatedBy, version.CreatedUtc, version.PlannedReleaseUtc, version.ReleasedUtc, version.ReleasedBy)).ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateVersionAsync(Guid id, string label, DateTimeOffset? plannedReleaseUtc = null, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        var model = await EditableModelAsync(id, cancellationToken);
        if (plannedReleaseUtc.HasValue) WorkspaceAccess.RequireManage((await ModelRoleAsync(model, cancellationToken)).Role);
        var version = await AddSnapshotAsync(model, WorkspaceAccess.Name(label), plannedReleaseUtc, cancellationToken);
        await SaveModelAsync(cancellationToken);
        return version.Id;
    }

    public async Task ReleaseVersionAsync(Guid id, Guid versionId, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        await EditableModelAsync(id, cancellationToken, manage: true);
        var version = await dbContext.ProjectVersions.SingleOrDefaultAsync(version => version.FlowProjectId == id && version.Id == versionId, cancellationToken)
            ?? throw new InvalidOperationException("This version is unavailable.");
        if (version.ReleasedUtc.HasValue) return;
        version.ReleasedUtc = DateTimeOffset.UtcNow;
        version.ReleasedBy = await ActorNameAsync(cancellationToken);
        await SaveModelAsync(cancellationToken);
    }

    public async Task<long> RestoreVersionAsync(Guid id, Guid versionId, long expectedRevision, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        var model = await EditableModelAsync(id, cancellationToken, manage: true);
        CheckRevision(model, expectedRevision);
        var version = await dbContext.ProjectVersions.AsNoTracking().SingleOrDefaultAsync(version => version.FlowProjectId == id && version.Id == versionId, cancellationToken)
            ?? throw new InvalidOperationException("This version is unavailable.");
        if (model.WorkspaceProjectId.HasValue && await NameExistsAsync(model.TenantId, model.WorkspaceProjectId, model.FolderPath, version.Name, model.Id, cancellationToken))
            throw new InvalidOperationException("The version's model name is already in use in this folder. Rename the other model before restoring.");
        model.UpdateProject(version.Name, version.BpmnXml, version.LogicXml);
        model.Revision++;
        model.LastEditedBy = await ActorNameAsync(cancellationToken);
        await AddSnapshotAsync(model, $"Restored version {version.VersionNumber}", null, cancellationToken);
        await SaveModelAsync(cancellationToken);
        return model.Revision;
    }

    public async Task<IReadOnlyList<ModelPresence>> HeartbeatAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        var model = await GetProjectCoreAsync(id, cancellationToken) ?? throw new InvalidOperationException("The model is unavailable.");
        var user = await access.UserAsync(cancellationToken);
        if (model.CanEdit)
        {
            var presence = await dbContext.ModelEditingPresences.SingleOrDefaultAsync(item => item.ModelId == id && item.UserId == user.Id, cancellationToken);
            if (presence is null)
            {
                presence = new ModelEditingPresence { ModelId = id, UserId = user.Id };
                dbContext.ModelEditingPresences.Add(presence);
            }
            presence.LastSeenUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        var active = await (from presence in dbContext.ModelEditingPresences.AsNoTracking()
                            join editor in dbContext.Users on presence.UserId equals editor.Id
                            where presence.ModelId == id && presence.UserId != user.Id
                            select new ModelPresence(editor.Id, editor.Email ?? editor.UserName ?? "", presence.LastSeenUtc)).ToListAsync(cancellationToken);
        var threshold = DateTimeOffset.UtcNow.AddSeconds(-75);
        return active.Where(item => item.LastSeenUtc >= threshold).ToList();
    }

    public async Task LeaveModelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var operation = await dbContext.EnterOperationAsync(cancellationToken);
        var user = await access.UserAsync(cancellationToken);
        var presence = await dbContext.ModelEditingPresences.SingleOrDefaultAsync(item => item.ModelId == id && item.UserId == user.Id, cancellationToken);
        if (presence is null) return;
        dbContext.ModelEditingPresences.Remove(presence);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<FlowProject> EditableModelAsync(Guid id, CancellationToken cancellationToken, bool manage = false)
    {
        EnsureTenant();
        var tracked = dbContext.ChangeTracker.Entries<FlowProject>().SingleOrDefault(entry => entry.Entity.Id == id);
        if (tracked is not null) tracked.State = EntityState.Detached;
        var model = await dbContext.Projects.SingleOrDefaultAsync(model => model.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("The requested project does not exist or access has been removed.");
        var (_, role) = await ModelRoleAsync(model, cancellationToken);
        if (manage) WorkspaceAccess.RequireManage(role); else WorkspaceAccess.RequireEdit(role);
        dbContext.AuthorizeModelWrite(model.Id);
        return model;
    }

    private async Task<(Guid? WorkspaceId, string Role)> ModelRoleAsync(FlowProject model, CancellationToken cancellationToken)
    {
        if (model.WorkspaceProjectId is not { } projectId) return (null, "Owner");
        var workspaceId = await dbContext.WorkspaceProjects.Where(project => project.Id == projectId).Select(project => project.WorkspaceId).SingleAsync(cancellationToken);
        return (workspaceId, await access.RoleAsync(workspaceId, cancellationToken));
    }

    private async Task<ProjectVersion> AddSnapshotAsync(FlowProject model, string label, DateTimeOffset? planned, CancellationToken cancellationToken)
    {
        var last = await dbContext.ProjectVersions.Where(version => version.FlowProjectId == model.Id).MaxAsync(version => (int?)version.VersionNumber, cancellationToken) ?? 0;
        var version = new ProjectVersion(model.TenantId, model.Id, last + 1, model.Name, model.BpmnXml, model.LogicXml)
        {
            Label = label, CreatedBy = await ActorNameAsync(cancellationToken), PlannedReleaseUtc = planned
        };
        dbContext.ProjectVersions.Add(version);
        return version;
    }

    private async Task SaveModelAsync(CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new InvalidOperationException("Another member saved this model first. Reload it before applying your changes; your changes were not overwritten.");
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
    }

    private static void CheckRevision(FlowProject model, long? expected)
    {
        if (model.WorkspaceProjectId.HasValue && expected is null) throw new InvalidOperationException("Reload this model before saving: a revision is required.");
        if (expected.HasValue && model.Revision != expected) throw new InvalidOperationException("Another member saved this model first. Reload it before applying your changes.");
    }

    private async Task<string?> ActorNameAsync(CancellationToken cancellationToken) => tenantContext.UserId is null ? null
        : await dbContext.Users.AsNoTracking().Where(user => user.Id == tenantContext.UserId).Select(user => user.Email ?? user.UserName).SingleOrDefaultAsync(cancellationToken);

    private async Task<Guid> EnsureDefaultContainerAsync(CancellationToken cancellationToken)
    {
        var user = await access.UserAsync(cancellationToken);
        var workspace = await dbContext.Workspaces.Where(item => item.OrganizationId == user.TenantId && item.OwnerUserId == user.Id)
            .OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        if (workspace is null)
        {
            workspace = new Workspace { OrganizationId = user.TenantId, OwnerUserId = user.Id, Name = "My workspace" };
            dbContext.Workspaces.Add(workspace);
            dbContext.WorkspaceMembers.Add(new() { WorkspaceId = workspace.Id, UserId = user.Id, Role = "Owner" });
        }
        var container = await dbContext.WorkspaceProjects.Where(item => item.WorkspaceId == workspace.Id).OrderBy(item => item.Name).FirstOrDefaultAsync(cancellationToken);
        if (container is null)
        {
            container = new WorkspaceProject { WorkspaceId = workspace.Id, Name = "Models" };
            dbContext.WorkspaceProjects.Add(container);
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return container.Id;
    }

    private async Task<string> AvailableNameAsync(string name, Guid ownerTenantId, Guid? projectId, string folder, CancellationToken cancellationToken)
    {
        var baseName = WorkspaceAccess.Name(name);
        var candidate = baseName;
        var suffix = 1;
        while (await NameExistsAsync(ownerTenantId, projectId, folder, candidate, null, cancellationToken))
        {
            var ending = $" ({suffix++})";
            candidate = baseName[..Math.Min(baseName.Length, 240 - ending.Length)] + ending;
        }
        return candidate;
    }

    private Task<bool> NameExistsAsync(Guid tenantId, Guid? projectId, string folder, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        dbContext.Projects.IgnoreQueryFilters().AnyAsync(model => model.Id != exceptId && model.Name == name
            && (projectId.HasValue ? model.WorkspaceProjectId == projectId && model.FolderPath == folder
                : model.WorkspaceProjectId == null && model.TenantId == tenantId), cancellationToken);

    private void EnsureTenant()
    {
        if (!tenantContext.IsAvailable) throw new InvalidOperationException("A tenant is required for simpleXflow project data.");
    }
}
