using SimpleXflow.Domain.Common;

namespace SimpleXflow.Domain.Projects;

public sealed class FlowProject : ITenantEntity
{
    private readonly List<ProjectAttachment> _attachments = [];
    private readonly List<ProjectVersion> _versions = [];

    private FlowProject()
    {
    }

    public FlowProject(Guid tenantId, string name, string bpmnXml)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Name = NormalizeName(name);
        BpmnXml = NormalizeModelXml(bpmnXml);
        CreatedUtc = DateTimeOffset.UtcNow;
        UpdatedUtc = CreatedUtc;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public string Name { get; private set; } = "";

    public string BpmnXml { get; private set; } = "";

    public string? LogicXml { get; private set; }

    public string? PreviousName { get; private set; }

    public string? PreviousBpmnXml { get; private set; }

    public string? PreviousLogicXml { get; private set; }

    public DateTimeOffset? PreviousUpdatedUtc { get; private set; }

    public DateTimeOffset CreatedUtc { get; private set; }

    public DateTimeOffset UpdatedUtc { get; private set; }

    public IReadOnlyCollection<ProjectAttachment> Attachments => _attachments;

    public IReadOnlyCollection<ProjectVersion> Versions => _versions;

    public bool CanUndo => _versions.Count > 0 || HasLegacyUndoSnapshot;

    public void Rename(string name)
    {
        Name = NormalizeName(name);
        Touch();
    }

    public void UpdateProject(string name, string bpmnXml, string? logicXml)
    {
        var normalizedName = NormalizeName(name);
        var normalizedBpmnXml = NormalizeModelXml(bpmnXml);
        var normalizedLogicXml = NormalizeLogicXml(logicXml);

        if (Name == normalizedName
            && BpmnXml == normalizedBpmnXml
            && LogicXml == normalizedLogicXml)
        {
            return;
        }

        Name = normalizedName;
        BpmnXml = normalizedBpmnXml;
        LogicXml = normalizedLogicXml;
        Touch();
    }

    public void UpdateModel(string bpmnXml, string? logicXml)
    {
        ApplyModel(bpmnXml, logicXml);
        Touch();
    }

    public void UndoLastChange()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("There is no saved change to undo for this project.");
        }

        var latestVersion = _versions
            .OrderByDescending(version => version.VersionNumber)
            .FirstOrDefault();

        if (latestVersion is not null)
        {
            Name = latestVersion.Name;
            BpmnXml = latestVersion.BpmnXml;
            LogicXml = latestVersion.LogicXml;
            _versions.Remove(latestVersion);
            Touch();
            return;
        }

        Name = PreviousName ?? Name;
        BpmnXml = PreviousBpmnXml!;
        LogicXml = PreviousLogicXml;
        UpdatedUtc = PreviousUpdatedUtc ?? UpdatedUtc;

        ClearUndoSnapshot();
        Touch();
    }

    private void ApplyModel(string bpmnXml, string? logicXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            throw new ArgumentException("A simpleXflow model is required.", nameof(bpmnXml));
        }

        BpmnXml = NormalizeModelXml(bpmnXml);
        LogicXml = string.IsNullOrWhiteSpace(logicXml) ? null : logicXml;
    }

    private void ClearUndoSnapshot()
    {
        PreviousName = null;
        PreviousBpmnXml = null;
        PreviousLogicXml = null;
        PreviousUpdatedUtc = null;
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

    private static string NormalizeModelXml(string bpmnXml)
    {
        if (string.IsNullOrWhiteSpace(bpmnXml))
        {
            throw new ArgumentException("A simpleXflow model is required.", nameof(bpmnXml));
        }

        return bpmnXml.Trim();
    }

    private static string? NormalizeLogicXml(string? logicXml)
    {
        return string.IsNullOrWhiteSpace(logicXml) ? null : logicXml.Trim();
    }

    private bool HasLegacyUndoSnapshot => !string.IsNullOrWhiteSpace(PreviousBpmnXml);
}
