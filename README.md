# Oppdateringer
Kommer til å lage en oppdatert readme + legge til kommentarer i mai når jeg er ferdig med eksamener o.l. Hvis det er noen spørsmål send mail til jonathlo@stud.ntnu.no. 



# VR Breadboard Circuit Simulator

A virtual reality breadboard simulator built in Unity that lets users build and test simple DC circuits in an interactive 3D environment.

## Overview

Users can grab and place electronic components onto a virtual breadboard using VR controllers. A real-time circuit solver computes node voltages and branch currents each frame, providing instant visual feedback — LEDs light up, current flow is animated through cables, and circuit state updates live as components are added or moved.

## Tech Stack

- **Engine**: Unity (C#)
- **VR Framework**: Unity XR Interaction Toolkit
- **Circuit Solving**: Custom nodal analysis solver (`CircuitSolver`, `CircuitSolver_2`)

## Project Structure

```
Assets/
├── Scripts/            # Core systems (circuit solvers, breadboard logic, components)
├── EiT Scripts/        # Cables, scaling, breadboard setup utilities
├── Prefabs/            # Component prefabs (LED, resistor, cable, etc.)
└── Scenes/             # Breadboard Room scene
```

## Getting Started

1. Open the project in Unity
2. Open the **Breadboard Room** scene (`Assets/Scenes/Breadboard Room`)
3. Connect a VR headset or use XR Device Simulator
4. Enter Play mode — grab components and place them on the breadboard to build circuits
