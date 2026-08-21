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
            Mm1QueueXml),
        new(
            "poster-hospital-er",
            "Poster sample - Hospital emergency room",
            "Hospital emergency room sample from the WinterSim poster: admission, rooms, departments, discharge and room-level treatment logic.",
            HospitalEmergencyRoomArchitectureXml,
            HospitalEmergencyRoomRoom1LogicXml,
            "Task_Room1")
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

    private const string HospitalEmergencyRoomArchitectureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn2:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn2="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" xmlns:regularBPMN="http://regularBPMN" xmlns:bioc="http://bpmn.io/schema/bpmn/biocolor/1.0" xmlns:color="http://www.omg.org/spec/BPMN/non-normative/color/1.0" id="Definitions_HospitalEmergencyRoomArchitecture" targetNamespace="https://simplexflow.ch/samples/wintersim2025">
          <bpmn2:collaboration id="Collaboration_HospitalER">
            <bpmn2:participant id="Participant_ERArchitecture" name="Hospital emergency room architecture" processRef="Process_ERArchitecture" />
            <bpmn2:textAnnotation id="TextAnnotation_PatientAdmission">
              <bpmn2:text>Patient Admission</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:textAnnotation id="TextAnnotation_PatientDischarge">
              <bpmn2:text>Patient Discharge</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:textAnnotation id="TextAnnotation_StayOnWard">
              <bpmn2:text>Stay On Ward</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:association id="Association_PatientAdmission" sourceRef="TextAnnotation_PatientAdmission" targetRef="StartEvent_PatientArrives" />
            <bpmn2:association id="Association_PatientDischarge" sourceRef="TextAnnotation_PatientDischarge" targetRef="EndEvent_PatientDischarged" />
            <bpmn2:association id="Association_StayOnWard" sourceRef="TextAnnotation_StayOnWard" targetRef="Task_Department" />
            <bpmn2:association id="Association_Doctors_Room1" sourceRef="Resource_Doctors" targetRef="Task_Room1" />
            <bpmn2:association id="Association_Nurses_Room1" sourceRef="Resource_Nurses" targetRef="Task_Room1" />
            <bpmn2:association id="Association_Cleaning_Room1" sourceRef="Resource_CleaningStaff" targetRef="Task_Room1" />
          </bpmn2:collaboration>
          <bpmn2:process id="Process_ERArchitecture" name="Hospital emergency room architecture" isExecutable="false">
            <bpmn2:startEvent id="StartEvent_PatientArrives" name="Patient">
              <bpmn2:outgoing>Flow_Arrival_CheckIn</bpmn2:outgoing>
            </bpmn2:startEvent>
            <bpmn2:task id="Task_CheckIn" name="Check-In">
              <bpmn2:incoming>Flow_Arrival_CheckIn</bpmn2:incoming>
              <bpmn2:outgoing>Flow_CheckIn_WaitingRoom</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_WaitingRoom" name="Waiting Room">
              <bpmn2:incoming>Flow_CheckIn_WaitingRoom</bpmn2:incoming>
              <bpmn2:outgoing>Flow_WaitingRoom_RoomChoice</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_RoomChoice" name="Assign room">
              <bpmn2:incoming>Flow_WaitingRoom_RoomChoice</bpmn2:incoming>
              <bpmn2:outgoing>Flow_RoomChoice_Room1</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_RoomChoice_Room2</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_RoomChoice_Room3</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_Room1" name="Room1">
              <bpmn2:incoming>Flow_RoomChoice_Room1</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Room1_Join</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_Room2" name="Room2">
              <bpmn2:incoming>Flow_RoomChoice_Room2</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Room2_Join</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_Room3" name="Room3">
              <bpmn2:incoming>Flow_RoomChoice_Room3</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Room3_Join</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_AfterRoom" name="After room">
              <bpmn2:incoming>Flow_Room1_Join</bpmn2:incoming>
              <bpmn2:incoming>Flow_Room2_Join</bpmn2:incoming>
              <bpmn2:incoming>Flow_Room3_Join</bpmn2:incoming>
              <bpmn2:outgoing>Flow_AfterRoom_Department</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_AfterRoom_CheckOut</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_Department" name="Department">
              <bpmn2:incoming>Flow_AfterRoom_Department</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Department_End</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_CheckOut" name="Check-Out">
              <bpmn2:incoming>Flow_AfterRoom_CheckOut</bpmn2:incoming>
              <bpmn2:outgoing>Flow_CheckOut_End</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:endEvent id="EndEvent_ToWard" name="Stay On Ward">
              <bpmn2:incoming>Flow_Department_End</bpmn2:incoming>
            </bpmn2:endEvent>
            <bpmn2:endEvent id="EndEvent_PatientDischarged" name="Patient Discharge">
              <bpmn2:incoming>Flow_CheckOut_End</bpmn2:incoming>
            </bpmn2:endEvent>
            <regularBPMN:resource id="Resource_Doctors" name="Doctors" />
            <regularBPMN:resource id="Resource_Nurses" name="Nurses" />
            <regularBPMN:resource id="Resource_CleaningStaff" name="Cleaning Staff" />
            <bpmn2:sequenceFlow id="Flow_Arrival_CheckIn" sourceRef="StartEvent_PatientArrives" targetRef="Task_CheckIn" />
            <bpmn2:sequenceFlow id="Flow_CheckIn_WaitingRoom" sourceRef="Task_CheckIn" targetRef="Task_WaitingRoom" />
            <bpmn2:sequenceFlow id="Flow_WaitingRoom_RoomChoice" sourceRef="Task_WaitingRoom" targetRef="Gateway_RoomChoice" />
            <bpmn2:sequenceFlow id="Flow_RoomChoice_Room1" sourceRef="Gateway_RoomChoice" targetRef="Task_Room1" />
            <bpmn2:sequenceFlow id="Flow_RoomChoice_Room2" sourceRef="Gateway_RoomChoice" targetRef="Task_Room2" />
            <bpmn2:sequenceFlow id="Flow_RoomChoice_Room3" sourceRef="Gateway_RoomChoice" targetRef="Task_Room3" />
            <bpmn2:sequenceFlow id="Flow_Room1_Join" sourceRef="Task_Room1" targetRef="Gateway_AfterRoom" />
            <bpmn2:sequenceFlow id="Flow_Room2_Join" sourceRef="Task_Room2" targetRef="Gateway_AfterRoom" />
            <bpmn2:sequenceFlow id="Flow_Room3_Join" sourceRef="Task_Room3" targetRef="Gateway_AfterRoom" />
            <bpmn2:sequenceFlow id="Flow_AfterRoom_Department" sourceRef="Gateway_AfterRoom" targetRef="Task_Department" />
            <bpmn2:sequenceFlow id="Flow_AfterRoom_CheckOut" sourceRef="Gateway_AfterRoom" targetRef="Task_CheckOut" />
            <bpmn2:sequenceFlow id="Flow_Department_End" sourceRef="Task_Department" targetRef="EndEvent_ToWard" />
            <bpmn2:sequenceFlow id="Flow_CheckOut_End" sourceRef="Task_CheckOut" targetRef="EndEvent_PatientDischarged" />
          </bpmn2:process>
          <bpmndi:BPMNDiagram id="BPMNDiagram_HospitalERArchitecture">
            <bpmndi:BPMNPlane id="BPMNPlane_HospitalERArchitecture" bpmnElement="Collaboration_HospitalER">
              <bpmndi:BPMNShape id="Participant_ERArchitecture_di" bpmnElement="Participant_ERArchitecture" isHorizontal="true">
                <dc:Bounds x="70" y="70" width="1110" height="390" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="StartEvent_PatientArrives_di" bpmnElement="StartEvent_PatientArrives"><dc:Bounds x="105" y="238" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_CheckIn_di" bpmnElement="Task_CheckIn"><dc:Bounds x="180" y="218" width="110" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_WaitingRoom_di" bpmnElement="Task_WaitingRoom"><dc:Bounds x="350" y="218" width="125" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_RoomChoice_di" bpmnElement="Gateway_RoomChoice" isMarkerVisible="true"><dc:Bounds x="540" y="231" width="50" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Room1_di" bpmnElement="Task_Room1"><dc:Bounds x="680" y="153" width="115" height="70" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Room2_di" bpmnElement="Task_Room2"><dc:Bounds x="680" y="243" width="115" height="70" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Room3_di" bpmnElement="Task_Room3"><dc:Bounds x="680" y="333" width="115" height="70" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_AfterRoom_di" bpmnElement="Gateway_AfterRoom" isMarkerVisible="true"><dc:Bounds x="880" y="256" width="50" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Department_di" bpmnElement="Task_Department"><dc:Bounds x="980" y="243" width="115" height="70" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_CheckOut_di" bpmnElement="Task_CheckOut"><dc:Bounds x="180" y="358" width="110" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="EndEvent_ToWard_di" bpmnElement="EndEvent_ToWard"><dc:Bounds x="1145" y="260" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="EndEvent_PatientDischarged_di" bpmnElement="EndEvent_PatientDischarged"><dc:Bounds x="105" y="378" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Resource_Doctors_di" bpmnElement="Resource_Doctors" bioc:stroke="#43A047" bioc:fill="#C8E6C9" color:background-color="#C8E6C9" color:border-color="#43A047"><dc:Bounds x="785" y="18" width="90" height="46" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Resource_Nurses_di" bpmnElement="Resource_Nurses" bioc:stroke="#FB8C00" bioc:fill="#FFE0B2" color:background-color="#FFE0B2" color:border-color="#FB8C00"><dc:Bounds x="920" y="32" width="90" height="46" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Resource_CleaningStaff_di" bpmnElement="Resource_CleaningStaff" bioc:stroke="#1E88E5" bioc:fill="#BBDEFB" color:background-color="#BBDEFB" color:border-color="#1E88E5"><dc:Bounds x="1045" y="62" width="120" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_PatientAdmission_di" bpmnElement="TextAnnotation_PatientAdmission"><dc:Bounds x="118" y="150" width="160" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_PatientDischarge_di" bpmnElement="TextAnnotation_PatientDischarge"><dc:Bounds x="120" y="445" width="170" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_StayOnWard_di" bpmnElement="TextAnnotation_StayOnWard"><dc:Bounds x="1060" y="156" width="150" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNEdge id="Flow_Arrival_CheckIn_di" bpmnElement="Flow_Arrival_CheckIn"><di:waypoint x="141" y="256" /><di:waypoint x="180" y="256" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_CheckIn_WaitingRoom_di" bpmnElement="Flow_CheckIn_WaitingRoom"><di:waypoint x="290" y="256" /><di:waypoint x="350" y="256" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_WaitingRoom_RoomChoice_di" bpmnElement="Flow_WaitingRoom_RoomChoice"><di:waypoint x="475" y="256" /><di:waypoint x="540" y="256" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_RoomChoice_Room1_di" bpmnElement="Flow_RoomChoice_Room1"><di:waypoint x="565" y="231" /><di:waypoint x="565" y="188" /><di:waypoint x="680" y="188" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_RoomChoice_Room2_di" bpmnElement="Flow_RoomChoice_Room2"><di:waypoint x="590" y="256" /><di:waypoint x="680" y="278" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_RoomChoice_Room3_di" bpmnElement="Flow_RoomChoice_Room3"><di:waypoint x="565" y="281" /><di:waypoint x="565" y="368" /><di:waypoint x="680" y="368" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Room1_Join_di" bpmnElement="Flow_Room1_Join"><di:waypoint x="795" y="188" /><di:waypoint x="905" y="188" /><di:waypoint x="905" y="256" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Room2_Join_di" bpmnElement="Flow_Room2_Join"><di:waypoint x="795" y="278" /><di:waypoint x="880" y="281" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Room3_Join_di" bpmnElement="Flow_Room3_Join"><di:waypoint x="795" y="368" /><di:waypoint x="905" y="368" /><di:waypoint x="905" y="306" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_AfterRoom_Department_di" bpmnElement="Flow_AfterRoom_Department"><di:waypoint x="930" y="281" /><di:waypoint x="980" y="278" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_AfterRoom_CheckOut_di" bpmnElement="Flow_AfterRoom_CheckOut"><di:waypoint x="905" y="306" /><di:waypoint x="905" y="413" /><di:waypoint x="290" y="413" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Department_End_di" bpmnElement="Flow_Department_End"><di:waypoint x="1095" y="278" /><di:waypoint x="1145" y="278" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_CheckOut_End_di" bpmnElement="Flow_CheckOut_End"><di:waypoint x="180" y="396" /><di:waypoint x="141" y="396" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_PatientAdmission_di" bpmnElement="Association_PatientAdmission"><di:waypoint x="170" y="200" /><di:waypoint x="123" y="238" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_PatientDischarge_di" bpmnElement="Association_PatientDischarge"><di:waypoint x="168" y="445" /><di:waypoint x="123" y="414" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_StayOnWard_di" bpmnElement="Association_StayOnWard"><di:waypoint x="1065" y="206" /><di:waypoint x="1040" y="243" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_Doctors_Room1_di" bpmnElement="Association_Doctors_Room1"><di:waypoint x="830" y="64" /><di:waypoint x="745" y="153" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_Nurses_Room1_di" bpmnElement="Association_Nurses_Room1"><di:waypoint x="950" y="78" /><di:waypoint x="760" y="153" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_Cleaning_Room1_di" bpmnElement="Association_Cleaning_Room1"><di:waypoint x="1055" y="112" /><di:waypoint x="790" y="168" /></bpmndi:BPMNEdge>
            </bpmndi:BPMNPlane>
          </bpmndi:BPMNDiagram>
        </bpmn2:definitions>
        """;

    private const string HospitalEmergencyRoomRoom1LogicXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn2:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn2="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" xmlns:simBPMN="http://simBPMN" xmlns:bioc="http://bpmn.io/schema/bpmn/biocolor/1.0" xmlns:color="http://www.omg.org/spec/BPMN/non-normative/color/1.0" id="Definitions_HospitalRoom1Logic" targetNamespace="https://simplexflow.ch/samples/wintersim2025/room1-logic">
          <bpmn2:process id="Process_Room1Logic" name="Room1 treatment logic" isExecutable="false">
            <bpmn2:startEvent id="StartEvent_RoomPatient" name="Patient">
              <bpmn2:outgoing>Flow_Patient_Queue</bpmn2:outgoing>
            </bpmn2:startEvent>
            <simBPMN:queue id="Queue_Room1" name="Room1 queue">
              <bpmn2:incoming>Flow_Patient_Queue</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Queue_InitialTreatment</bpmn2:outgoing>
            </simBPMN:queue>
            <bpmn2:task id="Task_InitialTreatment" name="Initial Treatment">
              <bpmn2:incoming>Flow_Queue_InitialTreatment</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Initial_Exam</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_ExamDiagnosis" name="Examination and Diagnosis">
              <bpmn2:incoming>Flow_Initial_Exam</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Exam_Decision</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_Disposition" name="Disposition">
              <bpmn2:incoming>Flow_Exam_Decision</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Discharge_Path</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_ToWard_WaitBed</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_WaitBed" name="Wait for Available Bed in Ward">
              <bpmn2:incoming>Flow_ToWard_WaitBed</bpmn2:incoming>
              <bpmn2:outgoing>Flow_WaitBed_CleaningJoin</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_CleaningJoin" name="Room available">
              <bpmn2:incoming>Flow_Discharge_Path</bpmn2:incoming>
              <bpmn2:incoming>Flow_WaitBed_CleaningJoin</bpmn2:incoming>
              <bpmn2:outgoing>Flow_CleaningJoin_Disinfection</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_Disinfection" name="Disinfection">
              <bpmn2:incoming>Flow_CleaningJoin_Disinfection</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Disinfection_Output</bpmn2:outgoing>
            </bpmn2:task>
            <simBPMN:output id="Output_RoomReleased" name="Room released">
              <bpmn2:incoming>Flow_Disinfection_Output</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Output_End</bpmn2:outgoing>
            </simBPMN:output>
            <bpmn2:endEvent id="EndEvent_RoomAvailable" name="Room available">
              <bpmn2:incoming>Flow_Output_End</bpmn2:incoming>
            </bpmn2:endEvent>
            <simBPMN:resource id="Resource_Doctors" name="Doctors" />
            <simBPMN:resource id="Resource_Nurses" name="Nurses" />
            <simBPMN:resource id="Resource_CleaningStaff" name="Cleaning Staff" />
            <simBPMN:resource id="Resource_DefaultPatient" name="default" />
            <bpmn2:textAnnotation id="TextAnnotation_DoctorsAnalysis">
              <bpmn2:text>Static analysis for Doctors: arrival rate lambda = 5 patients / 60 min, service time S = 25 min / patient, capacity C = 3 doctors, utilization U = lambda x S / C = 0.694.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:association id="Association_Default_Queue" sourceRef="Resource_DefaultPatient" targetRef="Queue_Room1" />
            <bpmn2:association id="Association_Doctors_Exam" sourceRef="Resource_Doctors" targetRef="Task_ExamDiagnosis" />
            <bpmn2:association id="Association_Nurses_Initial" sourceRef="Resource_Nurses" targetRef="Task_InitialTreatment" />
            <bpmn2:association id="Association_Nurses_Exam" sourceRef="Resource_Nurses" targetRef="Task_ExamDiagnosis" />
            <bpmn2:association id="Association_Cleaning_Disinfection" sourceRef="Resource_CleaningStaff" targetRef="Task_Disinfection" />
            <bpmn2:association id="Association_DoctorsAnalysis" sourceRef="TextAnnotation_DoctorsAnalysis" targetRef="Resource_Doctors" />
            <bpmn2:sequenceFlow id="Flow_Patient_Queue" sourceRef="StartEvent_RoomPatient" targetRef="Queue_Room1" />
            <bpmn2:sequenceFlow id="Flow_Queue_InitialTreatment" sourceRef="Queue_Room1" targetRef="Task_InitialTreatment" />
            <bpmn2:sequenceFlow id="Flow_Initial_Exam" sourceRef="Task_InitialTreatment" targetRef="Task_ExamDiagnosis" />
            <bpmn2:sequenceFlow id="Flow_Exam_Decision" sourceRef="Task_ExamDiagnosis" targetRef="Gateway_Disposition" />
            <bpmn2:sequenceFlow id="Flow_Discharge_Path" name="Discharge" sourceRef="Gateway_Disposition" targetRef="Gateway_CleaningJoin" />
            <bpmn2:sequenceFlow id="Flow_ToWard_WaitBed" name="To Ward" sourceRef="Gateway_Disposition" targetRef="Task_WaitBed" />
            <bpmn2:sequenceFlow id="Flow_WaitBed_CleaningJoin" sourceRef="Task_WaitBed" targetRef="Gateway_CleaningJoin" />
            <bpmn2:sequenceFlow id="Flow_CleaningJoin_Disinfection" sourceRef="Gateway_CleaningJoin" targetRef="Task_Disinfection" />
            <bpmn2:sequenceFlow id="Flow_Disinfection_Output" sourceRef="Task_Disinfection" targetRef="Output_RoomReleased" />
            <bpmn2:sequenceFlow id="Flow_Output_End" sourceRef="Output_RoomReleased" targetRef="EndEvent_RoomAvailable" />
          </bpmn2:process>
          <bpmndi:BPMNDiagram id="BPMNDiagram_Room1Logic">
            <bpmndi:BPMNPlane id="BPMNPlane_Room1Logic" bpmnElement="Process_Room1Logic">
              <bpmndi:BPMNShape id="StartEvent_RoomPatient_di" bpmnElement="StartEvent_RoomPatient"><dc:Bounds x="90" y="368" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Queue_Room1_di" bpmnElement="Queue_Room1"><dc:Bounds x="170" y="345" width="112" height="82" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_InitialTreatment_di" bpmnElement="Task_InitialTreatment"><dc:Bounds x="370" y="338" width="130" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_ExamDiagnosis_di" bpmnElement="Task_ExamDiagnosis"><dc:Bounds x="370" y="215" width="130" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_Disposition_di" bpmnElement="Gateway_Disposition" isMarkerVisible="true"><dc:Bounds x="565" y="245" width="50" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_WaitBed_di" bpmnElement="Task_WaitBed"><dc:Bounds x="655" y="370" width="140" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_CleaningJoin_di" bpmnElement="Gateway_CleaningJoin" isMarkerVisible="true"><dc:Bounds x="850" y="285" width="50" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Disinfection_di" bpmnElement="Task_Disinfection"><dc:Bounds x="960" y="270" width="130" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Output_RoomReleased_di" bpmnElement="Output_RoomReleased"><dc:Bounds x="1140" y="270" width="112" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="EndEvent_RoomAvailable_di" bpmnElement="EndEvent_RoomAvailable"><dc:Bounds x="1310" y="290" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Resource_Doctors_di" bpmnElement="Resource_Doctors" bioc:stroke="#43A047" bioc:fill="#C8E6C9" color:background-color="#C8E6C9" color:border-color="#43A047"><dc:Bounds x="255" y="160" width="90" height="46" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Resource_Nurses_di" bpmnElement="Resource_Nurses" bioc:stroke="#FB8C00" bioc:fill="#FFE0B2" color:background-color="#FFE0B2" color:border-color="#FB8C00"><dc:Bounds x="250" y="250" width="90" height="46" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Resource_CleaningStaff_di" bpmnElement="Resource_CleaningStaff" bioc:stroke="#1E88E5" bioc:fill="#BBDEFB" color:background-color="#BBDEFB" color:border-color="#1E88E5"><dc:Bounds x="930" y="110" width="120" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Resource_DefaultPatient_di" bpmnElement="Resource_DefaultPatient" bioc:stroke="#111111" bioc:fill="#E9ECEF" color:background-color="#E9ECEF" color:border-color="#111111"><dc:Bounds x="600" y="515" width="80" height="42" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_DoctorsAnalysis_di" bpmnElement="TextAnnotation_DoctorsAnalysis"><dc:Bounds x="58" y="112" width="210" height="96" /></bpmndi:BPMNShape>
              <bpmndi:BPMNEdge id="Flow_Patient_Queue_di" bpmnElement="Flow_Patient_Queue"><di:waypoint x="126" y="386" /><di:waypoint x="170" y="386" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Queue_InitialTreatment_di" bpmnElement="Flow_Queue_InitialTreatment"><di:waypoint x="282" y="386" /><di:waypoint x="370" y="376" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Initial_Exam_di" bpmnElement="Flow_Initial_Exam"><di:waypoint x="435" y="338" /><di:waypoint x="435" y="291" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Exam_Decision_di" bpmnElement="Flow_Exam_Decision"><di:waypoint x="500" y="253" /><di:waypoint x="565" y="270" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Discharge_Path_di" bpmnElement="Flow_Discharge_Path"><di:waypoint x="615" y="270" /><di:waypoint x="850" y="310" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_ToWard_WaitBed_di" bpmnElement="Flow_ToWard_WaitBed"><di:waypoint x="590" y="295" /><di:waypoint x="590" y="408" /><di:waypoint x="655" y="408" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_WaitBed_CleaningJoin_di" bpmnElement="Flow_WaitBed_CleaningJoin"><di:waypoint x="795" y="408" /><di:waypoint x="875" y="335" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_CleaningJoin_Disinfection_di" bpmnElement="Flow_CleaningJoin_Disinfection"><di:waypoint x="900" y="310" /><di:waypoint x="960" y="308" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Disinfection_Output_di" bpmnElement="Flow_Disinfection_Output"><di:waypoint x="1090" y="308" /><di:waypoint x="1140" y="308" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Output_End_di" bpmnElement="Flow_Output_End"><di:waypoint x="1252" y="308" /><di:waypoint x="1310" y="308" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_Default_Queue_di" bpmnElement="Association_Default_Queue"><di:waypoint x="600" y="536" /><di:waypoint x="226" y="427" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_Doctors_Exam_di" bpmnElement="Association_Doctors_Exam"><di:waypoint x="345" y="185" /><di:waypoint x="390" y="215" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_Nurses_Initial_di" bpmnElement="Association_Nurses_Initial"><di:waypoint x="340" y="276" /><di:waypoint x="390" y="338" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_Nurses_Exam_di" bpmnElement="Association_Nurses_Exam"><di:waypoint x="340" y="266" /><di:waypoint x="370" y="253" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_Cleaning_Disinfection_di" bpmnElement="Association_Cleaning_Disinfection"><di:waypoint x="990" y="160" /><di:waypoint x="1015" y="270" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Association_DoctorsAnalysis_di" bpmnElement="Association_DoctorsAnalysis"><di:waypoint x="268" y="160" /><di:waypoint x="300" y="183" /></bpmndi:BPMNEdge>
            </bpmndi:BPMNPlane>
          </bpmndi:BPMNDiagram>
        </bpmn2:definitions>
        """;

    private const string HospitalEmergencyRoomXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <bpmn2:definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:bpmn2="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Definitions_HospitalEmergencyRoom" targetNamespace="https://simplexflow.ch/samples/wintersim2025">
          <bpmn2:collaboration id="Collaboration_HospitalER">
            <bpmn2:participant id="Participant_ERArchitecture" name="Hospital emergency room architecture" processRef="Process_ERArchitecture" />
            <bpmn2:participant id="Participant_RoomLogic" name="Room logic - examination and discharge" processRef="Process_RoomLogic" />
            <bpmn2:textAnnotation id="TextAnnotation_PatientAdmission">
              <bpmn2:text>Patient admission: external arrival stream of patients entering check-in.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:textAnnotation id="TextAnnotation_PatientDischarge">
              <bpmn2:text>Patient discharge: checked-out patients leave the emergency room system.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:textAnnotation id="TextAnnotation_StayOnWard">
              <bpmn2:text>Stay on ward: patients that need inpatient care move from department to ward when a bed is available.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:textAnnotation id="TextAnnotation_Doctors">
              <bpmn2:text>Doctors resource. Poster note: service time at least 25 min per patient, capacity C = 3 doctors, utilization U = lambda x S / C = 0.694.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:textAnnotation id="TextAnnotation_Nurses">
              <bpmn2:text>Nurses resource used during initial treatment and examination.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:textAnnotation id="TextAnnotation_CleaningStaff">
              <bpmn2:text>Cleaning staff resource used for room disinfection after treatment.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:association id="Association_PatientAdmission" sourceRef="TextAnnotation_PatientAdmission" targetRef="StartEvent_PatientArrives" />
            <bpmn2:association id="Association_PatientDischarge" sourceRef="TextAnnotation_PatientDischarge" targetRef="EndEvent_PatientDischarged" />
            <bpmn2:association id="Association_StayOnWard" sourceRef="TextAnnotation_StayOnWard" targetRef="Task_Department" />
            <bpmn2:association id="Association_Doctors_Room" sourceRef="TextAnnotation_Doctors" targetRef="Task_Room1" />
            <bpmn2:association id="Association_Nurses_Room" sourceRef="TextAnnotation_Nurses" targetRef="Task_Room1" />
            <bpmn2:association id="Association_Cleaning_Room" sourceRef="TextAnnotation_CleaningStaff" targetRef="Task_Room1" />
            <bpmn2:association id="Association_Doctors_Logic" sourceRef="TextAnnotation_Doctors" targetRef="Task_ExamDiagnosis" />
            <bpmn2:association id="Association_Nurses_Logic" sourceRef="TextAnnotation_Nurses" targetRef="Task_InitialTreatment" />
            <bpmn2:association id="Association_Cleaning_Logic" sourceRef="TextAnnotation_CleaningStaff" targetRef="Task_Disinfection" />
          </bpmn2:collaboration>
          <bpmn2:process id="Process_ERArchitecture" name="Hospital emergency room architecture" isExecutable="false">
            <bpmn2:startEvent id="StartEvent_PatientArrives" name="Patient arrives">
              <bpmn2:outgoing>Flow_Arrival_CheckIn</bpmn2:outgoing>
            </bpmn2:startEvent>
            <bpmn2:task id="Task_CheckIn" name="Check-In">
              <bpmn2:incoming>Flow_Arrival_CheckIn</bpmn2:incoming>
              <bpmn2:outgoing>Flow_CheckIn_WaitingRoom</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_WaitingRoom" name="Waiting Room">
              <bpmn2:incoming>Flow_CheckIn_WaitingRoom</bpmn2:incoming>
              <bpmn2:outgoing>Flow_WaitingRoom_RoomChoice</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_RoomChoice" name="Assign room">
              <bpmn2:incoming>Flow_WaitingRoom_RoomChoice</bpmn2:incoming>
              <bpmn2:outgoing>Flow_RoomChoice_Room1</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_RoomChoice_Room2</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_RoomChoice_Room3</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_Room1" name="Room1">
              <bpmn2:incoming>Flow_RoomChoice_Room1</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Room1_Join</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_Room2" name="Room2">
              <bpmn2:incoming>Flow_RoomChoice_Room2</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Room2_Join</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_Room3" name="Room3">
              <bpmn2:incoming>Flow_RoomChoice_Room3</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Room3_Join</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_AfterRoom" name="After room">
              <bpmn2:incoming>Flow_Room1_Join</bpmn2:incoming>
              <bpmn2:incoming>Flow_Room2_Join</bpmn2:incoming>
              <bpmn2:incoming>Flow_Room3_Join</bpmn2:incoming>
              <bpmn2:outgoing>Flow_AfterRoom_Department</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_AfterRoom_CheckOut</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_Department" name="Department">
              <bpmn2:incoming>Flow_AfterRoom_Department</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Department_End</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_CheckOut" name="Check-Out">
              <bpmn2:incoming>Flow_AfterRoom_CheckOut</bpmn2:incoming>
              <bpmn2:outgoing>Flow_CheckOut_End</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:endEvent id="EndEvent_ToWard" name="To ward">
              <bpmn2:incoming>Flow_Department_End</bpmn2:incoming>
            </bpmn2:endEvent>
            <bpmn2:endEvent id="EndEvent_PatientDischarged" name="Patient discharged">
              <bpmn2:incoming>Flow_CheckOut_End</bpmn2:incoming>
            </bpmn2:endEvent>
            <bpmn2:sequenceFlow id="Flow_Arrival_CheckIn" sourceRef="StartEvent_PatientArrives" targetRef="Task_CheckIn" />
            <bpmn2:sequenceFlow id="Flow_CheckIn_WaitingRoom" sourceRef="Task_CheckIn" targetRef="Task_WaitingRoom" />
            <bpmn2:sequenceFlow id="Flow_WaitingRoom_RoomChoice" sourceRef="Task_WaitingRoom" targetRef="Gateway_RoomChoice" />
            <bpmn2:sequenceFlow id="Flow_RoomChoice_Room1" sourceRef="Gateway_RoomChoice" targetRef="Task_Room1" />
            <bpmn2:sequenceFlow id="Flow_RoomChoice_Room2" sourceRef="Gateway_RoomChoice" targetRef="Task_Room2" />
            <bpmn2:sequenceFlow id="Flow_RoomChoice_Room3" sourceRef="Gateway_RoomChoice" targetRef="Task_Room3" />
            <bpmn2:sequenceFlow id="Flow_Room1_Join" sourceRef="Task_Room1" targetRef="Gateway_AfterRoom" />
            <bpmn2:sequenceFlow id="Flow_Room2_Join" sourceRef="Task_Room2" targetRef="Gateway_AfterRoom" />
            <bpmn2:sequenceFlow id="Flow_Room3_Join" sourceRef="Task_Room3" targetRef="Gateway_AfterRoom" />
            <bpmn2:sequenceFlow id="Flow_AfterRoom_Department" sourceRef="Gateway_AfterRoom" targetRef="Task_Department" />
            <bpmn2:sequenceFlow id="Flow_AfterRoom_CheckOut" sourceRef="Gateway_AfterRoom" targetRef="Task_CheckOut" />
            <bpmn2:sequenceFlow id="Flow_Department_End" sourceRef="Task_Department" targetRef="EndEvent_ToWard" />
            <bpmn2:sequenceFlow id="Flow_CheckOut_End" sourceRef="Task_CheckOut" targetRef="EndEvent_PatientDischarged" />
          </bpmn2:process>
          <bpmn2:process id="Process_RoomLogic" name="Room-level emergency treatment logic" isExecutable="false">
            <bpmn2:startEvent id="StartEvent_RoomToken" name="Patient ready">
              <bpmn2:outgoing>Flow_RoomToken_Queue</bpmn2:outgoing>
            </bpmn2:startEvent>
            <bpmn2:task id="Task_RoomQueue" name="Room queue">
              <bpmn2:incoming>Flow_RoomToken_Queue</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Queue_InitialTreatment</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_InitialTreatment" name="Initial Treatment">
              <bpmn2:incoming>Flow_Queue_InitialTreatment</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Initial_Exam</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_ExamDiagnosis" name="Examination and Diagnosis">
              <bpmn2:incoming>Flow_Initial_Exam</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Exam_Decision</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_Disposition" name="Disposition">
              <bpmn2:incoming>Flow_Exam_Decision</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Discharge_Path</bpmn2:outgoing>
              <bpmn2:outgoing>Flow_ToWard_WaitBed</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_WaitBed" name="Wait for Available Bed in Ward">
              <bpmn2:incoming>Flow_ToWard_WaitBed</bpmn2:incoming>
              <bpmn2:outgoing>Flow_WaitBed_CleaningJoin</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:exclusiveGateway id="Gateway_CleaningJoin" name="Room available">
              <bpmn2:incoming>Flow_Discharge_Path</bpmn2:incoming>
              <bpmn2:incoming>Flow_WaitBed_CleaningJoin</bpmn2:incoming>
              <bpmn2:outgoing>Flow_CleaningJoin_Disinfection</bpmn2:outgoing>
            </bpmn2:exclusiveGateway>
            <bpmn2:task id="Task_Disinfection" name="Disinfection">
              <bpmn2:incoming>Flow_CleaningJoin_Disinfection</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Disinfection_Buffer</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:task id="Task_RoomBuffer" name="Room released">
              <bpmn2:incoming>Flow_Disinfection_Buffer</bpmn2:incoming>
              <bpmn2:outgoing>Flow_Buffer_End</bpmn2:outgoing>
            </bpmn2:task>
            <bpmn2:endEvent id="EndEvent_RoomAvailable" name="Room available">
              <bpmn2:incoming>Flow_Buffer_End</bpmn2:incoming>
            </bpmn2:endEvent>
            <bpmn2:textAnnotation id="TextAnnotation_RoomLogic_Default">
              <bpmn2:text>Default patient token follows the room-level logic until the room is cleaned and released.</bpmn2:text>
            </bpmn2:textAnnotation>
            <bpmn2:association id="Association_RoomLogic_Default" sourceRef="TextAnnotation_RoomLogic_Default" targetRef="Task_InitialTreatment" />
            <bpmn2:sequenceFlow id="Flow_RoomToken_Queue" sourceRef="StartEvent_RoomToken" targetRef="Task_RoomQueue" />
            <bpmn2:sequenceFlow id="Flow_Queue_InitialTreatment" sourceRef="Task_RoomQueue" targetRef="Task_InitialTreatment" />
            <bpmn2:sequenceFlow id="Flow_Initial_Exam" sourceRef="Task_InitialTreatment" targetRef="Task_ExamDiagnosis" />
            <bpmn2:sequenceFlow id="Flow_Exam_Decision" sourceRef="Task_ExamDiagnosis" targetRef="Gateway_Disposition" />
            <bpmn2:sequenceFlow id="Flow_Discharge_Path" name="Discharge" sourceRef="Gateway_Disposition" targetRef="Gateway_CleaningJoin" />
            <bpmn2:sequenceFlow id="Flow_ToWard_WaitBed" name="To Ward" sourceRef="Gateway_Disposition" targetRef="Task_WaitBed" />
            <bpmn2:sequenceFlow id="Flow_WaitBed_CleaningJoin" sourceRef="Task_WaitBed" targetRef="Gateway_CleaningJoin" />
            <bpmn2:sequenceFlow id="Flow_CleaningJoin_Disinfection" sourceRef="Gateway_CleaningJoin" targetRef="Task_Disinfection" />
            <bpmn2:sequenceFlow id="Flow_Disinfection_Buffer" sourceRef="Task_Disinfection" targetRef="Task_RoomBuffer" />
            <bpmn2:sequenceFlow id="Flow_Buffer_End" sourceRef="Task_RoomBuffer" targetRef="EndEvent_RoomAvailable" />
          </bpmn2:process>
          <bpmndi:BPMNDiagram id="BPMNDiagram_HospitalER">
            <bpmndi:BPMNPlane id="BPMNPlane_HospitalER" bpmnElement="Collaboration_HospitalER">
              <bpmndi:BPMNShape id="Participant_ERArchitecture_di" bpmnElement="Participant_ERArchitecture" isHorizontal="true">
                <dc:Bounds x="70" y="70" width="1030" height="360" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Participant_RoomLogic_di" bpmnElement="Participant_RoomLogic" isHorizontal="true">
                <dc:Bounds x="70" y="500" width="1030" height="330" />
              </bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="StartEvent_PatientArrives_di" bpmnElement="StartEvent_PatientArrives"><dc:Bounds x="105" y="230" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_CheckIn_di" bpmnElement="Task_CheckIn"><dc:Bounds x="180" y="210" width="110" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_WaitingRoom_di" bpmnElement="Task_WaitingRoom"><dc:Bounds x="350" y="210" width="125" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_RoomChoice_di" bpmnElement="Gateway_RoomChoice" isMarkerVisible="true"><dc:Bounds x="540" y="223" width="50" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Room1_di" bpmnElement="Task_Room1"><dc:Bounds x="680" y="145" width="115" height="70" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Room2_di" bpmnElement="Task_Room2"><dc:Bounds x="680" y="235" width="115" height="70" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Room3_di" bpmnElement="Task_Room3"><dc:Bounds x="680" y="325" width="115" height="70" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_AfterRoom_di" bpmnElement="Gateway_AfterRoom" isMarkerVisible="true"><dc:Bounds x="880" y="248" width="50" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Department_di" bpmnElement="Task_Department"><dc:Bounds x="980" y="235" width="115" height="70" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_CheckOut_di" bpmnElement="Task_CheckOut"><dc:Bounds x="180" y="350" width="110" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="EndEvent_ToWard_di" bpmnElement="EndEvent_ToWard"><dc:Bounds x="1145" y="252" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="EndEvent_PatientDischarged_di" bpmnElement="EndEvent_PatientDischarged"><dc:Bounds x="105" y="370" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_PatientAdmission_di" bpmnElement="TextAnnotation_PatientAdmission"><dc:Bounds x="120" y="145" width="180" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_PatientDischarge_di" bpmnElement="TextAnnotation_PatientDischarge"><dc:Bounds x="120" y="440" width="190" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_StayOnWard_di" bpmnElement="TextAnnotation_StayOnWard"><dc:Bounds x="1040" y="150" width="180" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_Doctors_di" bpmnElement="TextAnnotation_Doctors"><dc:Bounds x="760" y="25" width="150" height="60" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_Nurses_di" bpmnElement="TextAnnotation_Nurses"><dc:Bounds x="890" y="35" width="150" height="60" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_CleaningStaff_di" bpmnElement="TextAnnotation_CleaningStaff"><dc:Bounds x="1020" y="60" width="160" height="60" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="StartEvent_RoomToken_di" bpmnElement="StartEvent_RoomToken"><dc:Bounds x="105" y="645" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_RoomQueue_di" bpmnElement="Task_RoomQueue"><dc:Bounds x="180" y="625" width="110" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_InitialTreatment_di" bpmnElement="Task_InitialTreatment"><dc:Bounds x="380" y="665" width="130" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_ExamDiagnosis_di" bpmnElement="Task_ExamDiagnosis"><dc:Bounds x="380" y="555" width="130" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_Disposition_di" bpmnElement="Gateway_Disposition" isMarkerVisible="true"><dc:Bounds x="570" y="570" width="50" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_WaitBed_di" bpmnElement="Task_WaitBed"><dc:Bounds x="650" y="690" width="140" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Gateway_CleaningJoin_di" bpmnElement="Gateway_CleaningJoin" isMarkerVisible="true"><dc:Bounds x="845" y="590" width="50" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_Disinfection_di" bpmnElement="Task_Disinfection"><dc:Bounds x="950" y="575" width="130" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="Task_RoomBuffer_di" bpmnElement="Task_RoomBuffer"><dc:Bounds x="1130" y="575" width="130" height="76" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="EndEvent_RoomAvailable_di" bpmnElement="EndEvent_RoomAvailable"><dc:Bounds x="1315" y="595" width="36" height="36" /></bpmndi:BPMNShape>
              <bpmndi:BPMNShape id="TextAnnotation_RoomLogic_Default_di" bpmnElement="TextAnnotation_RoomLogic_Default"><dc:Bounds x="590" y="785" width="260" height="50" /></bpmndi:BPMNShape>
              <bpmndi:BPMNEdge id="Flow_Arrival_CheckIn_di" bpmnElement="Flow_Arrival_CheckIn"><di:waypoint x="141" y="248" /><di:waypoint x="180" y="248" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_CheckIn_WaitingRoom_di" bpmnElement="Flow_CheckIn_WaitingRoom"><di:waypoint x="290" y="248" /><di:waypoint x="350" y="248" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_WaitingRoom_RoomChoice_di" bpmnElement="Flow_WaitingRoom_RoomChoice"><di:waypoint x="475" y="248" /><di:waypoint x="540" y="248" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_RoomChoice_Room1_di" bpmnElement="Flow_RoomChoice_Room1"><di:waypoint x="565" y="223" /><di:waypoint x="565" y="180" /><di:waypoint x="680" y="180" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_RoomChoice_Room2_di" bpmnElement="Flow_RoomChoice_Room2"><di:waypoint x="590" y="248" /><di:waypoint x="680" y="270" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_RoomChoice_Room3_di" bpmnElement="Flow_RoomChoice_Room3"><di:waypoint x="565" y="273" /><di:waypoint x="565" y="360" /><di:waypoint x="680" y="360" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Room1_Join_di" bpmnElement="Flow_Room1_Join"><di:waypoint x="795" y="180" /><di:waypoint x="905" y="180" /><di:waypoint x="905" y="248" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Room2_Join_di" bpmnElement="Flow_Room2_Join"><di:waypoint x="795" y="270" /><di:waypoint x="880" y="273" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Room3_Join_di" bpmnElement="Flow_Room3_Join"><di:waypoint x="795" y="360" /><di:waypoint x="905" y="360" /><di:waypoint x="905" y="298" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_AfterRoom_Department_di" bpmnElement="Flow_AfterRoom_Department"><di:waypoint x="930" y="273" /><di:waypoint x="980" y="270" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_AfterRoom_CheckOut_di" bpmnElement="Flow_AfterRoom_CheckOut"><di:waypoint x="905" y="298" /><di:waypoint x="905" y="405" /><di:waypoint x="290" y="405" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Department_End_di" bpmnElement="Flow_Department_End"><di:waypoint x="1095" y="270" /><di:waypoint x="1145" y="270" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_CheckOut_End_di" bpmnElement="Flow_CheckOut_End"><di:waypoint x="180" y="388" /><di:waypoint x="141" y="388" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_RoomToken_Queue_di" bpmnElement="Flow_RoomToken_Queue"><di:waypoint x="141" y="663" /><di:waypoint x="180" y="663" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Queue_InitialTreatment_di" bpmnElement="Flow_Queue_InitialTreatment"><di:waypoint x="290" y="663" /><di:waypoint x="380" y="703" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Initial_Exam_di" bpmnElement="Flow_Initial_Exam"><di:waypoint x="445" y="665" /><di:waypoint x="445" y="631" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Exam_Decision_di" bpmnElement="Flow_Exam_Decision"><di:waypoint x="510" y="593" /><di:waypoint x="570" y="595" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Discharge_Path_di" bpmnElement="Flow_Discharge_Path"><di:waypoint x="620" y="595" /><di:waypoint x="845" y="615" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_ToWard_WaitBed_di" bpmnElement="Flow_ToWard_WaitBed"><di:waypoint x="595" y="620" /><di:waypoint x="595" y="728" /><di:waypoint x="650" y="728" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_WaitBed_CleaningJoin_di" bpmnElement="Flow_WaitBed_CleaningJoin"><di:waypoint x="790" y="728" /><di:waypoint x="870" y="640" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_CleaningJoin_Disinfection_di" bpmnElement="Flow_CleaningJoin_Disinfection"><di:waypoint x="895" y="615" /><di:waypoint x="950" y="613" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Disinfection_Buffer_di" bpmnElement="Flow_Disinfection_Buffer"><di:waypoint x="1080" y="613" /><di:waypoint x="1130" y="613" /></bpmndi:BPMNEdge>
              <bpmndi:BPMNEdge id="Flow_Buffer_End_di" bpmnElement="Flow_Buffer_End"><di:waypoint x="1260" y="613" /><di:waypoint x="1315" y="613" /></bpmndi:BPMNEdge>
            </bpmndi:BPMNPlane>
          </bpmndi:BPMNDiagram>
        </bpmn2:definitions>
        """;
}
