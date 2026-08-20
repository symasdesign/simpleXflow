namespace SimpleXflow.Application.Projects;

public static class ProjectSamples
{
    public static IReadOnlyList<ProjectSample> All { get; } =
    [
        new(
            "paper-coffee-break",
            "Paper sample - Coffee break",
            "Architecture view from the EUROSIM 2026 coffee-break example: visitor routing, refrigerator use, hot beverages and seating.",
            CoffeeBreakArchitectureXml),
        new(
            "paper-mm1-queue",
            "Paper sample - M/M/1 queue",
            "Compact reference model for the M/M/1 mapping: arrivals, FIFO queue, capacity-1 server and exponential service.",
            Mm1QueueXml)
    ];

    public static ProjectSample? Find(string? id) =>
        All.FirstOrDefault(sample => sample.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    private const string CoffeeBreakArchitectureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn2:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn2="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_CoffeeBreak" targetNamespace="https://simplexflow.ch/samples/eurosim2026">
          <bpmn2:collaboration id="Collaboration_CoffeeBreak">
            <bpmn2:participant id="Participant_CoffeeBreak" name="Coffee-break system architecture" processRef="Process_CoffeeBreak" />
            <bpmn2:textAnnotation id="TextAnnotation_CoffeeBreak_Context">
              <bpmn2:text>EUROSIM 2026 paper sample. Peak: 200 visitors. Routing: 20% direct seating, 40% refrigerator then seating, 40% hot beverages. After hot beverages: 50% seating, 50% refrigerator then seating.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:textAnnotation id="TextAnnotation_CoffeeBreak_Resources">
              <bpmn2:text>Modeled resources: coffee beans, milk, paper cups. Infrastructure such as water and electricity is assumed available and documented as context.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:association id="Association_CoffeeBreak_Context" sourceRef="TextAnnotation_CoffeeBreak_Context" targetRef="Gateway_InitialChoice" />
            <bpmn2:association id="Association_CoffeeBreak_Resources" sourceRef="TextAnnotation_CoffeeBreak_Resources" targetRef="Task_HotBeverages" />
          </bpmn2:collaboration>
          <bpmn2:process id="Process_CoffeeBreak" name="Coffee-break visitor architecture" isExecutable="false">
            <bpmn2:startEvent id="StartEvent_VisitorsArrive" name="Visitors arrive">
              <bpmn2:outgoing>Flow_Arrive_To_Choice</bpmn2:outgoing>
            </bpmn2:startEvent>
            <bpmn2:exclusiveGateway id="Gateway_InitialChoice" name="Visitor route">
              <bpmn2:incoming>Flow_Arrive_To_Choice</bpmn2:incoming>
              <bpmn2:outgoing>Flow_DirectSeat</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_Refrigerator</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_HotBeverages</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_Refrigerator" name="Use refrigerator">
              <bpmn2:incoming>Flow_Refrigerator</bpmn2:incoming>
              <bpmn2:incoming>Flow_AfterHot_To_Refrigerator</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Refrigerator_To_Seat</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_HotBeverages" name="Hot beverages / Barista">
              <bpmn2:incoming>Flow_HotBeverages</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Hot_To_Gateway</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_AfterHot" name="After hot beverage">
              <bpmn2:incoming>Flow_Hot_To_Gateway</bpmn2:incoming>
              <bpmn2:outgoing>Flow_AfterHot_To_Seat</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_AfterHot_To_Refrigerator</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_Seating" name="Take a seat">
              <bpmn2:incoming>Flow_DirectSeat</bpmn2:incoming>
              <bpmn2:incoming>Flow_Refrigerator_To_Seat</bpmn2:incoming>
              <bpmn2:incoming>Flow_AfterHot_To_Seat</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Seat_To_End</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:endEvent id="EndEvent_Seated" name="Visitor seated">
              <bpmn2:incoming>Flow_Seat_To_End</bpmn2:incoming>
            </bpmn2:endEvent>
            <bpmn2:sequenceFlow id="Flow_Arrive_To_Choice" sourceRef="StartEvent_VisitorsArrive" targetRef="Gateway_InitialChoice" />
            <bpmn2:sequenceFlow id="Flow_DirectSeat" name="20%" sourceRef="Gateway_InitialChoice" targetRef="Task_Seating" />
            <bpmn2:sequenceFlow id="Flow_Refrigerator" name="40%" sourceRef="Gateway_InitialChoice" targetRef="Task_Refrigerator" />
            <bpmn2:sequenceFlow id="Flow_HotBeverages" name="40%" sourceRef="Gateway_InitialChoice" targetRef="Task_HotBeverages" />
            <bpmn2:sequenceFlow id="Flow_Hot_To_Gateway" sourceRef="Task_HotBeverages" targetRef="Gateway_AfterHot" />
            <bpmn2:sequenceFlow id="Flow_AfterHot_To_Seat" name="50%" sourceRef="Gateway_AfterHot" targetRef="Task_Seating" />
            <bpmn2:sequenceFlow id="Flow_AfterHot_To_Refrigerator" name="50%" sourceRef="Gateway_AfterHot" targetRef="Task_Refrigerator" />
            <bpmn2:sequenceFlow id="Flow_Refrigerator_To_Seat" sourceRef="Task_Refrigerator" targetRef="Task_Seating" />
            <bpmn2:sequenceFlow id="Flow_Seat_To_End" sourceRef="Task_Seating" targetRef="EndEvent_Seated" />
          </bpmn2:process>
          <bpmndi:BPMNDiagram id="BPMNDiagram_CoffeeBreak">
            <bpmndi:BPMNPlane id="BPMNPlane_CoffeeBreak" bpmnElement="Collaboration_CoffeeBreak">
              <bpmndi:BPMNShape id="Participant_CoffeeBreak_di" bpmnElement="Participant_CoffeeBreak" isHorizontal="true">
                <dc:Bounds x="120" y="80" width="980" height="460" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="StartEvent_VisitorsArrive_di" bpmnElement="StartEvent_VisitorsArrive">
                <dc:Bounds x="170" y="250" width="36" height="36" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_InitialChoice_di" bpmnElement="Gateway_InitialChoice" isMarkerVisible="true">
                <dc:Bounds x="270" y="243" width="50" height="50" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Refrigerator_di" bpmnElement="Task_Refrigerator">
                <dc:Bounds x="450" y="165" width="145" height="70" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_HotBeverages_di" bpmnElement="Task_HotBeverages">
                <dc:Bounds x="450" y="300" width="160" height="70" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_AfterHot_di" bpmnElement="Gateway_AfterHot" isMarkerVisible="true">
                <dc:Bounds x="680" y="310" width="50" height="50" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Seating_di" bpmnElement="Task_Seating">
                <dc:Bounds x="820" y="230" width="135" height="70" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="EndEvent_Seated_di" bpmnElement="EndEvent_Seated">
                <dc:Bounds x="1010" y="248" width="36" height="36" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_CoffeeBreak_Context_di" bpmnElement="TextAnnotation_CoffeeBreak_Context">
                <dc:Bounds x="250" y="405" width="335" height="80" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_CoffeeBreak_Resources_di" bpmnElement="TextAnnotation_CoffeeBreak_Resources">
                <dc:Bounds x="650" y="405" width="320" height="80" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNEdge id="Flow_Arrive_To_Choice_di" bpmnElement="Flow_Arrive_To_Choice">
                <di:waypoint x="206" y="268" />
                <di:waypoint x="270" y="268" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_DirectSeat_di" bpmnElement="Flow_DirectSeat">
                <di:waypoint x="320" y="268" />
                <di:waypoint x="820" y="265" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Refrigerator_di" bpmnElement="Flow_Refrigerator">
                <di:waypoint x="295" y="243" />
                <di:waypoint x="295" y="200" />
                <di:waypoint x="450" y="200" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_HotBeverages_di" bpmnElement="Flow_HotBeverages">
                <di:waypoint x="295" y="293" />
                <di:waypoint x="295" y="335" />
                <di:waypoint x="450" y="335" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Hot_To_Gateway_di" bpmnElement="Flow_Hot_To_Gateway">
                <di:waypoint x="610" y="335" />
                <di:waypoint x="680" y="335" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_AfterHot_To_Seat_di" bpmnElement="Flow_AfterHot_To_Seat">
                <di:waypoint x="730" y="335" />
                <di:waypoint x="770" y="335" />
                <di:waypoint x="770" y="265" />
                <di:waypoint x="820" y="265" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_AfterHot_To_Refrigerator_di" bpmnElement="Flow_AfterHot_To_Refrigerator">
                <di:waypoint x="705" y="310" />
                <di:waypoint x="705" y="200" />
                <di:waypoint x="595" y="200" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Refrigerator_To_Seat_di" bpmnElement="Flow_Refrigerator_To_Seat">
                <di:waypoint x="595" y="200" />
                <di:waypoint x="760" y="200" />
                <di:waypoint x="760" y="250" />
                <di:waypoint x="820" y="250" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Seat_To_End_di" bpmnElement="Flow_Seat_To_End">
                <di:waypoint x="955" y="265" />
                <di:waypoint x="1010" y="266" />
              </bpmndi:BPMNEdge>
            </bpmndi:BPMNPlane>
          </bpmndi:BPMNDiagram>
        </bpmn2:definitions>
        """;

    private const string Mm1QueueXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn2:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn2="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_MM1" targetNamespace="https://simplexflow.ch/samples/eurosim2026">
          <bpmn2:process id="Process_MM1" name="M/M/1 queue reference model" isExecutable="false">
            <bpmn2:startEvent id="StartEvent_Arrivals" name="Arrivals lambda">
              <bpmn2:outgoing>Flow_Arrivals_Queue</bpmn2:outgoing>
            </bpmn2:startEvent>
            <bpmn2:task id="Task_FIFOQueue" name="FIFO queue">
              <bpmn2:incoming>Flow_Arrivals_Queue</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Queue_Seize</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_SeizeServer" name="Seize server capacity 1">
              <bpmn2:incoming>Flow_Queue_Seize</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Seize_Service</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_Service" name="Service time Exp(mu)">
              <bpmn2:incoming>Flow_Seize_Service</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Service_Release</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_ReleaseServer" name="Release server">
              <bpmn2:incoming>Flow_Service_Release</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Release_End</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:endEvent id="EndEvent_Departure" name="Departure">
              <bpmn2:incoming>Flow_Release_End</bpmn2:incoming>
            </bpmn2:endEvent>
            <bpmn2:textAnnotation id="TextAnnotation_MM1_Assumptions">
              <bpmn2:text>M/M/1 mapping from the EUROSIM 2026 paper: exponential interarrival times with rate lambda, exponential service times with rate mu, FIFO queue, one capacity-constrained server.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:association id="Association_MM1_Assumptions" sourceRef="TextAnnotation_MM1_Assumptions" targetRef="Task_FIFOQueue" />
            <bpmn2:sequenceFlow id="Flow_Arrivals_Queue" sourceRef="StartEvent_Arrivals" targetRef="Task_FIFOQueue" />
            <bpmn2:sequenceFlow id="Flow_Queue_Seize" sourceRef="Task_FIFOQueue" targetRef="Task_SeizeServer" />
            <bpmn2:sequenceFlow id="Flow_Seize_Service" sourceRef="Task_SeizeServer" targetRef="Task_Service" />
            <bpmn2:sequenceFlow id="Flow_Service_Release" sourceRef="Task_Service" targetRef="Task_ReleaseServer" />
            <bpmn2:sequenceFlow id="Flow_Release_End" sourceRef="Task_ReleaseServer" targetRef="EndEvent_Departure" />
          </bpmn2:process>
          <bpmndi:BPMNDiagram id="BPMNDiagram_MM1">
            <bpmndi:BPMNPlane id="BPMNPlane_MM1" bpmnElement="Process_MM1">
              <bpmndi:BPMNShape id="StartEvent_Arrivals_di" bpmnElement="StartEvent_Arrivals">
                <dc:Bounds x="130" y="210" width="36" height="36" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_FIFOQueue_di" bpmnElement="Task_FIFOQueue">
                <dc:Bounds x="230" y="190" width="120" height="76" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_SeizeServer_di" bpmnElement="Task_SeizeServer">
                <dc:Bounds x="410" y="190" width="150" height="76" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Service_di" bpmnElement="Task_Service">
                <dc:Bounds x="620" y="190" width="145" height="76" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_ReleaseServer_di" bpmnElement="Task_ReleaseServer">
                <dc:Bounds x="825" y="190" width="130" height="76" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="EndEvent_Departure_di" bpmnElement="EndEvent_Departure">
                <dc:Bounds x="1015" y="210" width="36" height="36" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_MM1_Assumptions_di" bpmnElement="TextAnnotation_MM1_Assumptions">
                <dc:Bounds x="310" y="320" width="500" height="80" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNEdge id="Flow_Arrivals_Queue_di" bpmnElement="Flow_Arrivals_Queue">
                <di:waypoint x="166" y="228" />
                <di:waypoint x="230" y="228" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Queue_Seize_di" bpmnElement="Flow_Queue_Seize">
                <di:waypoint x="350" y="228" />
                <di:waypoint x="410" y="228" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Seize_Service_di" bpmnElement="Flow_Seize_Service">
                <di:waypoint x="560" y="228" />
                <di:waypoint x="620" y="228" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Service_Release_di" bpmnElement="Flow_Service_Release">
                <di:waypoint x="765" y="228" />
                <di:waypoint x="825" y="228" />
              </bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Release_End_di" bpmnElement="Flow_Release_End">
                <di:waypoint x="955" y="228" />
                <di:waypoint x="1015" y="228" />
              </bpmndi:BPMNEdge>
            </bpmndi:BPMNPlane>
          </bpmndi:BPMNDiagram>
        </bpmn2:definitions>
        """;
}
