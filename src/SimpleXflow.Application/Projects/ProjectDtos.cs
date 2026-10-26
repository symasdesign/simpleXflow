namespace SimpleXflow.Application.Projects;

public sealed record ProjectSummary(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedUtc,
    Guid? WorkspaceProjectId = null,
    Guid? WorkspaceId = null,
    string FolderPath = "");

public sealed record ProjectDetail(
    Guid Id,
    string Name,
    string BpmnXml,
    string? LogicXml,
    bool CanUndo,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    Guid? WorkspaceProjectId = null,
    Guid? WorkspaceId = null,
    string FolderPath = "",
    long Revision = 0,
    bool CanEdit = true,
    bool CanManage = true,
    string? LastEditedBy = null);

public sealed record CreateProjectRequest(
    string Name,
    string BpmnXml,
    string? LogicXml = null,
    Guid? WorkspaceProjectId = null,
    string FolderPath = "");

public sealed record UpdateProjectRequest(
    string Name,
    string BpmnXml,
    string? LogicXml,
    long? ExpectedRevision = null,
    string? FolderPath = null);

public sealed record ModelVersionSummary(Guid Id, int Number, string Label, string? CreatedBy, DateTimeOffset CreatedUtc,
    DateTimeOffset? PlannedReleaseUtc, DateTimeOffset? ReleasedUtc, string? ReleasedBy);
public sealed record ModelPresence(string UserId, string Email, DateTimeOffset LastSeenUtc);
