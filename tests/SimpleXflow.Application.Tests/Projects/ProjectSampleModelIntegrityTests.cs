using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using SimpleXflow.Application.Projects;

namespace SimpleXflow.Application.Tests.Projects;

public sealed class ProjectSampleModelIntegrityTests
{
    private static readonly XNamespace Bpmn = "http://www.omg.org/spec/BPMN/20100524/MODEL";
    private static readonly XNamespace BpmnDi = "http://www.omg.org/spec/BPMN/20100524/DI";
    private static readonly XNamespace Dc = "http://www.omg.org/spec/DD/20100524/DC";
    private static readonly XNamespace Di = "http://www.omg.org/spec/DD/20100524/DI";

    [Theory]
    [MemberData(nameof(Documents))]
    public void Model_HasBpmnNamespacesAndUniqueXmlIds(string sampleId, bool logic)
    {
        var root = ReadModel(sampleId, logic);

        Assert.Equal(Bpmn + "definitions", root.Name);
        Assert.True(Uri.TryCreate(Attribute(root, "targetNamespace"), UriKind.Absolute, out _));
        Assert.NotEmpty(root.Elements(Bpmn + "process"));
        Assert.NotEmpty(root.Elements(BpmnDi + "BPMNDiagram"));

        var ids = root.DescendantsAndSelf().Attributes("id").Select(attribute => attribute.Value).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (var id in ids)
        {
            Assert.Equal(id, XmlConvert.VerifyNCName(id));
        }
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Model_ReferencesExistingElementsAndParticipantProcesses(string sampleId, bool logic)
    {
        var root = ReadModel(sampleId, logic);
        var elements = IndexElements(root);
        string[] referenceNames = ["sourceRef", "targetRef", "bpmnElement", "processRef", "default"];

        foreach (var reference in root.DescendantsAndSelf().Attributes().Where(attribute => referenceNames.Contains(attribute.Name.LocalName)))
        {
            Assert.True(elements.ContainsKey(reference.Value), $"{sampleId}: {reference.Parent?.Name.LocalName}.{reference.Name} references missing element {reference.Value}.");
        }

        foreach (var participant in root.Descendants(Bpmn + "participant"))
        {
            Assert.Equal(Bpmn + "process", elements[Attribute(participant, "processRef")].Name);
        }

        foreach (var plane in root.Descendants(BpmnDi + "BPMNPlane"))
        {
            Assert.Contains(elements[Attribute(plane, "bpmnElement")].Name, new[] { Bpmn + "process", Bpmn + "collaboration" });
        }
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Model_SequenceFlowsAndNodeReferencesAgreeInBothDirections(string sampleId, bool logic)
    {
        var root = ReadModel(sampleId, logic);
        var elements = IndexElements(root);

        foreach (var process in root.Elements(Bpmn + "process"))
        {
            var flows = process.Elements(Bpmn + "sequenceFlow").ToArray();
            Assert.NotEmpty(flows);

            foreach (var flow in flows)
            {
                var id = Attribute(flow, "id");
                var source = elements[Attribute(flow, "sourceRef")];
                var target = elements[Attribute(flow, "targetRef")];
                Assert.Same(process, source.Parent);
                Assert.Same(process, target.Parent);
                Assert.Single(source.Elements(Bpmn + "outgoing"), element => element.Value == id);
                Assert.Single(target.Elements(Bpmn + "incoming"), element => element.Value == id);
            }

            foreach (var reference in process.Descendants().Where(element => element.Name == Bpmn + "incoming" || element.Name == Bpmn + "outgoing"))
            {
                Assert.True(elements.TryGetValue(reference.Value, out var flow), $"{sampleId}: missing sequence flow {reference.Value}.");
                Assert.Equal(Bpmn + "sequenceFlow", flow!.Name);
                var direction = reference.Name == Bpmn + "incoming" ? "targetRef" : "sourceRef";
                Assert.Equal(Attribute(reference.Parent!, "id"), Attribute(flow, direction));
            }
        }
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Model_EveryFlowNodeIsReachableFromStartAndCanReachAnEnd(string sampleId, bool logic)
    {
        var root = ReadModel(sampleId, logic);

        foreach (var process in root.Elements(Bpmn + "process"))
        {
            var starts = process.Elements(Bpmn + "startEvent").ToArray();
            var ends = process.Elements(Bpmn + "endEvent").ToArray();
            Assert.NotEmpty(starts);
            Assert.NotEmpty(ends);
            Assert.All(starts, start => Assert.Empty(start.Elements(Bpmn + "incoming")));
            Assert.All(ends, end => Assert.Empty(end.Elements(Bpmn + "outgoing")));

            var flows = process.Elements(Bpmn + "sequenceFlow")
                .Select(flow => (Source: Attribute(flow, "sourceRef"), Target: Attribute(flow, "targetRef")))
                .ToArray();
            var fromStart = Reachable(starts.Select(start => Attribute(start, "id")), flows);
            var toEnd = Reachable(ends.Select(end => Attribute(end, "id")), flows.Select(flow => (flow.Target, flow.Source)));
            var flowNodes = process.Elements().Where(IsFlowNode);

            foreach (var node in flowNodes)
            {
                var id = Attribute(node, "id");
                Assert.True(fromStart.Contains(id), $"{sampleId}: flow node {id} cannot be reached from a start event.");
                Assert.True(toEnd.Contains(id), $"{sampleId}: flow node {id} has no path to an end event.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Documents))]
    public void Model_DiagramHasFiniteGeometryAndDisplaysEveryFlowNodeAndConnection(string sampleId, bool logic)
    {
        var root = ReadModel(sampleId, logic);
        var elements = IndexElements(root);
        var shapes = root.Descendants(BpmnDi + "BPMNShape").ToArray();
        var edges = root.Descendants(BpmnDi + "BPMNEdge").ToArray();
        Assert.NotEmpty(shapes);
        Assert.NotEmpty(edges);

        foreach (var shape in shapes)
        {
            Assert.True(elements.ContainsKey(Attribute(shape, "bpmnElement")));
            var bounds = Assert.Single(shape.Elements(Dc + "Bounds"));
            Assert.True(Number(bounds, "width") > 0);
            Assert.True(Number(bounds, "height") > 0);
            Number(bounds, "x");
            Number(bounds, "y");
        }

        foreach (var edge in edges)
        {
            var connection = elements[Attribute(edge, "bpmnElement")];
            Assert.Contains(connection.Name, new[] { Bpmn + "sequenceFlow", Bpmn + "association", Bpmn + "messageFlow" });
            var waypoints = edge.Elements(Di + "waypoint").ToArray();
            Assert.True(waypoints.Length >= 2, $"{sampleId}: edge {Attribute(edge, "id")} requires at least two waypoints.");
            foreach (var point in waypoints)
            {
                Number(point, "x");
                Number(point, "y");
            }
        }

        foreach (var node in root.Elements(Bpmn + "process").SelectMany(process => process.Elements()).Where(IsFlowNode))
        {
            Assert.Single(shapes, shape => Attribute(shape, "bpmnElement") == Attribute(node, "id"));
        }

        foreach (var connection in root.Descendants().Where(element => element.Name == Bpmn + "sequenceFlow" || element.Name == Bpmn + "association"))
        {
            var id = Attribute(connection, "id");
            Assert.True(edges.Count(edge => Attribute(edge, "bpmnElement") == id) == 1,
                $"{sampleId}: connection {id} must have exactly one BPMNEdge so it is visible in the diagram.");
        }
    }

    [Fact]
    public void HospitalRoomLogic_UsesSimulationElementsAndReferencesArchitectureResources()
    {
        var architecture = ReadModel("poster-hospital-er", logic: false);
        var logic = ReadModel("poster-hospital-er", logic: true);
        XNamespace regular = "http://regularBPMN";
        XNamespace simulation = "http://simBPMN";

        Assert.Single(logic.Descendants(simulation + "queue"));
        Assert.Single(logic.Descendants(simulation + "output"));
        foreach (var resource in architecture.Descendants(regular + "resource"))
        {
            var logicResource = Assert.Single(logic.Descendants(simulation + "resource"), element => Attribute(element, "id") == Attribute(resource, "id"));
            Assert.Equal(Attribute(resource, "name"), Attribute(logicResource, "name"));
            Assert.Contains(logic.Descendants(Bpmn + "association"), association => Attribute(association, "sourceRef") == Attribute(logicResource, "id"));
        }
    }

    public static IEnumerable<object[]> Documents()
    {
        foreach (var sample in ProjectSamples.All)
        {
            yield return [sample.Id, false];
            if (sample.LogicXml is not null)
            {
                yield return [sample.Id, true];
            }
        }
    }

    private static XElement ReadModel(string sampleId, bool logic)
    {
        var sample = Assert.IsType<ProjectSample>(ProjectSamples.Find(sampleId));
        var xml = logic ? sample.LogicXml : sample.BpmnXml;
        Assert.NotNull(xml);
        return Assert.IsType<XElement>(XDocument.Parse(xml).Root);
    }

    private static Dictionary<string, XElement> IndexElements(XElement root) => root.DescendantsAndSelf()
        .Where(element => element.Attribute("id") is not null)
        .ToDictionary(element => Attribute(element, "id"), StringComparer.Ordinal);

    private static string Attribute(XElement element, string name)
    {
        var attribute = element.Attribute(name);
        Assert.NotNull(attribute);
        Assert.False(string.IsNullOrWhiteSpace(attribute.Value));
        return attribute.Value;
    }

    private static double Number(XElement element, string attribute)
    {
        Assert.True(double.TryParse(Attribute(element, attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var value));
        Assert.True(double.IsFinite(value));
        return value;
    }

    private static bool IsFlowNode(XElement element) =>
        element.Name.LocalName is "task" or "userTask" or "serviceTask" or "subProcess"
            or "startEvent" or "endEvent" or "intermediateCatchEvent" or "intermediateThrowEvent"
            or "exclusiveGateway" or "parallelGateway" or "inclusiveGateway" or "queue" or "output"
        || element.Elements(Bpmn + "incoming").Any() || element.Elements(Bpmn + "outgoing").Any();

    private static HashSet<string> Reachable(IEnumerable<string> startIds, IEnumerable<(string Source, string Target)> edges)
    {
        var adjacency = edges.ToLookup(edge => edge.Source, edge => edge.Target, StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(startIds);
        while (pending.TryDequeue(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (var next in adjacency[current])
            {
                pending.Enqueue(next);
            }
        }

        return visited;
    }
}
