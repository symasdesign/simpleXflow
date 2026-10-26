using System.Xml.Linq;
using SimpleXflow.Application.Projects;

namespace SimpleXflow.Application.Tests.Projects;

public sealed class ProjectSamplesTests
{
    [Fact]
    public void All_IncludesPaperPresentationSamples()
    {
        var sampleIds = ProjectSamples.All.Select(sample => sample.Id).ToArray();

        Assert.Contains("paper-coffee-break", sampleIds);
        Assert.Contains("paper-mm1-queue", sampleIds);
        Assert.Contains("poster-hospital-er", sampleIds);
    }

    [Fact]
    public void All_HasUniqueIdsAndNames()
    {
        Assert.Equal(
            ProjectSamples.All.Count,
            ProjectSamples.All.Select(sample => sample.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            ProjectSamples.All.Count,
            ProjectSamples.All.Select(sample => sample.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void All_ContainsValidBpmnXml(string sampleId)
    {
        var sample = Assert.IsType<ProjectSample>(ProjectSamples.Find(sampleId));
        var document = XDocument.Parse(sample.BpmnXml);
        var root = Assert.IsType<XElement>(document.Root);

        AssertBpmnDocument(root);

        if (!string.IsNullOrWhiteSpace(sample.LogicXml))
        {
            var logicDocument = XDocument.Parse(sample.LogicXml);
            var logicRoot = Assert.IsType<XElement>(logicDocument.Root);

            AssertBpmnDocument(logicRoot);
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Find_ReturnsSampleIndependentOfIdCasing(string sampleId)
    {
        var expected = Assert.Single(ProjectSamples.All, sample => sample.Id == sampleId);

        Assert.Same(expected, ProjectSamples.Find(sampleId));
        Assert.Same(expected, ProjectSamples.Find(sampleId.ToUpperInvariant()));
        Assert.Same(expected, ProjectSamples.Find(sampleId.ToLowerInvariant()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("does-not-exist")]
    [InlineData("paper-coffee")]
    [InlineData(" paper-coffee-break ")]
    public void Find_UnknownOrIncompleteIdReturnsNull(string? sampleId)
    {
        Assert.Null(ProjectSamples.Find(sampleId));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void All_HasUsableCatalogMetadataAndCoherentLogicTarget(string sampleId)
    {
        var sample = Assert.IsType<ProjectSample>(ProjectSamples.Find(sampleId));

        Assert.Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$", sample.Id);
        Assert.False(string.IsNullOrWhiteSpace(sample.Name));
        Assert.False(string.IsNullOrWhiteSpace(sample.Description));
        Assert.Equal(sample.Name.Trim(), sample.Name);

        if (sample.LogicXml is null)
        {
            Assert.Null(sample.LogicTargetElementId);
            return;
        }

        Assert.False(string.IsNullOrWhiteSpace(sample.LogicXml));
        Assert.False(string.IsNullOrWhiteSpace(sample.LogicTargetElementId));
        var architecture = XDocument.Parse(sample.BpmnXml);
        var target = Assert.Single(architecture.Descendants(), element => element.Attribute("id")?.Value == sample.LogicTargetElementId);
        Assert.Equal("task", target.Name.LocalName);
    }

    [Fact]
    public void HospitalEmergencyRoomSample_SplitsArchitectureAndRoom1Logic()
    {
        var sample = Assert.Single(ProjectSamples.All, sample => sample.Id == "poster-hospital-er");
        var architectureDocument = XDocument.Parse(sample.BpmnXml);
        Assert.NotNull(sample.LogicXml);
        var logicDocument = XDocument.Parse(sample.LogicXml);

        Assert.Equal("Task_Room1", sample.LogicTargetElementId);
        Assert.Contains(architectureDocument.Descendants(), element => element.Attribute("name")?.Value == "Check-In");
        Assert.Contains(architectureDocument.Descendants(), element => element.Attribute("name")?.Value == "Room1");
        Assert.Contains(architectureDocument.Descendants(), element => element.Attribute("name")?.Value == "Doctors");
        Assert.DoesNotContain(architectureDocument.Descendants(), element => element.Attribute("name")?.Value == "Examination and Diagnosis");
        Assert.DoesNotContain(architectureDocument.Descendants(), element => element.Attribute("name")?.Value == "Disinfection");

        Assert.Contains(logicDocument.Descendants(), element => element.Attribute("name")?.Value == "Initial Treatment");
        Assert.Contains(logicDocument.Descendants(), element => element.Attribute("name")?.Value == "Examination and Diagnosis");
        Assert.Contains(logicDocument.Descendants(), element => element.Attribute("name")?.Value == "Disinfection");
        Assert.Contains(logicDocument.Descendants(), element => element.Value.Contains("capacity C = 3 doctors", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertBpmnDocument(XElement root)
    {
        Assert.Equal("definitions", root.Name.LocalName);
        Assert.Contains(root.Descendants(), element => element.Name.LocalName == "process");
        Assert.Contains(root.Descendants(), element => element.Name.LocalName == "BPMNDiagram");
        Assert.Contains(root.Descendants(), element => element.Name.LocalName == "BPMNShape");
        Assert.Contains(root.Descendants(), element => element.Name.LocalName == "BPMNEdge");
    }

    public static IEnumerable<object[]> Samples() =>
        ProjectSamples.All.Select(sample => new object[] { sample.Id });
}
