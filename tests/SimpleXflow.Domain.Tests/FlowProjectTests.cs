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
    public void UpdateProject_CapturesSavedVersion()
    {
        var project = new FlowProject(Guid.NewGuid(), "Original", "<old />");

        project.UpdateProject("Changed", "<new />", "<logic />");

        Assert.Equal("Changed", project.Name);
        Assert.Equal("<new />", project.BpmnXml);
        Assert.Equal("<logic />", project.LogicXml);
        Assert.True(project.CanUndo);
        var version = Assert.Single(project.Versions);
        Assert.Equal("Original", version.Name);
        Assert.Equal("<old />", version.BpmnXml);
    }

    [Fact]
    public void UndoLastChange_RestoresSavedVersionsStepByStep()
    {
        var project = new FlowProject(Guid.NewGuid(), "Original", "<old />");
        project.UpdateProject("Changed", "<new />", "<logic />");
        project.UpdateProject("Changed again", "<newer />", "<newer-logic />");

        project.UndoLastChange();

        Assert.Equal("Changed", project.Name);
        Assert.Equal("<new />", project.BpmnXml);
        Assert.Equal("<logic />", project.LogicXml);
        Assert.True(project.CanUndo);

        project.UndoLastChange();

        Assert.Equal("Original", project.Name);
        Assert.Equal("<old />", project.BpmnXml);
        Assert.Null(project.LogicXml);
        Assert.False(project.CanUndo);
    }

    [Fact]
    public void UpdateProject_DoesNotCreateUndoVersionWhenNothingChanged()
    {
        var project = new FlowProject(Guid.NewGuid(), "Original", "<old />");

        project.UpdateProject(" Original ", " <old /> ", null);

        Assert.Empty(project.Versions);
        Assert.False(project.CanUndo);
    }
}
