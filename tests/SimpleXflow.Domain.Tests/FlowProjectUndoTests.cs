using System.Reflection;
using SimpleXflow.Domain.Projects;

namespace SimpleXflow.Domain.Tests;

public sealed class FlowProjectUndoTests
{
    [Fact]
    public void UndoLastChange_RestoresHighestVersionNumberRegardlessOfInsertionOrder()
    {
        var project = new FlowProject(Guid.NewGuid(), "Current", "<current />");
        var createdUtc = project.CreatedUtc;
        var highest = Version(project, 7, "Latest saved", "<latest />", "<latest-logic />");
        var oldest = Version(project, 1, "Oldest saved", "<oldest />", null);
        var middle = Version(project, 3, "Middle saved", "<middle />", "<middle-logic />");
        HydrateVersions(project, middle, highest, oldest);
        var beforeUndo = DateTimeOffset.UtcNow;

        project.UndoLastChange();

        Assert.Equal("Latest saved", project.Name);
        Assert.Equal("<latest />", project.BpmnXml);
        Assert.Equal("<latest-logic />", project.LogicXml);
        Assert.Equal(createdUtc, project.CreatedUtc);
        Assert.InRange(project.UpdatedUtc, beforeUndo, DateTimeOffset.UtcNow);
        Assert.Equal(new[] { oldest.Id, middle.Id }.Order(), project.Versions.Select(version => version.Id).Order());
        Assert.True(project.CanUndo);
    }

    [Fact]
    public void UndoLastChange_ConsumesHistoryUntilItIsEmptyWithoutCreatingRedoVersions()
    {
        var project = new FlowProject(Guid.NewGuid(), "Current", "<current />");
        HydrateVersions(project,
            Version(project, 2, "Second", "<second />", "<logic />"),
            Version(project, 1, "First", "<first />", null));

        project.UndoLastChange();
        Assert.Equal("Second", project.Name);
        Assert.Single(project.Versions);
        Assert.True(project.CanUndo);

        project.UndoLastChange();
        Assert.Equal("First", project.Name);
        Assert.Equal("<first />", project.BpmnXml);
        Assert.Null(project.LogicXml);
        Assert.Empty(project.Versions);
        Assert.False(project.CanUndo);
        var lastUpdated = project.UpdatedUtc;

        Assert.Throws<InvalidOperationException>(project.UndoLastChange);
        Assert.Equal("First", project.Name);
        Assert.Equal(lastUpdated, project.UpdatedUtc);
    }

    [Fact]
    public void Autosave_LeavesExistingVersionHistoryAvailableForUndo()
    {
        var project = new FlowProject(Guid.NewGuid(), "Current", "<current />");
        var savedVersion = Version(project, 1, "Saved", "<saved />", "<saved-logic />");
        HydrateVersions(project, savedVersion);

        project.UpdateProject("Autosaved", "<autosaved />", "<autosaved-logic />");

        Assert.Same(savedVersion, Assert.Single(project.Versions));
        Assert.True(project.CanUndo);
        project.UndoLastChange();
        Assert.Equal("Saved", project.Name);
        Assert.Equal("<saved />", project.BpmnXml);
        Assert.Equal("<saved-logic />", project.LogicXml);
    }

    [Fact]
    public void UndoLastChange_RestoresAndClearsLegacySnapshot()
    {
        var project = new FlowProject(Guid.NewGuid(), "Current", "<current />");
        project.UpdateModel(project.BpmnXml, "<current-logic />");
        HydrateLegacySnapshot(project, "Legacy", "<legacy />", "<legacy-logic />", DateTimeOffset.UnixEpoch);
        var beforeUndo = DateTimeOffset.UtcNow;

        Assert.True(project.CanUndo);
        project.UndoLastChange();

        Assert.Equal("Legacy", project.Name);
        Assert.Equal("<legacy />", project.BpmnXml);
        Assert.Equal("<legacy-logic />", project.LogicXml);
        Assert.InRange(project.UpdatedUtc, beforeUndo, DateTimeOffset.UtcNow);
        Assert.Null(project.PreviousName);
        Assert.Null(project.PreviousBpmnXml);
        Assert.Null(project.PreviousLogicXml);
        Assert.Null(project.PreviousUpdatedUtc);
        Assert.False(project.CanUndo);
    }

    [Fact]
    public void UndoLastChange_LegacySnapshotWithoutOptionalFieldsKeepsNameAndClearsLogic()
    {
        var project = new FlowProject(Guid.NewGuid(), "Current", "<current />");
        project.UpdateModel(project.BpmnXml, "<current-logic />");
        HydrateLegacySnapshot(project, null, "<legacy />", null, null);

        project.UndoLastChange();

        Assert.Equal("Current", project.Name);
        Assert.Equal("<legacy />", project.BpmnXml);
        Assert.Null(project.LogicXml);
        Assert.False(project.CanUndo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void LegacySnapshot_RequiresNonemptyModelForUndo(string? model)
    {
        var project = new FlowProject(Guid.NewGuid(), "Current", "<current />");
        HydrateLegacySnapshot(project, "Legacy", model, "<logic />", DateTimeOffset.UnixEpoch);

        Assert.False(project.CanUndo);
        Assert.Throws<InvalidOperationException>(project.UndoLastChange);
        Assert.Equal("Current", project.Name);
        Assert.Equal("<current />", project.BpmnXml);
    }

    [Fact]
    public void UndoLastChange_UsesVersionHistoryBeforeLegacySnapshot()
    {
        var project = new FlowProject(Guid.NewGuid(), "Current", "<current />");
        HydrateVersions(project, Version(project, 4, "Saved", "<saved />", null));
        HydrateLegacySnapshot(project, "Legacy", "<legacy />", null, null);

        project.UndoLastChange();

        Assert.Equal("Saved", project.Name);
        Assert.Empty(project.Versions);
        Assert.True(project.CanUndo);
        Assert.Equal("<legacy />", project.PreviousBpmnXml);
        project.UndoLastChange();
        Assert.Equal("Legacy", project.Name);
        Assert.False(project.CanUndo);
    }

    private static ProjectVersion Version(FlowProject project, int number, string name, string model, string? logic) =>
        new(project.TenantId, project.Id, number, name, model, logic);

    // Versions and legacy snapshots are populated by EF during materialization; there is no public mutation API.
    private static void HydrateVersions(FlowProject project, params ProjectVersion[] versions)
    {
        var field = typeof(FlowProject).GetField("_versions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var collection = Assert.IsType<List<ProjectVersion>>(field.GetValue(project));
        collection.AddRange(versions);
    }

    private static void HydrateLegacySnapshot(FlowProject project, string? name, string? model, string? logic, DateTimeOffset? updatedUtc)
    {
        typeof(FlowProject).GetProperty(nameof(FlowProject.PreviousName))!.SetValue(project, name);
        typeof(FlowProject).GetProperty(nameof(FlowProject.PreviousBpmnXml))!.SetValue(project, model);
        typeof(FlowProject).GetProperty(nameof(FlowProject.PreviousLogicXml))!.SetValue(project, logic);
        typeof(FlowProject).GetProperty(nameof(FlowProject.PreviousUpdatedUtc))!.SetValue(project, updatedUtc);
    }
}
