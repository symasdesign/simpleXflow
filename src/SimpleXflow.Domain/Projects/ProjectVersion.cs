using SimpleXflow.Domain.Common;

namespace SimpleXflow.Domain.Projects;

public sealed class ProjectVersion : ITenantEntity
{
    private ProjectVersion()
    {
    }

    public ProjectVersion(
        Guid tenantId,
        Guid flowProjectId,
        int versionNumber,
        string name,
        string bpmnXml,
        string? logicXml)
    {
        if (versionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be greater than zero.");
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        FlowProjectId = flowProjectId;
        VersionNumber = versionNumber;
        Name = NormalizeName(name);
        BpmnXml = NormalizeModelXml(bpmnXml);
        LogicXml = string.IsNullOrWhiteSpace(logicXml) ? null : logicXml.Trim();
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public Guid FlowProjectId { get; private set; }

    public int VersionNumber { get; private set; }

    public string Name { get; private set; } = "";

    public string BpmnXml { get; private set; } = "";

    public string? LogicXml { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        return name.Trim();
    }

    private static string NormalizeModelXml(string bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            throw new ArgumentException("A simpleXflow model is required.", nameof(bpmnXml));
        }

        return bpmnXml.Trim();
    }
}
