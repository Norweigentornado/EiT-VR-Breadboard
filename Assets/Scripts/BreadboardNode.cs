using System.Collections.Generic;

/// <summary>
/// Represents a group of electrically connected breadboard holes.
/// The CircuitSolver writes to solvedVoltage each frame.
/// Components read solvedVoltage — they never write voltage directly anymore.
/// </summary>
public class BreadboardNode
{
    public List<BreadboardSocket> sockets = new List<BreadboardSocket>();

    // ── Set by voltage sources (BatteryComponent) ───────────────────
    /// <summary>If true, this node is held at a fixed voltage by a source.</summary>
    public bool isVoltageSource = false;
    /// <summary>The fixed voltage imposed by a source (only valid if isVoltageSource).</summary>
    public float sourceVoltage = 0f;

    // ── Written by CircuitSolver each frame ─────────────────────────
    /// <summary>The solved voltage at this node. Read this in LEDs, etc.</summary>
    public float solvedVoltage = 0f;

    // ── Convenience ─────────────────────────────────────────────────
    public bool IsPowered => solvedVoltage > 0f;

    /// <summary>Legacy shim — existing code reading .voltage still compiles.</summary>
    public float voltage => solvedVoltage;
}