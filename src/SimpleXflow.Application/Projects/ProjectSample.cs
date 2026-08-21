namespace SimpleXflow.Application.Projects;

public sealed record ProjectSample(
    string Id,
    string Name,
    string Description,
    string BpmnXml,
    string? LogicXml = null,
    string? LogicTargetElementId = null);
