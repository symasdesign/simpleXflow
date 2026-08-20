using SimpleXflow.Domain.Common;

namespace SimpleXflow.Domain.Projects;

public sealed class FlowProject : ITenantEntity
{
    private readonly List<ProjectAttachment> _attachments = [];

    private FlowProject()
    {
    }

    public FlowProject(Guid tenantId, string name, string bpmnXml)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = NormalizeName(name);
        BpmnXml = bpmnXml;
        CreatedUtc = DateTimeOffset.UtcNow;
        UpdatedUtc = CreatedUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = "";

    public string BpmnXml { get; private set; } = "";

    public string? LogicXml { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset UpdatedUtc { get; private set; }

    public IReadOnlyCollection<ProjectAttachment> Attachments => _attachments;

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        Touch();
    }

    public void UpdateModel(string bpmnXml, string? logicXml)
    {
        BpmnXml = bpmnXml;
        LogicXml = string.IsNullOrWhiteSpace(logicXml) ? null : logicXml;
        Touch();
    }

    private void Touch()
    {
        UpdatedUtc = DateTimeOffset.UtcNow;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Project name is required.", nameof(name));
        }

        return name.Trim();
    }
}
