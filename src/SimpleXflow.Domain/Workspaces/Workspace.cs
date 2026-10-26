namespace SimpleXflow.Domain.Workspaces;

public sealed class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string OwnerUserId { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WorkspaceMember
{
    public Guid WorkspaceId { get; set; }
    public string UserId { get; set; } = "";
    public string Role { get; set; } = "Editor";
}

public sealed class WorkspaceInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Editor";
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? AcceptedUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
}

public sealed class WorkspaceProject
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class ModelFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceProjectId { get; set; }
    public string Path { get; set; } = "";
}

public sealed class WorkPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? AssigneeUserId { get; set; }
    public DateTimeOffset? DueUtc { get; set; }
    public string Status { get; set; } = "Open";
    public Guid? ModelId { get; set; }
}

public sealed class ModelEditingPresence
{
    public Guid ModelId { get; set; }
    public string UserId { get; set; } = "";
    public DateTimeOffset LastSeenUtc { get; set; }
}
