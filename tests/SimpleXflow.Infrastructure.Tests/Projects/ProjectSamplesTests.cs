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

    public static IEnumerable<object[]> Samples() =>
        ProjectSamples.All.Select(sample => new object[] { sample });
}
