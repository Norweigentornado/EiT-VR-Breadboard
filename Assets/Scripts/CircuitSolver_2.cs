using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Improved DC breadboard solver.
///
/// Design goals:
/// - Treat jumper cables as ideal conductors that merge breadboard nodes.
/// - Solve node voltages for the reduced circuit.
/// - Compute branch currents for resistors and diode-like components.
/// - Keep the original CircuitSolver untouched.
///
/// Notes:
/// - This solver assumes a single battery.
/// - Resistors are linear.
/// - LEDComponent is treated as a simple piecewise-linear diode:
///   off below forwardVoltage, on above forwardVoltage with internalResistance.
/// - Ideal cables are topology only; they are not stamped as resistors.
/// </summary>
public class CircuitSolver_2 : MonoBehaviour
{
    public static CircuitSolver_2 Instance { get; private set; }

    [Header("Debug")]
    public bool logSolverOutput = false;

    [Tooltip("Maximum number of iterations used to settle diode on/off states.")]
    public int maxDiodeIterations = 12;

    [Tooltip("Voltages/currents smaller than this are treated as zero.")]
    public float epsilon = 1e-6f;

    private readonly List<ITwoTerminalComponent> _allTwoTerminal = new();
    private readonly List<CableElectrical> _cables = new();
    private readonly List<BatteryComponent> _batteries = new();
    private readonly List<BranchInfo> _solvedBranches = new();
    private readonly Dictionary<Object, float> _componentCurrentMap = new();
    private readonly Dictionary<BreadboardNode, int> _nodeVoltageIndexMap = new();
    private readonly Dictionary<BreadboardNode, float> _nodeVoltageMap = new();

    public IReadOnlyList<BranchInfo> SolvedBranches => _solvedBranches;

    public struct BranchInfo
    {
        public Object component;
        public BreadboardNode nodeA;
        public BreadboardNode nodeB;
        public float currentAmps;
        public float voltageDrop;
        public string kind;
    }

    private class UnionFind
    {
        private readonly Dictionary<BreadboardNode, BreadboardNode> _parent = new();

        public void Add(BreadboardNode node)
        {
            if (node != null && !_parent.ContainsKey(node))
                _parent[node] = node;
        }

        public BreadboardNode Find(BreadboardNode node)
        {
            if (node == null)
                return null;

            Add(node);
            BreadboardNode parent = _parent[node];
            if (!ReferenceEquals(parent, node))
                _parent[node] = Find(parent);
            return _parent[node];
        }

        public void Union(BreadboardNode a, BreadboardNode b)
        {
            if (a == null || b == null)
                return;

            BreadboardNode rootA = Find(a);
            BreadboardNode rootB = Find(b);
            if (!ReferenceEquals(rootA, rootB))
                _parent[rootB] = rootA;
        }
    }

    private class ResistorBranch
    {
        public Object sourceObject;
        public BreadboardNode rootA;
        public BreadboardNode rootB;
        public float resistance;
        public string kind;
    }

