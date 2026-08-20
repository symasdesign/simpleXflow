using System.Xml.Linq;
using SimpleXflow.Application.Projects;

namespace SimpleXflow.Infrastructure.Tests.Projects;

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
    public void All_ContainsValidBpmnXml(ProjectSample sample)
    {
        var document = XDocument.Parse(sample.BpmnXml);
        var root = Assert.IsType<XElement>(document.Root);

        Assert.Equal("definitions", root.Name.LocalName);
        Assert.Contains(root.Descendants(), element => element.Name.LocalName == "process");
        Assert.Contains(root.Descendants(), element => element.Name.LocalName == "BPMNDiagram");
        Assert.Contains(root.Descendants(), element => element.Name.LocalName == "BPMNShape");
        Assert.Contains(root.Descendants(), element => element.Name.LocalName == "BPMNEdge");
    }

    [Fact]
    public void HospitalEmergencyRoomSample_ContainsPosterScenarioElements()
    {
        var sample = Assert.Single(ProjectSamples.All, sample => sample.Id == "poster-hospital-er");
        var document = XDocument.Parse(sample.BpmnXml);

        Assert.Contains(document.Descendants(), element => element.Attribute("name")?.Value == "Check-In");
        Assert.Contains(document.Descendants(), element => element.Attribute("name")?.Value == "Room1");
        Assert.Contains(document.Descendants(), element => element.Attribute("name")?.Value == "Examination and Diagnosis");
        Assert.Contains(document.Descendants(), element => element.Attribute("name")?.Value == "Disinfection");
        Assert.Contains(document.Descendants(), element => element.Value.Contains("Doctors resource", StringComparison.OrdinalIgnoreCase));
    }

    public static IEnumerable<object[]> Samples() =>
        ProjectSamples.All.Select(sample => new object[] { sample });
}
