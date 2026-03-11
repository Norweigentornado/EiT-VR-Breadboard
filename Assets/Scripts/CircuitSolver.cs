using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Place ONE of these anywhere in your scene (e.g. on the Breadboard GameObject).
/// 
/// Every frame it:
///   1. Collects all registered nodes, resistors, voltage sources
///   2. Builds a conductance matrix (Modified Nodal Analysis)
///   3. Solves the linear system with Gaussian elimination
///   4. Writes the result back to each BreadboardNode.solvedVoltage
/// 
/// Components register/unregister themselves — nothing needs to be wired manually.
/// </summary>
public class CircuitSolver : MonoBehaviour
{
    public static CircuitSolver Instance { get; private set; }

    [Header("Debug")]
    public bool logSolverOutput = false;

    // ── Registered components ────────────────────────────────────────
    private readonly List<BreadboardNode>     _nodes     = new();
    private readonly List<ITwoTerminalComponent> _resistors = new();
    private readonly List<BatteryComponent>   _batteries = new();

    // ── Unity lifecycle ──────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void LateUpdate()
    {
        // Collect current node set from all batteries and resistors
        RebuildNodeList();
        if (_nodes.Count == 0) return;

        Solve();
    }

    // ── Registration API (called by components themselves) ───────────

    public void RegisterBattery(BatteryComponent b)
    {
        if (!_batteries.Contains(b)) _batteries.Add(b);
    }

    public void UnregisterBattery(BatteryComponent b)
    {
        _batteries.Remove(b);
    }

    public void RegisterComponent(ITwoTerminalComponent r)
    {
        if (!_resistors.Contains(r)) _resistors.Add(r);
    }

    public void UnregisterComponent(ITwoTerminalComponent r)
    {
        _resistors.Remove(r);
    }

    // ── Node collection ──────────────────────────────────────────────

    void RebuildNodeList()
    {
        _nodes.Clear();

        // Gather every node touched by a connected battery or resistor
        foreach (var bat in _batteries)
        {
            AddNodeIfConnected(bat.PositiveNode);
            AddNodeIfConnected(bat.NegativeNode);
        }

        foreach (var res in _resistors)
        {
            AddNodeIfConnected(res.NodeA);
            AddNodeIfConnected(res.NodeB);
        }
    }

    void AddNodeIfConnected(BreadboardNode node)
    {
        if (node != null && !_nodes.Contains(node))
            _nodes.Add(node);
    }

    // ── MNA Solver ───────────────────────────────────────────────────

    void Solve()
    {
        int n = _nodes.Count;

        // G matrix (conductance) and I vector (current injections)
        float[,] G = new float[n, n];
        float[]  I = new float[n];

        // ── Stamp resistors ──────────────────────────────────────────
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

        // ── Stamp voltage sources (batteries) via node fixing ────────
        // For each voltage-source node, replace its row with a fixed-voltage equation.
        foreach (var node in _nodes)
        {
            if (!node.isVoltageSource) continue;

            int i = _nodes.IndexOf(node);
            if (i < 0) continue;

            // Zero the row, set diagonal to 1, RHS to known voltage
            for (int j = 0; j < n; j++)
                G[i, j] = 0f;

            G[i, i] = 1f;
            I[i]    = node.sourceVoltage;
        }

        // ── Floating nodes (nothing connected) get 0V ────────────────
        for (int i = 0; i < n; i++)
        {
            bool allZero = true;
            for (int j = 0; j < n; j++)
                if (G[i, j] != 0f) { allZero = false; break; }

            if (allZero)
            {
                G[i, i] = 1f;
                I[i]    = 0f;
            }
        }

        // ── Gaussian elimination ─────────────────────────────────────
        float[] result = GaussianElimination(G, I, n);

        if (result == null)
        {
            Debug.LogWarning("[CircuitSolver] Singular matrix — circuit may be unconnected.");
            return;
        }

        // ── Write back ───────────────────────────────────────────────
        for (int i = 0; i < n; i++)
        {
            _nodes[i].solvedVoltage = result[i];

            if (logSolverOutput)
                Debug.Log($"[CircuitSolver] Node {i}: {result[i]:F3}V");
        }

        // Any node not in the current solve gets zeroed
        // (handles disconnected nodes left over from a previous frame)
    }

    // ── Gaussian elimination with partial pivoting ───────────────────

    static float[] GaussianElimination(float[,] A, float[] b, int n)
    {
        // Augmented matrix
        float[,] M = new float[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                M[i, j] = A[i, j];
            M[i, n] = b[i];
        }

        for (int col = 0; col < n; col++)
        {
            // Partial pivot
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

            if (maxVal < 1e-9f) return null; // singular

            // Swap rows
            if (pivot != col)
            {
                for (int j = 0; j <= n; j++)
                    (M[col, j], M[pivot, j]) = (M[pivot, j], M[col, j]);
            }

            // Eliminate below
            for (int row = col + 1; row < n; row++)
            {
                float factor = M[row, col] / M[col, col];
                for (int j = col; j <= n; j++)
                    M[row, j] -= factor * M[col, j];
            }
        }

        // Back substitution
        float[] x = new float[n];
        for (int i = n - 1; i >= 0; i--)
        {
            x[i] = M[i, n];
            for (int j = i + 1; j < n; j++)
                x[i] -= M[i, j] * x[j];
            x[i] /= M[i, i];
        }

        return x;
    }
}