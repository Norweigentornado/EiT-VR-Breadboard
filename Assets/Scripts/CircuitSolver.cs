using System.Collections.Generic;
using UnityEngine;

public class CircuitSolver : MonoBehaviour
{
    public static CircuitSolver Instance { get; private set; }

    [Header("Debug")]
    public bool logSolverOutput = false;

    private readonly List<BreadboardNode> _nodes = new();
    private readonly List<ITwoTerminalComponent> _resistors = new();
    private readonly List<BreadboardNode> _fixedNodes = new();

    // Solved current per component (Amps). Positive = NodeA→NodeB.
    private readonly Dictionary<ITwoTerminalComponent, float> _componentCurrents = new();

    /// <summary>Returns the solved current (Amps) for a component. Positive = A→B.</summary>
    public float GetCurrent(ITwoTerminalComponent comp)
    {
        return _componentCurrents.TryGetValue(comp, out float c) ? c : 0f;
    }

    // Track last state to avoid spamming logs
    private int _lastNodeCount = -1;
    private int _lastResistorCount = -1;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        RebuildNodeList();
        if (_nodes.Count == 0) return;
        Solve();
        LogCircuitStatusIfChanged();
    }

    public void RegisterFixedNode(BreadboardNode node)
    {
        if (!_fixedNodes.Contains(node))
        {
            _fixedNodes.Add(node);
            Debug.Log($"[CircuitSolver] Power rail registered: {(node.sourceVoltage > 0 ? node.sourceVoltage + "V (VCC)" : "0V (GND)")}");
        }
    }

    public void RegisterComponent(ITwoTerminalComponent r)
    {
        if (!_resistors.Contains(r))
        {
            _resistors.Add(r);
            Debug.Log($"[CircuitSolver] Component registered: {r.GetType().Name} ({r.OhmsValue}Ω)");
        }
    }

    public void UnregisterComponent(ITwoTerminalComponent r)
    {
        if (_resistors.Remove(r))
            Debug.Log($"[CircuitSolver] Component unregistered: {r.GetType().Name}");
    }

    void RebuildNodeList()
    {
        _nodes.Clear();

        foreach (var node in _fixedNodes)
            _nodes.Add(node);

        foreach (var res in _resistors)
        {
            AddNode(res.NodeA);
            AddNode(res.NodeB);
        }
    }

    void AddNode(BreadboardNode node)
    {
        if (node != null && !_nodes.Contains(node))
            _nodes.Add(node);
    }

    // Only logs when something changes — not every frame
    void LogCircuitStatusIfChanged()
    {
        if (_nodes.Count == _lastNodeCount && _resistors.Count == _lastResistorCount)
            return;

        _lastNodeCount = _nodes.Count;
        _lastResistorCount = _resistors.Count;

        Debug.Log($"[CircuitSolver] Circuit changed — Nodes: {_nodes.Count}, Components: {_resistors.Count}");

        // Check each component is properly connected
        foreach (var res in _resistors)
        {
            bool aOk = res.NodeA != null;
            bool bOk = res.NodeB != null;
            bool complete = aOk && bOk;

            Debug.Log($"  {res.GetType().Name} ({res.OhmsValue}Ω) — " +
                      $"NodeA: {(aOk ? $"{res.NodeA.solvedVoltage:F2}V" : "NOT CONNECTED")} | " +
                      $"NodeB: {(bOk ? $"{res.NodeB.solvedVoltage:F2}V" : "NOT CONNECTED")} | " +
                      $"{(complete ? "COMPLETE" : "INCOMPLETE")}");
        }

        // Log power rail voltages
        foreach (var node in _fixedNodes)
            Debug.Log($"  Rail: {node.sourceVoltage}V → solvedVoltage={node.solvedVoltage:F2}V");
    }

    void Solve()
    {
        int n = _nodes.Count;
        float[,] G = new float[n, n];
        float[] I = new float[n];

        foreach (var res in _resistors)
        {
            if (res.NodeA == null || res.NodeB == null) continue;
            if (res.OhmsValue <= 0f) continue;

            int a = _nodes.IndexOf(res.NodeA);
            int b = _nodes.IndexOf(res.NodeB);
            if (a < 0 || b < 0) continue;

            float g = 1f / res.OhmsValue;
            G[a, a] += g;
            G[b, b] += g;
            G[a, b] -= g;
            G[b, a] -= g;
        }

        for (int i = 0; i < n; i++)
        {
            if (!_nodes[i].isVoltageSource) continue;
            for (int j = 0; j < n; j++) G[i, j] = 0f;
            G[i, i] = 1f;
            I[i] = _nodes[i].sourceVoltage;
        }

        for (int i = 0; i < n; i++)
        {
            if (_nodes[i].isVoltageSource) continue;
            bool allZero = true;
            for (int j = 0; j < n; j++)
                if (G[i, j] != 0f) { allZero = false; break; }
            if (allZero) { G[i, i] = 1f; I[i] = 0f; }
        }

        float[] result = GaussianElimination(G, I, n);

        if (result == null)
        {
            Debug.LogWarning("[CircuitSolver] Singular matrix — circuit may be unconnected.");
            foreach (var node in _nodes)
                if (!node.isVoltageSource)
                    node.solvedVoltage = 0f;
            _componentCurrents.Clear();
            return;
        }

        for (int i = 0; i < n; i++)
        {
            _nodes[i].solvedVoltage = result[i];
            if (logSolverOutput)
                Debug.Log($"[CircuitSolver] Node {i}: {result[i]:F3}V");
        }

        // Compute current through each component: I = (Va - Vb) / R
        _componentCurrents.Clear();
        foreach (var res in _resistors)
        {
            if (res.NodeA == null || res.NodeB == null || res.OhmsValue <= 0f)
                continue;
            float current = (res.NodeA.solvedVoltage - res.NodeB.solvedVoltage) / res.OhmsValue;
            _componentCurrents[res] = current;
        }
    }

    static float[] GaussianElimination(float[,] A, float[] b, int n)
    {
        float[,] M = new float[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++) M[i, j] = A[i, j];
            M[i, n] = b[i];
        }

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            float maxVal = Mathf.Abs(M[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                if (Mathf.Abs(M[row, col]) > maxVal)
                {
                    maxVal = Mathf.Abs(M[row, col]);
                    pivot = row;
                }
            }

            if (maxVal < 1e-9f) return null;

            if (pivot != col)
                for (int j = 0; j <= n; j++)
                    (M[col, j], M[pivot, j]) = (M[pivot, j], M[col, j]);

            for (int row = col + 1; row < n; row++)
            {
                float factor = M[row, col] / M[col, col];
                for (int j = col; j <= n; j++)
                    M[row, j] -= factor * M[col, j];
            }
        }

        float[] x = new float[n];
        for (int i = n - 1; i >= 0; i--)
        {
            x[i] = M[i, n];
            for (int j = i + 1; j < n; j++) x[i] -= M[i, j] * x[j];
            x[i] /= M[i, i];
        }

        return x;
    }
}