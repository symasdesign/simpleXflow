namespace SimpleXflow.Application.Projects;

public sealed record ProjectSummary(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedUtc);

public sealed record ProjectDetail(
    Guid Id,
    string Name,
    string BpmnXml,
    string? LogicXml,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record CreateProjectRequest(
    string Name,
    string BpmnXml,
    string? LogicXml = null);

public sealed record UpdateProjectRequest(
    string Name,
    string BpmnXml,
    string? LogicXml);
