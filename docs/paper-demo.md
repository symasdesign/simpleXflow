# EUROSIM 2026 Paper Demo

This document describes the presentation setup for showing the migrated simpleXflow tool alongside the EUROSIM 2026 paper.

## Source

The demo content is derived from the EUROSIM 2026 camera-ready paper `simpleXflow___EUROSIM_2026_camera_ready.pdf`. The paper is treated as a source document for terminology and examples, not as executable project instructions.

## Goal

The tool demo should show simpleXflow as described in the paper:

- a semi-formal language for simulation model specification
- BPMN-inspired symbols with simulation-oriented semantics
- explicit separation of system architecture and system logic
- explicit concepts for resources, queues, buffers, timing and contextual assumptions
- browser-based visualizer support for structured documentation and later analysis

## Built-in Samples

The workspace contains three presentation-oriented samples in `ProjectSamples`:

### Paper sample - Coffee break

This is the main presentation sample. It represents the coffee-break architecture described in the paper:

- peak demand of 200 visitors
- 20% direct seating
- 40% refrigerator then seating
- 40% hot beverages
- after hot beverages, 50% direct seating and 50% refrigerator then seating
- modeled consumables: coffee beans, milk and paper cups
- infrastructure such as water and electricity documented as contextual assumptions

Use this sample to explain the architecture view and the role of the Barista component as the bridge to a component-local logic view.

### Paper sample - M/M/1 queue

This compact reference sample supports the analytical mapping described in the paper:

- exponential interarrival times with rate lambda
- explicit FIFO queue
- capacity-1 server resource
- exponential service time with rate mu
- departure after service release

Use this sample when explaining that simpleXflow is not only a communicative process sketch, but can encode classical discrete-event simulation structures in a visually accessible way.

### Poster sample - Hospital emergency room

This sample reflects the emergency-room scenario from the WinterSim poster material and is useful when a richer, visually recognizable demonstration is needed:

- patient admission through check-in and waiting room
- assignment to Room1, Room2 or Room3
- routing to department or discharge
- room-level logic for initial treatment, examination and diagnosis
- ward-bed wait path and room disinfection before release
- resource annotations for doctors, nurses and cleaning staff

Use this sample to show that simpleXflow can present architecture and local logic views together while keeping simulation concepts such as resources, queues, capacity and post-treatment cleanup visible.

## Demo Flow

1. Log in to simpleXflow.
2. Open `Projects`.
3. Select `Paper sample - Coffee break` from the `Demo sample` selector.
4. Click `Load`.
5. Explain the architecture-level entity movement and the attached assumptions.
6. Click `Save` to persist the sample in the current tenant workspace.
7. Repeat with `Paper sample - M/M/1 queue` if the talk needs a shorter analytical reference model.
8. Load `Poster sample - Hospital emergency room` for the visually richer scenario and explain architecture plus room-level logic.

## Validation

The samples are covered by unit tests that parse the BPMN XML and verify that each sample contains:

- BPMN definitions
- at least one process
- BPMN diagram information
- shapes and edges required by the visual editor

Run:

```powershell
dotnet test simpleXflow.slnx
```

## Notes

The current implementation remains a migrated compatibility visualizer. The sample XML deliberately uses standard BPMN XML carriers plus annotations so the current bpmn-js based visualizer can render the examples while the application presents them as simpleXflow projects.
