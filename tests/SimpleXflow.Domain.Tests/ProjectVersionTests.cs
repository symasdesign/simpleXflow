using SimpleXflow.Domain.Projects;

namespace SimpleXflow.Domain.Tests;

public sealed class ProjectVersionTests
{
    [Fact]
    public void Constructor_CreatesTenantScopedNormalizedSnapshot()
    {
        var tenantId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;

        var version = new ProjectVersion(tenantId, projectId, 42, " \nSaved project\t", " \n<model />\t", " \n<logic />\t");

        Assert.NotEqual(Guid.Empty, version.Id);
        Assert.Equal(tenantId, version.TenantId);
        Assert.Equal(projectId, version.FlowProjectId);
        Assert.Equal(42, version.VersionNumber);
        Assert.Equal("Saved project", version.Name);
        Assert.Equal("<model />", version.BpmnXml);
        Assert.Equal("<logic />", version.LogicXml);
        Assert.InRange(version.CreatedUtc, before, DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Constructor_RejectsNonpositiveVersionNumber(int versionNumber)
    {
        Assert.Throws<ArgumentOutOfRangeException>("versionNumber", () =>
            new ProjectVersion(Guid.NewGuid(), Guid.NewGuid(), versionNumber, "Project", "<model />", null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Constructor_AllowsPositiveVersionNumberBoundaries(int versionNumber)
    {
        var version = new ProjectVersion(Guid.NewGuid(), Guid.NewGuid(), versionNumber, "Project", "<model />", null);

        Assert.Equal(versionNumber, version.VersionNumber);
    }

    [Theory]
    [InlineData(null, "<model />", "name")]
    [InlineData("", "<model />", "name")]
    [InlineData(" \t ", "<model />", "name")]
    [InlineData("Project", null, "bpmnXml")]
    [InlineData("Project", "", "bpmnXml")]
    [InlineData("Project", " \t ", "bpmnXml")]
    public void Constructor_RejectsMissingRequiredSnapshotData(string? name, string? model, string parameter)
    {
        Assert.Throws<ArgumentException>(parameter, () =>
            new ProjectVersion(Guid.NewGuid(), Guid.NewGuid(), 1, name!, model!, null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Constructor_NormalizesEmptyLogicToNull(string? logic)
    {
        var version = new ProjectVersion(Guid.NewGuid(), Guid.NewGuid(), 1, "Project", "<model />", logic);

        Assert.Null(version.LogicXml);
    }

    [Fact]
    public void Snapshot_RemainsUnchangedWhenCurrentProjectIsEdited()
    {
        var project = new FlowProject(Guid.NewGuid(), "Saved", "<saved />");
        project.UpdateModel(project.BpmnXml, "<saved-logic />");
        var version = new ProjectVersion(project.TenantId, project.Id, 1, project.Name, project.BpmnXml, project.LogicXml);

        project.UpdateProject("Edited", "<edited />", null);

        Assert.Equal("Saved", version.Name);
        Assert.Equal("<saved />", version.BpmnXml);
        Assert.Equal("<saved-logic />", version.LogicXml);
    }
}