    private class DiodeBranch
    {
        public LEDComponent sourceObject;
        public BreadboardNode anodeRoot;
        public BreadboardNode cathodeRoot;
        public float forwardVoltage;
        public float internalResistance;
        public bool isOn;
    }

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
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        RefreshComponentLists();
        Solve();
    }

    public bool TryGetComponentCurrent(Object component, out float currentAmps)
    {
        return _componentCurrentMap.TryGetValue(component, out currentAmps);
    }

    public float GetSolvedVoltage(BreadboardNode node)
    {
        return node != null && _nodeVoltageMap.TryGetValue(node, out float voltage)
            ? voltage
            : 0f;
    }

    private void RefreshComponentLists()
    {
        _allTwoTerminal.Clear();
        _cables.Clear();
        _batteries.Clear();

        foreach (MonoBehaviour behaviour in FindObjectsOfType<MonoBehaviour>(true))
        {
            if (behaviour is ITwoTerminalComponent component)
            {
                _allTwoTerminal.Add(component);

                if (behaviour is CableElectrical cable)
                    _cables.Add(cable);
            }

            if (behaviour is BatteryComponent battery)
                _batteries.Add(battery);
        }
    }

    private void Solve()
    {
        _solvedBranches.Clear();
        _componentCurrentMap.Clear();
        _nodeVoltageIndexMap.Clear();
        _nodeVoltageMap.Clear();

        if (_batteries.Count == 0)
        {
            ZeroKnownNodeVoltages();
            return;
        }

        BatteryComponent battery = FindActiveBattery();
        if (battery == null || battery.PositiveNode == null || battery.NegativeNode == null)
        {
            ZeroKnownNodeVoltages();
            return;
        }

        UnionFind unionFind = new UnionFind();
        HashSet<BreadboardNode> rawNodes = new HashSet<BreadboardNode>();

        CollectRawNodes(rawNodes, battery);
        foreach (ITwoTerminalComponent component in _allTwoTerminal)
            CollectRawNodes(rawNodes, component);

        foreach (BreadboardNode node in rawNodes)
            unionFind.Add(node);

        foreach (CableElectrical cable in _cables)
            unionFind.Union(cable.NodeA, cable.NodeB);

        BreadboardNode batteryPositiveRoot = unionFind.Find(battery.PositiveNode);
        BreadboardNode batteryNegativeRoot = unionFind.Find(battery.NegativeNode);

        if (batteryPositiveRoot == null || batteryNegativeRoot == null)
        {
            ZeroKnownNodeVoltages();
            return;
        }

        if (ReferenceEquals(batteryPositiveRoot, batteryNegativeRoot))
        {
            Debug.LogWarning("[CircuitSolver_2] Battery terminals are shorted by ideal wiring.");
            ZeroKnownNodeVoltages();
            return;
        }

        List<ResistorBranch> resistors = new List<ResistorBranch>();
        List<DiodeBranch> diodes = new List<DiodeBranch>();
        HashSet<BreadboardNode> reducedNodes = new HashSet<BreadboardNode>();

        reducedNodes.Add(batteryPositiveRoot);
        reducedNodes.Add(batteryNegativeRoot);

        BuildBranches(unionFind, resistors, diodes, reducedNodes);

        Dictionary<BreadboardNode, float> solvedVoltages = SolveReducedNetwork(
            reducedNodes,
            resistors,
            diodes,
            batteryPositiveRoot,
            batteryNegativeRoot,
            battery.voltage);

        if (solvedVoltages == null)
        {
            Debug.LogWarning("[CircuitSolver_2] Could not solve circuit.");
            ZeroKnownNodeVoltages();
            return;
        }

        foreach (BreadboardNode rawNode in rawNodes)
        {
            BreadboardNode root = unionFind.Find(rawNode);
            float voltage = solvedVoltages.TryGetValue(root, out float value) ? value : 0f;
            rawNode.solvedVoltage = voltage;
            _nodeVoltageMap[rawNode] = voltage;
        }

        PopulateCurrents(solvedVoltages, resistors, diodes);
        PopulateCableVisualCurrents(unionFind, rawNodes, resistors, diodes);
    }

    private BatteryComponent FindActiveBattery()
    {
        foreach (BatteryComponent battery in _batteries)
        {
            if (battery != null && battery.PositiveNode != null && battery.NegativeNode != null)
                return battery;
        }

        return null;
    }

    private void CollectRawNodes(HashSet<BreadboardNode> rawNodes, BatteryComponent battery)
    {
        if (battery.PositiveNode != null)
            rawNodes.Add(battery.PositiveNode);
        if (battery.NegativeNode != null)
            rawNodes.Add(battery.NegativeNode);
    }

    private void CollectRawNodes(HashSet<BreadboardNode> rawNodes, ITwoTerminalComponent component)
    {
        if (component.NodeA != null)
            rawNodes.Add(component.NodeA);
        if (component.NodeB != null)
            rawNodes.Add(component.NodeB);
    }

    private void BuildBranches(
        UnionFind unionFind,
        List<ResistorBranch> resistors,
        List<DiodeBranch> diodes,
        HashSet<BreadboardNode> reducedNodes)
    {
        foreach (ITwoTerminalComponent component in _allTwoTerminal)
        {
            if (component == null || component.NodeA == null || component.NodeB == null)
                continue;

            if (component is CableElectrical)
                continue;

            BreadboardNode rootA = unionFind.Find(component.NodeA);
            BreadboardNode rootB = unionFind.Find(component.NodeB);

            if (rootA == null || rootB == null || ReferenceEquals(rootA, rootB))
                continue;

            reducedNodes.Add(rootA);
            reducedNodes.Add(rootB);

            if (component is LEDComponent led)
            {
                diodes.Add(new DiodeBranch
                {
                    sourceObject = led,
                    anodeRoot = rootA,
                    cathodeRoot = rootB,
                    forwardVoltage = Mathf.Max(0f, led.forwardVoltage),
                    internalResistance = Mathf.Max(led.internalResistance, 0.1f),
                    isOn = false
                });
                continue;
            }

            if (component.OhmsValue <= epsilon)
                continue;

            resistors.Add(new ResistorBranch
            {
                sourceObject = component as Object,
                rootA = rootA,
                rootB = rootB,
                resistance = component.OhmsValue,
                kind = component.GetType().Name
            });
        }
    }

    private Dictionary<BreadboardNode, float> SolveReducedNetwork(
        HashSet<BreadboardNode> reducedNodes,
        List<ResistorBranch> resistors,
        List<DiodeBranch> diodes,
        BreadboardNode batteryPositiveRoot,
        BreadboardNode batteryNegativeRoot,
        float batteryVoltage)
    {
        List<BreadboardNode> orderedNodes = new List<BreadboardNode>(reducedNodes);
        foreach (BreadboardNode node in orderedNodes)
            _nodeVoltageIndexMap[node] = _nodeVoltageIndexMap.Count;

        Dictionary<BreadboardNode, float> lastVoltages = new Dictionary<BreadboardNode, float>();
        foreach (BreadboardNode node in orderedNodes)
            lastVoltages[node] = ReferenceEquals(node, batteryPositiveRoot) ? batteryVoltage : 0f;

        for (int iteration = 0; iteration < Mathf.Max(1, maxDiodeIterations); iteration++)
        {
            float[,] matrix = new float[orderedNodes.Count, orderedNodes.Count];
            float[] rhs = new float[orderedNodes.Count];

            foreach (ResistorBranch resistor in resistors)
                StampResistor(matrix, resistor.rootA, resistor.rootB, resistor.resistance);

            bool diodeStateChanged = false;
            foreach (DiodeBranch diode in diodes)
            {
                float anodeVoltage = lastVoltages.TryGetValue(diode.anodeRoot, out float va) ? va : 0f;
                float cathodeVoltage = lastVoltages.TryGetValue(diode.cathodeRoot, out float vb) ? vb : 0f;
                bool shouldBeOn = (anodeVoltage - cathodeVoltage) > diode.forwardVoltage + epsilon;

                if (diode.isOn != shouldBeOn)
                {
                    diode.isOn = shouldBeOn;
                    diodeStateChanged = true;
                }

                if (diode.isOn)
                    StampPiecewiseDiode(matrix, rhs, diode.anodeRoot, diode.cathodeRoot, diode.internalResistance, diode.forwardVoltage);
            }

            EnforceFixedVoltage(matrix, rhs, batteryPositiveRoot, batteryVoltage);
            EnforceFixedVoltage(matrix, rhs, batteryNegativeRoot, 0f);
            FixFloatingRows(matrix, rhs);

            float[] solution = GaussianElimination(matrix, rhs, orderedNodes.Count);
            if (solution == null)
                return null;

            Dictionary<BreadboardNode, float> solved = new Dictionary<BreadboardNode, float>(orderedNodes.Count);
            for (int i = 0; i < orderedNodes.Count; i++)
                solved[orderedNodes[i]] = solution[i];

            bool voltageStable = VoltagesConverged(lastVoltages, solved);
            lastVoltages = solved;

            if (!diodeStateChanged && voltageStable)
                return solved;
        }

        return lastVoltages;
    }

    private void StampResistor(float[,] matrix, BreadboardNode a, BreadboardNode b, float resistance)
    {
        if (!_nodeVoltageIndexMap.TryGetValue(a, out int ia) || !_nodeVoltageIndexMap.TryGetValue(b, out int ib))
            return;

        float conductance = 1f / resistance;
        matrix[ia, ia] += conductance;
        matrix[ib, ib] += conductance;
        matrix[ia, ib] -= conductance;
        matrix[ib, ia] -= conductance;
    }

    private void StampPiecewiseDiode(float[,] matrix, float[] rhs, BreadboardNode a, BreadboardNode b, float resistance, float forwardVoltage)
    {
        if (!_nodeVoltageIndexMap.TryGetValue(a, out int ia) || !_nodeVoltageIndexMap.TryGetValue(b, out int ib))
            return;

        float conductance = 1f / resistance;

        matrix[ia, ia] += conductance;
        matrix[ib, ib] += conductance;
        matrix[ia, ib] -= conductance;
        matrix[ib, ia] -= conductance;

        rhs[ia] -= conductance * forwardVoltage;
        rhs[ib] += conductance * forwardVoltage;
    }

    private void EnforceFixedVoltage(float[,] matrix, float[] rhs, BreadboardNode node, float voltage)
    {
        if (!_nodeVoltageIndexMap.TryGetValue(node, out int row))
            return;

        int n = rhs.Length;
        for (int col = 0; col < n; col++)
            matrix[row, col] = 0f;

        matrix[row, row] = 1f;
        rhs[row] = voltage;
    }

    private void FixFloatingRows(float[,] matrix, float[] rhs)
    {
        int n = rhs.Length;
        for (int row = 0; row < n; row++)
        {
            bool allZero = true;
            for (int col = 0; col < n; col++)
            {
                if (Mathf.Abs(matrix[row, col]) > epsilon)
                {
                    allZero = false;
                    break;
                }
            }

            if (allZero)
            {
                matrix[row, row] = 1f;
                rhs[row] = 0f;
            }
        }
    }

    private bool VoltagesConverged(Dictionary<BreadboardNode, float> oldVoltages, Dictionary<BreadboardNode, float> newVoltages)
    {
        foreach (KeyValuePair<BreadboardNode, float> pair in newVoltages)
        {
            float oldValue = oldVoltages.TryGetValue(pair.Key, out float value) ? value : 0f;
            if (Mathf.Abs(pair.Value - oldValue) > 1e-4f)
                return false;
        }

        return true;
    }

    private void PopulateCurrents(
        Dictionary<BreadboardNode, float> solvedVoltages,
        List<ResistorBranch> resistors,
        List<DiodeBranch> diodes)
    {
        foreach (ResistorBranch resistor in resistors)
        {
            float va = solvedVoltages.TryGetValue(resistor.rootA, out float a) ? a : 0f;
            float vb = solvedVoltages.TryGetValue(resistor.rootB, out float b) ? b : 0f;
            float current = (va - vb) / resistor.resistance;

            StoreBranch(
                resistor.sourceObject,
                resistor.rootA,
                resistor.rootB,
                va - vb,
                current,
                resistor.kind);
        }

        foreach (DiodeBranch diode in diodes)
        {
            float va = solvedVoltages.TryGetValue(diode.anodeRoot, out float a) ? a : 0f;
            float vb = solvedVoltages.TryGetValue(diode.cathodeRoot, out float b) ? b : 0f;
            float current = diode.isOn
                ? Mathf.Max(0f, (va - vb - diode.forwardVoltage) / diode.internalResistance)
                : 0f;

            StoreBranch(
                diode.sourceObject,
                diode.anodeRoot,
                diode.cathodeRoot,
                va - vb,
                current,
                "Diode");
        }
    }

    private void PopulateCableVisualCurrents(
        UnionFind unionFind,
        HashSet<BreadboardNode> rawNodes,
        List<ResistorBranch> resistors,
        List<DiodeBranch> diodes)
    {
        Dictionary<BreadboardNode, float> nodeThroughput = new Dictionary<BreadboardNode, float>();

        foreach (ResistorBranch resistor in resistors)
        {
            if (_componentCurrentMap.TryGetValue(resistor.sourceObject, out float current))
            {
                AddNodeThroughput(nodeThroughput, resistor.rootA, Mathf.Abs(current));
                AddNodeThroughput(nodeThroughput, resistor.rootB, Mathf.Abs(current));
            }
        }

        foreach (DiodeBranch diode in diodes)
        {
            if (_componentCurrentMap.TryGetValue(diode.sourceObject, out float current))
            {
                AddNodeThroughput(nodeThroughput, diode.anodeRoot, Mathf.Abs(current));
                AddNodeThroughput(nodeThroughput, diode.cathodeRoot, Mathf.Abs(current));
            }
        }

        foreach (CableElectrical cable in _cables)
        {
            if (cable == null || cable.NodeA == null || cable.NodeB == null)
                continue;

            BreadboardNode root = unionFind.Find(cable.NodeA);
            float visualCurrent = nodeThroughput.TryGetValue(root, out float throughput) ? throughput : 0f;

            StoreBranch(
                cable,
                cable.NodeA,
                cable.NodeB,
                0f,
                visualCurrent,
                "IdealCable");
        }

        foreach (BreadboardNode node in rawNodes)
        {
            BreadboardNode root = unionFind.Find(node);
            if (!_nodeVoltageMap.ContainsKey(node) && _nodeVoltageMap.TryGetValue(root, out float voltage))
                _nodeVoltageMap[node] = voltage;
        }
    }

    private void AddNodeThroughput(Dictionary<BreadboardNode, float> nodeThroughput, BreadboardNode node, float currentMagnitude)
    {
        if (node == null)
            return;

        if (nodeThroughput.ContainsKey(node))
            nodeThroughput[node] += currentMagnitude;
        else
            nodeThroughput[node] = currentMagnitude;
    }

    private void StoreBranch(Object component, BreadboardNode nodeA, BreadboardNode nodeB, float voltageDrop, float currentAmps, string kind)
    {
        if (component != null)
            _componentCurrentMap[component] = currentAmps;

        _solvedBranches.Add(new BranchInfo
        {
            component = component,
            nodeA = nodeA,
            nodeB = nodeB,
            voltageDrop = voltageDrop,
            currentAmps = currentAmps,
            kind = kind
        });

        if (logSolverOutput && component != null)
            Debug.Log($"[CircuitSolver_2] {kind} {component.name}: V={voltageDrop:F3}V I={currentAmps:F6}A");
    }

    private void ZeroKnownNodeVoltages()
    {
        foreach (BreadboardSocket socket in FindObjectsOfType<BreadboardSocket>(true))
        {
            if (socket != null && socket.node != null)
                socket.node.solvedVoltage = 0f;
        }
    }

    private static float[] GaussianElimination(float[,] A, float[] b, int n)
    {
        float[,] M = new float[n, n + 1];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
                M[i, j] = A[i, j];
            M[i, n] = b[i];
        }

        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            float maxVal = Mathf.Abs(M[col, col]);
            for (int row = col + 1; row < n; row++)
            {
                float candidate = Mathf.Abs(M[row, col]);
                if (candidate > maxVal)
                {
                    maxVal = candidate;
                    pivot = row;
                }
            }

            if (maxVal < 1e-9f)
                return null;

            if (pivot != col)
            {
                for (int j = col; j <= n; j++)
                {
                    float temp = M[col, j];
                    M[col, j] = M[pivot, j];
                    M[pivot, j] = temp;
                }
            }

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
            for (int j = i + 1; j < n; j++)
                x[i] -= M[i, j] * x[j];
            x[i] /= M[i, i];
        }

        return x;
    }
}
