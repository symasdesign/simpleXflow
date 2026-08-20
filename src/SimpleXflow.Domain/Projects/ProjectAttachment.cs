using SimpleXflow.Domain.Common;

namespace SimpleXflow.Domain.Projects;

public sealed class ProjectAttachment : ITenantEntity
{
    private ProjectAttachment()
    {
    }

    public ProjectAttachment(Guid tenantId, Guid projectId, string fileName, string contentType, byte[] content)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ProjectId = projectId;
        FileName = fileName.Trim();
        ContentType = contentType.Trim();
        Content = content;
        CreatedUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; private set; }

    public string FileName { get; private set; } = "";

    public string ContentType { get; private set; } = "";

    public byte[] Content { get; private set; } = [];

    public DateTimeOffset CreatedUtc { get; private set; }

    public FlowProject Project { get; private set; } = default!;
}
