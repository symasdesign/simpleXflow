using System.Text;
using SimpleXflow.Domain.Projects;

namespace SimpleXflow.Domain.Tests;

public sealed class ProjectAttachmentTests
{
    [Fact]
    public void Constructor_AssociatesAttachmentWithTenantAndProjectAndNormalizesMetadata()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("<model>Grüsse aus Zürich</model>");
        var before = DateTimeOffset.UtcNow;

        var attachment = new ProjectAttachment(tenantId, projectId, " \tProzess Modell.bpmn\r\n", " application/xml ", content);

        Assert.NotEqual(Guid.Empty, attachment.Id);
        Assert.Equal(tenantId, attachment.TenantId);
        Assert.Equal(projectId, attachment.ProjectId);
        Assert.Equal("Prozess Modell.bpmn", attachment.FileName);
        Assert.Equal("application/xml", attachment.ContentType);
        Assert.Equal(content, attachment.Content);
        Assert.InRange(attachment.CreatedUtc, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Constructor_PreservesBinaryPayloadIncludingZeroBytes()
    {
        byte[] content = [0x00, 0x01, 0xff, 0xfe, 0x80, 0x00];

        var attachment = new ProjectAttachment(Guid.NewGuid(), Guid.NewGuid(), "payload.bin", "application/octet-stream", content);

        Assert.Equal(content, attachment.Content);
    }

    [Fact]
    public void Constructor_AllowsAnEmptyFile()
    {
        var attachment = new ProjectAttachment(Guid.NewGuid(), Guid.NewGuid(), "empty.txt", "text/plain", []);

        Assert.Empty(attachment.Content);
    }
}
