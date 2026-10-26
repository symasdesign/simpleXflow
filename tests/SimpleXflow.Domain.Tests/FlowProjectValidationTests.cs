using SimpleXflow.Domain.Projects;

namespace SimpleXflow.Domain.Tests;

public sealed class FlowProjectValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Constructor_RejectsMissingName(string? name)
    {
        Assert.Throws<ArgumentException>("name", () => new FlowProject(Guid.NewGuid(), name!, "<model />"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Constructor_RejectsMissingModel(string? model)
    {
        Assert.Throws<ArgumentException>("bpmnXml", () => new FlowProject(Guid.NewGuid(), "Project", model!));
    }

    [Fact]
    public void Constructor_TrimsOuterWhitespaceAndPreservesModelContents()
    {
        const string xml = "<model>\n  <task name=\"Keep  spacing\" />\n</model>";
        var before = DateTimeOffset.UtcNow;

        var project = new FlowProject(Guid.NewGuid(), " \tProject name\r\n", $" \n{xml}\t ");

        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("Project name", project.Name);
        Assert.Equal(xml, project.BpmnXml);
        Assert.InRange(project.CreatedUtc, before, DateTimeOffset.UtcNow);
        Assert.Equal(project.CreatedUtc, project.UpdatedUtc);
        Assert.Null(project.LogicXml);
        Assert.Empty(project.Attachments);
        Assert.Empty(project.Versions);
        Assert.False(project.CanUndo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void Rename_InvalidNameLeavesProjectUnchanged(string? name)
    {
        var project = CreateProject();
        var before = Capture(project);

        Assert.Throws<ArgumentException>("name", () => project.Rename(name!));

        Assert.Equal(before, Capture(project));
    }

    [Fact]
    public void Rename_NormalizesNameAndPreservesModelIdentityAndCreationTime()
    {
        var project = CreateProject();
        var before = Capture(project);
        var changedAfter = DateTimeOffset.UtcNow;

        project.Rename(" \tRenamed project\n ");

        Assert.Equal("Renamed project", project.Name);
        Assert.Equal(before.Id, project.Id);
        Assert.Equal(before.TenantId, project.TenantId);
        Assert.Equal(before.CreatedUtc, project.CreatedUtc);
        Assert.Equal(before.BpmnXml, project.BpmnXml);
        Assert.Equal(before.LogicXml, project.LogicXml);
        Assert.InRange(project.UpdatedUtc, changedAfter, DateTimeOffset.UtcNow);
        Assert.False(project.CanUndo);
    }

    [Theory]
    [InlineData(null, "<changed />", "name")]
    [InlineData("", "<changed />", "name")]
    [InlineData(" \t ", "<changed />", "name")]
    [InlineData("Changed", null, "bpmnXml")]
    [InlineData("Changed", "", "bpmnXml")]
    [InlineData("Changed", " \t ", "bpmnXml")]
    public void UpdateProject_ValidatesAllInputsBeforeChangingState(string? name, string? model, string parameter)
    {
        var project = CreateProject();
        var before = Capture(project);

        Assert.Throws<ArgumentException>(parameter, () => project.UpdateProject(name!, model!, "<changed-logic />"));

        Assert.Equal(before, Capture(project));
        Assert.False(project.CanUndo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void UpdateModel_InvalidModelLeavesExistingLogicAndTimestampsUnchanged(string? model)
    {
        var project = CreateProject();
        var before = Capture(project);

        Assert.Throws<ArgumentException>("bpmnXml", () => project.UpdateModel(model!, "<changed-logic />"));

        Assert.Equal(before, Capture(project));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void UpdateProject_EmptyLogicRemovesPreviouslySavedLogic(string? logic)
    {
        var project = CreateProject();

        project.UpdateProject(project.Name, project.BpmnXml, logic);

        Assert.Null(project.LogicXml);
        Assert.False(project.CanUndo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n ")]
    public void UpdateModel_EmptyLogicRemovesPreviouslySavedLogic(string? logic)
    {
        var project = CreateProject();

        project.UpdateModel("<new />", logic);

        Assert.Equal("<new />", project.BpmnXml);
        Assert.Null(project.LogicXml);
        Assert.False(project.CanUndo);
    }

    [Fact]
    public void UpdateProject_NormalizesAllFieldsAndPreservesIdentityAndCreationTime()
    {
        var project = CreateProject();
        var before = Capture(project);
        var changedAfter = DateTimeOffset.UtcNow;

        project.UpdateProject(" \tChanged\n", " \n<changed />\t", " \n<logic>  preserve  </logic>\t ");

        Assert.Equal("Changed", project.Name);
        Assert.Equal("<changed />", project.BpmnXml);
        Assert.Equal("<logic>  preserve  </logic>", project.LogicXml);
        Assert.Equal(before.Id, project.Id);
        Assert.Equal(before.TenantId, project.TenantId);
        Assert.Equal(before.CreatedUtc, project.CreatedUtc);
        Assert.InRange(project.UpdatedUtc, changedAfter, DateTimeOffset.UtcNow);
        Assert.Empty(project.Versions);
    }

    [Fact]
    public void UpdateModel_NormalizesLogicConsistentlyWithProjectUpdates()
    {
        var project = CreateProject();

        project.UpdateModel(" \n<changed />\t ", " \n<logic>  preserve  </logic>\t ");
        var afterModelUpdate = project.UpdatedUtc;

        Assert.Equal("<changed />", project.BpmnXml);
        Assert.Equal("<logic>  preserve  </logic>", project.LogicXml);
        project.UpdateProject(project.Name, project.BpmnXml, "<logic>  preserve  </logic>");
        Assert.Equal(afterModelUpdate, project.UpdatedUtc);
    }

    [Fact]
    public void UpdateProject_EquivalentNormalizedStateDoesNotTouchProject()
    {
        var project = CreateProject();
        var before = Capture(project);

        project.UpdateProject(" \nOriginal\t ", " \n<original />\t ", " \n<original-logic />\t ");

        Assert.Equal(before, Capture(project));
        Assert.Empty(project.Versions);
    }

    [Fact]
    public void UpdateProject_LogicOnlyChangeIsSaved()
    {
        var project = CreateProject();
        var before = Capture(project);

        project.UpdateProject(project.Name, project.BpmnXml, "<different-logic />");

        Assert.Equal("<different-logic />", project.LogicXml);
        Assert.Equal(before.Name, project.Name);
        Assert.Equal(before.BpmnXml, project.BpmnXml);
        Assert.True(project.UpdatedUtc >= before.UpdatedUtc);
        Assert.False(project.CanUndo);
    }

    private static FlowProject CreateProject()
    {
        var project = new FlowProject(Guid.NewGuid(), "Original", "<original />");
        project.UpdateModel(project.BpmnXml, "<original-logic />");
        return project;
    }

    private static ProjectState Capture(FlowProject project) => new(
        project.Id, project.TenantId, project.Name, project.BpmnXml, project.LogicXml, project.CreatedUtc, project.UpdatedUtc);

    private sealed record ProjectState(
        Guid Id, Guid TenantId, string Name, string BpmnXml, string? LogicXml, DateTimeOffset CreatedUtc, DateTimeOffset UpdatedUtc);
}
