using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SimpleXflow.Application.Abstractions;
using SimpleXflow.Application.Workspaces;
using SimpleXflow.Domain.Workspaces;
using SimpleXflow.Infrastructure.Persistence;

namespace SimpleXflow.Infrastructure.Workspaces;

public sealed class WorkspaceService(ApplicationDbContext db, ITenantContext tenant) : IWorkspaceService
{
    private readonly WorkspaceAccess access = new(db, tenant);

    public async Task<OrganizationSummary> GetOrganizationAsync(CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var user = await access.UserAsync(cancellationToken);
        var organization = await db.Tenants.AsNoTracking().SingleAsync(item => item.Id == user.TenantId, cancellationToken);
        return new(organization.Id, organization.Name, user.Id, user.Email ?? user.UserName ?? "");
    }

    public async Task<IReadOnlyList<WorkspaceSummary>> GetWorkspacesAsync(CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        await access.UserAsync(cancellationToken);
        await MigrateLegacyModelsAsync(cancellationToken);
        return await (from workspace in db.Workspaces.AsNoTracking()
                      join member in db.WorkspaceMembers on workspace.Id equals member.WorkspaceId
                      join organization in db.Tenants on workspace.OrganizationId equals organization.Id
                      where member.UserId == tenant.UserId
                      orderby workspace.Name
                      select new WorkspaceSummary(workspace.Id, workspace.Name, organization.Id, organization.Name, member.Role,
                          organization.Id != tenant.TenantId, db.WorkspaceProjects.Count(project => project.WorkspaceId == workspace.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateWorkspaceAsync(string name, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var user = await access.UserAsync(cancellationToken);
        var workspace = new Workspace { OrganizationId = user.TenantId, OwnerUserId = user.Id, Name = WorkspaceAccess.Name(name, 200) };
        db.Workspaces.Add(workspace);
        db.WorkspaceMembers.Add(new() { WorkspaceId = workspace.Id, UserId = user.Id, Role = "Owner" });
        await db.SaveChangesAsync(cancellationToken);
        return workspace.Id;
    }

    public async Task<WorkspaceDetail?> GetWorkspaceAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        await access.UserAsync(cancellationToken);
        var role = await db.WorkspaceMembers.AsNoTracking().Where(item => item.WorkspaceId == id && item.UserId == tenant.UserId)
            .Select(item => item.Role).SingleOrDefaultAsync(cancellationToken);
        if (role is null) return null;
        var workspace = await db.Workspaces.AsNoTracking().SingleAsync(item => item.Id == id, cancellationToken);
        var organization = await db.Tenants.AsNoTracking().SingleAsync(item => item.Id == workspace.OrganizationId, cancellationToken);
        var projectRows = await db.WorkspaceProjects.AsNoTracking().Where(item => item.WorkspaceId == id).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var projectIds = projectRows.Select(item => item.Id).ToArray();
        var folders = await db.ModelFolders.AsNoTracking().Where(item => projectIds.Contains(item.WorkspaceProjectId)).ToListAsync(cancellationToken);
        var models = await db.Projects.Where(item => item.WorkspaceProjectId.HasValue && projectIds.Contains(item.WorkspaceProjectId.Value))
            .Select(item => new { item.WorkspaceProjectId, item.FolderPath }).ToListAsync(cancellationToken);
        var projects = projectRows.Select(project => new WorkspaceProjectSummary(project.Id, project.Name, models.Count(item => item.WorkspaceProjectId == project.Id),
            folders.Where(item => item.WorkspaceProjectId == project.Id).Select(item => item.Path)
                .Concat(models.Where(item => item.WorkspaceProjectId == project.Id && item.FolderPath != "").Select(item => item.FolderPath))
                .Distinct().OrderBy(path => path).ToList())).ToList();
        var members = await (from member in db.WorkspaceMembers
                             join user in db.Users on member.UserId equals user.Id
                             join org in db.Tenants on user.TenantId equals org.Id
                             where member.WorkspaceId == id
                             select new WorkspaceMemberSummary(user.Id, user.Email ?? user.UserName ?? "", org.Name, member.Role)).ToListAsync(cancellationToken);
        var invitations = WorkspaceAccess.CanManage(role)
            ? (await db.WorkspaceInvitations.AsNoTracking().Where(item => item.WorkspaceId == id && item.AcceptedUtc == null && item.RevokedUtc == null)
                .ToListAsync(cancellationToken)).Where(item => item.ExpiresUtc > DateTimeOffset.UtcNow)
                .Select(item => new WorkspaceInvitationSummary(item.Id, item.Email, item.Role, item.ExpiresUtc)).ToList()
            : [];
        var packages = await db.WorkPackages.AsNoTracking().Where(item => item.WorkspaceId == id).ToListAsync(cancellationToken);
        return new(id, workspace.Name, organization.Id, organization.Name, role, projects, members, invitations,
            packages.Select(item => new WorkPackageSummary(item.Id, item.Title, item.AssigneeUserId,
                members.SingleOrDefault(member => member.UserId == item.AssigneeUserId)?.Email, item.DueUtc, item.Status, item.ModelId, item.Description)).ToList());
    }

    public async Task<Guid> CreateProjectAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        WorkspaceAccess.RequireEdit(await access.RoleAsync(workspaceId, cancellationToken));
        var project = new WorkspaceProject { WorkspaceId = workspaceId, Name = WorkspaceAccess.Name(name) };
        if (await db.WorkspaceProjects.AnyAsync(item => item.WorkspaceId == workspaceId && item.Name == project.Name, cancellationToken))
            throw new InvalidOperationException("A project with this name already exists in this workspace.");
        db.WorkspaceProjects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
        return project.Id;
    }

    public async Task CreateFolderAsync(Guid projectId, string path, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var project = await db.WorkspaceProjects.AsNoTracking().SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken)
            ?? throw new InvalidOperationException("The project is unavailable.");
        WorkspaceAccess.RequireEdit(await access.RoleAsync(project.WorkspaceId, cancellationToken));
        var normalized = WorkspaceAccess.FolderPath(path);
        if (normalized == "") throw new ArgumentException("Enter a folder name.");
        var existing = await db.ModelFolders.Where(item => item.WorkspaceProjectId == projectId).Select(item => item.Path).ToListAsync(cancellationToken);
        var segments = normalized.Split('/');
        for (var index = 1; index <= segments.Length; index++)
        {
            var parent = string.Join('/', segments.Take(index));
            if (!existing.Contains(parent)) db.ModelFolders.Add(new() { WorkspaceProjectId = projectId, Path = parent });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvitationResult> CreateInvitationAsync(Guid workspaceId, string email, string role, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var actorRole = await access.RoleAsync(workspaceId, cancellationToken);
        ValidateRoleChange(actorRole, role);
        if (!MailAddress.TryCreate(email.Trim(), out var address) || address.Address != email.Trim()) throw new ArgumentException("Enter a valid email address.");
        var normalized = email.Trim().ToUpperInvariant();
        if (await (from member in db.WorkspaceMembers join user in db.Users on member.UserId equals user.Id
                   where member.WorkspaceId == workspaceId && user.NormalizedEmail == normalized select member).AnyAsync(cancellationToken))
            throw new InvalidOperationException("This account is already a workspace member.");
        var oldInvitations = await db.WorkspaceInvitations.Where(item => item.WorkspaceId == workspaceId && item.Email == normalized && item.AcceptedUtc == null && item.RevokedUtc == null).ToListAsync(cancellationToken);
        // Replacing an invitation also revokes its previous grant. An admin
        // cannot use re-invitation to override an owner's admin invitation.
        foreach (var old in oldInvitations) ValidateRoleChange(actorRole, old.Role);
        foreach (var old in oldInvitations) old.RevokedUtc = DateTimeOffset.UtcNow;
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var invitation = new WorkspaceInvitation { WorkspaceId = workspaceId, Email = normalized, Role = role, TokenHash = HashToken(token), ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) };
        db.WorkspaceInvitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);
        return new(invitation.Id, token, invitation.ExpiresUtc);
    }

    public async Task<Guid> AcceptInvitationAsync(string token, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var user = await access.UserAsync(cancellationToken);
        if (!user.EmailConfirmed) throw new InvalidOperationException("Confirm your account email before accepting a workspace invitation.");
        var hash = HashToken(token);
        var invitation = await db.WorkspaceInvitations.SingleOrDefaultAsync(item => item.TokenHash == hash, cancellationToken);
        if (invitation is null || invitation.RevokedUtc.HasValue || invitation.AcceptedUtc.HasValue || invitation.ExpiresUtc <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("This invitation is invalid, expired or already used.");
        if (!string.Equals(invitation.Email, user.NormalizedEmail, StringComparison.Ordinal))
            throw new InvalidOperationException("Sign in with the email address this invitation was sent to.");
        if (!await db.WorkspaceMembers.AnyAsync(item => item.WorkspaceId == invitation.WorkspaceId && item.UserId == user.Id, cancellationToken))
            db.WorkspaceMembers.Add(new() { WorkspaceId = invitation.WorkspaceId, UserId = user.Id, Role = invitation.Role });
        invitation.AcceptedUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return invitation.WorkspaceId;
    }

    public async Task SetMemberRoleAsync(Guid workspaceId, string userId, string role, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var actorRole = await access.RoleAsync(workspaceId, cancellationToken);
        ValidateRoleChange(actorRole, role);
        var member = await db.WorkspaceMembers.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("This member no longer belongs to the workspace.");
        ValidateRoleChange(actorRole, member.Role);
        member.Role = role;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid workspaceId, string userId, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var actorRole = await access.RoleAsync(workspaceId, cancellationToken);
        var member = await db.WorkspaceMembers.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("This member no longer belongs to the workspace.");
        ValidateRoleChange(actorRole, member.Role);
        db.WorkspaceMembers.Remove(member);
        var packages = await db.WorkPackages.Where(item => item.WorkspaceId == workspaceId && item.AssigneeUserId == userId).ToListAsync(cancellationToken);
        foreach (var package in packages) package.AssigneeUserId = null;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeInvitationAsync(Guid workspaceId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var actorRole = await access.RoleAsync(workspaceId, cancellationToken);
        var invitation = await db.WorkspaceInvitations.SingleOrDefaultAsync(item => item.WorkspaceId == workspaceId && item.Id == invitationId, cancellationToken)
            ?? throw new InvalidOperationException("This invitation is unavailable.");
        ValidateRoleChange(actorRole, invitation.Role);
        invitation.RevokedUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> CreateWorkPackageAsync(Guid workspaceId, WorkPackageRequest request, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        WorkspaceAccess.RequireManage(await access.RoleAsync(workspaceId, cancellationToken));
        var package = new WorkPackage { WorkspaceId = workspaceId };
        await ApplyWorkPackageAsync(package, request, cancellationToken);
        db.WorkPackages.Add(package);
        await db.SaveChangesAsync(cancellationToken);
        return package.Id;
    }

    public async Task UpdateWorkPackageAsync(Guid workspaceId, Guid workPackageId, WorkPackageRequest request, CancellationToken cancellationToken = default)
    {
        using var operation = await db.EnterOperationAsync(cancellationToken);
        var role = await access.RoleAsync(workspaceId, cancellationToken);
        var package = await db.WorkPackages.SingleOrDefaultAsync(item => item.Id == workPackageId && item.WorkspaceId == workspaceId, cancellationToken)
            ?? throw new InvalidOperationException("This work package is unavailable.");
        if (!WorkspaceAccess.CanManage(role)
            && (package.AssigneeUserId != tenant.UserId || package.Title != request.Title || package.AssigneeUserId != request.AssigneeUserId
                || package.DueUtc != request.DueUtc || package.ModelId != request.ModelId || package.Description != request.Description))
            throw new InvalidOperationException("Only administrators can delegate or change a work package. Assignees can update its status.");
        await ApplyWorkPackageAsync(package, request, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplyWorkPackageAsync(WorkPackage package, WorkPackageRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not ("Open" or "In progress" or "Done")) throw new ArgumentException("Choose a valid work package status.");
        if (request.AssigneeUserId is not null && !await db.WorkspaceMembers.AnyAsync(item => item.WorkspaceId == package.WorkspaceId && item.UserId == request.AssigneeUserId, cancellationToken))
            throw new InvalidOperationException("Assign work only to members of this workspace.");
        if (request.ModelId.HasValue && !await db.Projects.AnyAsync(model => model.Id == request.ModelId && db.WorkspaceProjects.Any(project => project.Id == model.WorkspaceProjectId && project.WorkspaceId == package.WorkspaceId), cancellationToken))
            throw new InvalidOperationException("Choose a model in this workspace.");
        if (request.Description?.Length > 4000) throw new ArgumentException("The description can contain at most 4000 characters.");
        package.Title = WorkspaceAccess.Name(request.Title);
        package.AssigneeUserId = request.AssigneeUserId;
        package.DueUtc = request.DueUtc;
        package.Status = request.Status;
        package.ModelId = request.ModelId;
        package.Description = request.Description;
    }

    private async Task MigrateLegacyModelsAsync(CancellationToken cancellationToken, bool retry = true)
    {
        var models = await db.Projects.Where(model => model.WorkspaceProjectId == null && model.TenantId == tenant.TenantId).ToListAsync(cancellationToken);
        if (models.Count == 0) return;
        var workspace = new Workspace { OrganizationId = tenant.TenantId, OwnerUserId = tenant.UserId!, Name = "My workspace" };
        db.Workspaces.Add(workspace);
        var users = await db.Users.Where(user => user.TenantId == tenant.TenantId).Select(user => user.Id).ToListAsync(cancellationToken);
        foreach (var userId in users) db.WorkspaceMembers.Add(new() { WorkspaceId = workspace.Id, UserId = userId, Role = userId == tenant.UserId ? "Owner" : "Editor" });
        var project = new WorkspaceProject { WorkspaceId = workspace.Id, Name = "Imported models" };
        db.WorkspaceProjects.Add(project);
        foreach (var model in models)
        {
            model.WorkspaceProjectId = project.Id;
            model.Revision++;
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) when (retry)
        {
            // The losing transaction also rolls back its workspace and members.
            db.ChangeTracker.Clear();
            await MigrateLegacyModelsAsync(cancellationToken, retry: false);
        }
    }

    private static void ValidateRoleChange(string actorRole, string role)
    {
        WorkspaceAccess.RequireManage(actorRole);
        if (role is not ("Admin" or "Editor" or "Viewer") || (role == "Admin" && actorRole != "Owner"))
            throw new InvalidOperationException("The owner is permanent. Only the owner can appoint or remove administrators.");
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
