namespace SimpleXflow.Application.Workspaces;

public interface IWorkspaceService
{
    Task<OrganizationSummary> GetOrganizationAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkspaceSummary>> GetWorkspacesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateWorkspaceAsync(string name, CancellationToken cancellationToken = default);
    Task<WorkspaceDetail?> GetWorkspaceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateProjectAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default);
    Task CreateFolderAsync(Guid projectId, string path, CancellationToken cancellationToken = default);
    Task<InvitationResult> CreateInvitationAsync(Guid workspaceId, string email, string role, CancellationToken cancellationToken = default);
    Task<Guid> AcceptInvitationAsync(string token, CancellationToken cancellationToken = default);
    Task SetMemberRoleAsync(Guid workspaceId, string userId, string role, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(Guid workspaceId, string userId, CancellationToken cancellationToken = default);
    Task RevokeInvitationAsync(Guid workspaceId, Guid invitationId, CancellationToken cancellationToken = default);
    Task<Guid> CreateWorkPackageAsync(Guid workspaceId, WorkPackageRequest request, CancellationToken cancellationToken = default);
    Task UpdateWorkPackageAsync(Guid workspaceId, Guid workPackageId, WorkPackageRequest request, CancellationToken cancellationToken = default);
}

public sealed record OrganizationSummary(Guid Id, string Name, string CurrentUserId, string Email);
public sealed record WorkspaceSummary(Guid Id, string Name, Guid OrganizationId, string OrganizationName, string Role, bool IsShared, int ProjectCount);
public sealed record WorkspaceDetail(Guid Id, string Name, Guid OrganizationId, string OrganizationName, string Role,
    IReadOnlyList<WorkspaceProjectSummary> Projects, IReadOnlyList<WorkspaceMemberSummary> Members,
    IReadOnlyList<WorkspaceInvitationSummary> Invitations, IReadOnlyList<WorkPackageSummary> WorkPackages);
public sealed record WorkspaceProjectSummary(Guid Id, string Name, int ModelCount, IReadOnlyList<string> Folders);
public sealed record WorkspaceMemberSummary(string UserId, string Email, string OrganizationName, string Role);
public sealed record WorkspaceInvitationSummary(Guid Id, string Email, string Role, DateTimeOffset ExpiresUtc);
public sealed record InvitationResult(Guid Id, string Token, DateTimeOffset ExpiresUtc);
public sealed record WorkPackageRequest(string Title, string? AssigneeUserId, DateTimeOffset? DueUtc, string Status = "Open", Guid? ModelId = null, string? Description = null);
public sealed record WorkPackageSummary(Guid Id, string Title, string? AssigneeUserId, string? AssigneeEmail, DateTimeOffset? DueUtc, string Status, Guid? ModelId, string? Description);
