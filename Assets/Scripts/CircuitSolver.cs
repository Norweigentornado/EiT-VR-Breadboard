using System.Collections.Generic;
using UnityEngine;

public class CircuitSolver : MonoBehaviour
{
    public static CircuitSolver Instance { get; private set; }

    [Header("Debug")]
    public bool logSolverOutput = false;

    private readonly List<BreadboardNode> _nodes = new();
    private readonly List<ITwoTerminalComponent> _resistors = new();

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
        if (_nodes.Count == 0)
        {
            Debug.LogWarning("[CircuitSolver] No nodes found!");
            return;
        }

        // Add this block:
        int vsCount = 0;
        foreach (var node in _nodes)
            if (node.isVoltageSource) vsCount++;
        if (vsCount == 0)
            Debug.LogWarning("[CircuitSolver] No voltage source nodes found — power rails may not be registered!");

        Solve();
    }

    // ── Registration ─────────────────────────────────────────────────

    public void RegisterComponent(ITwoTerminalComponent r)
    {
        if (!_resistors.Contains(r)) _resistors.Add(r);
    }

    public void UnregisterComponent(ITwoTerminalComponent r)
    {
        _resistors.Remove(r);
    }

    // ── Node collection ───────────────────────────────────────────────

    void RebuildNodeList()
    {
        _nodes.Clear();

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

    // ── MNA Solver ────────────────────────────────────────────────────

    void Solve()
    {
        int n = _nodes.Count;
        float[,] G = new float[n, n];
        float[] I = new float[n];

        // Stamp resistors
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

        // Fix voltage-source rows (power rails)
        for (int i = 0; i < n; i++)
        {
            if (!_nodes[i].isVoltageSource) continue;

            for (int j = 0; j < n; j++) G[i, j] = 0f;
            G[i, i] = 1f;
            I[i] = _nodes[i].sourceVoltage;
        }

        // Floating nodes → 0 V
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
            return;
        }

        for (int i = 0; i < n; i++)
        {
            _nodes[i].solvedVoltage = result[i];
            if (logSolverOutput)
                Debug.Log($"[CircuitSolver] Node {i}: {result[i]:F3}V");
        }
    }

    // ── Gaussian elimination with partial pivoting ────────────────────

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