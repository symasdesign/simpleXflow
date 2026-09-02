using SimpleXflow.Domain.Projects;

namespace SimpleXflow.Domain.Tests;

public sealed class FlowProjectTests
{
    [Fact]
    public void Constructor_CreatesTenantScopedProject()
    {
        var tenantId = Guid.NewGuid();

        var project = new FlowProject(tenantId, "  Coffee break  ", "<xml />");

        Assert.Equal(tenantId, project.TenantId);
        Assert.Equal("Coffee break", project.Name);
        Assert.Equal("<xml />", project.BpmnXml);
        Assert.True(project.CreatedUtc <= project.UpdatedUtc);
    }

    [Fact]
    public void UpdateModel_StoresLogicXmlAndTouchesProject()
    {
        var project = new FlowProject(Guid.NewGuid(), "Flow", "<old />");
        var previousUpdatedUtc = project.UpdatedUtc;

        project.UpdateModel("<new />", "<logic />");

        Assert.Equal("<new />", project.BpmnXml);
        Assert.Equal("<logic />", project.LogicXml);
        Assert.True(project.UpdatedUtc >= previousUpdatedUtc);
    }

    [Fact]
    public void Rename_RejectsEmptyName()
    {
        var project = new FlowProject(Guid.NewGuid(), "Flow", "<xml />");

        Assert.Throws<ArgumentException>(() => project.Rename(""));
    }

    [Fact]
    public void UpdateProject_AutosavesCurrentStateWithoutVersionSnapshot()
    {
        var project = new FlowProject(Guid.NewGuid(), "Original", "<old />");

        project.UpdateProject("Changed", "<new />", "<logic />");

        Assert.Equal("Changed", project.Name);
        Assert.Equal("<new />", project.BpmnXml);
        Assert.Equal("<logic />", project.LogicXml);
        Assert.False(project.CanUndo);
        Assert.Empty(project.Versions);
    }

    [Fact]
    public void UndoLastChange_RejectsProjectWithoutSavedVersion()
    {
        var project = new FlowProject(Guid.NewGuid(), "Original", "<old />");

        var exception = Assert.Throws<InvalidOperationException>(project.UndoLastChange);
        Assert.Contains("no saved change", exception.Message);
    }

    [Fact]
    public void UpdateProject_DoesNotCreateUndoVersionWhenNothingChanged()
    {
        var project = new FlowProject(Guid.NewGuid(), "Original", "<old />");
        var previousUpdatedUtc = project.UpdatedUtc;

        project.UpdateProject(" Original ", " <old /> ", null);

        Assert.Empty(project.Versions);
        Assert.False(project.CanUndo);
        Assert.Equal(previousUpdatedUtc, project.UpdatedUtc);
    }
}
