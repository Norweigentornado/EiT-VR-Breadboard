using System.Collections.Generic;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using UnityEngine;

public class BreadboardLogic : MonoBehaviour
{
    List<BreadboardNode> nodes = new List<BreadboardNode>();

    private Dictionary<(int row, int col), BreadboardSocket> _socketLookup
    = new Dictionary<(int row, int col), BreadboardSocket>();

    void Awake()
    {
        
    }

    void Start()
    {
        BuildBreadboard();
        DebugPowerRails();
        BuildSocketLookup();
    }

    // Sort by the number in the GameObject name, e.g. "Socket (3)" → 3, "Socket" → 0
    BreadboardSocket[] GetSortedSockets(Transform row)
    {
        // Only get sockets that are DIRECT children of the row, not grandchildren
        List<BreadboardSocket> sockets = new List<BreadboardSocket>();
        foreach (Transform child in row)
        {
            BreadboardSocket socket = child.GetComponent<BreadboardSocket>();
            if (socket != null)
                sockets.Add(socket);
        }

        sockets.Sort((a, b) =>
        {
            int numA = ExtractNumber(a.name);
            int numB = ExtractNumber(b.name);
            return numA.CompareTo(numB);
        });

        return sockets.ToArray();
    }

    int ExtractNumber(string name)
    {
        var match = Regex.Match(name, @"\((\d+)\)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    void BuildBreadboard()
    {
        foreach (Transform row in transform)
        {
            if (!row.name.Contains("Row")) continue;

            BreadboardSocket[] sockets = GetSortedSockets(row);
            if (sockets.Length < 14) continue;

            /*
             * After sorting by name number:
             *  "Socket"     → 0  = left GND
             *  "Socket (1)" → 1  = left VCC
             *  "Socket (2)" → 2  \
             *  ...                  j–f
             *  "Socket (6)" → 6  /
             *  "Socket (7)" → 7  \
             *  ...                  e–a
             *  "Socket (11)"→ 11 /
             *  "Socket (12)"→ 12 = right GND
             *  "Socket (13)"→ 13 = right VCC
             */

            CreateNode(sockets, 2, 6);   // j-f
            CreateNode(sockets, 7, 11);  // e-a
        }

        BuildPowerRails();
    }

    void BuildPowerRails()
    {
        var leftGND = new List<BreadboardSocket>();
        var leftVCC = new List<BreadboardSocket>();
        var rightGND = new List<BreadboardSocket>();
        var rightVCC = new List<BreadboardSocket>();

        foreach (Transform row in transform)
        {
            if (!row.name.Contains("Row")) continue;

            BreadboardSocket[] sockets = GetSortedSockets(row);
            if (sockets.Length < 14) continue;

            leftGND.Add(sockets[0]);   // "Socket"      → GND
            leftVCC.Add(sockets[1]);   // "Socket (1)"  → VCC
            rightGND.Add(sockets[12]); // "Socket (12)" → GND
            rightVCC.Add(sockets[13]); // "Socket (13)" → VCC
        }

        CreatePowerRailNode(leftGND, isPositive: false);  // 0 V
        CreatePowerRailNode(leftVCC, isPositive: true);   // 5 V
        CreatePowerRailNode(rightGND, isPositive: false);  // 0 V
        CreatePowerRailNode(rightVCC, isPositive: true);   // 5 V
    }

    void CreatePowerRailNode(List<BreadboardSocket> sockets, bool isPositive)
    {
        var node = new BreadboardNode();
        node.isVoltageSource = true;
        node.sourceVoltage = isPositive ? 5f : 0f;

        foreach (var socket in sockets)
        {
            socket.node = node;
            node.sockets.Add(socket);
        }
        nodes.Add(node);

        // Register with solver so it's always included in the solve
        if (CircuitSolver.Instance != null)
            CircuitSolver.Instance.RegisterFixedNode(node);
    }

    void CreateNode(BreadboardSocket[] sockets, int start, int end)
    {
        var node = new BreadboardNode();
        for (int i = start; i <= end; i++)
        {
            sockets[i].node = node;
            node.sockets.Add(sockets[i]);
        }
        nodes.Add(node);
    }

    void DebugPowerRails()
    {
        Debug.Log("=== POWER RAIL DEBUG ===");
        foreach (Transform row in transform)
        {
            if (!row.name.Contains("Row")) continue;

            BreadboardSocket[] sockets = GetSortedSockets(row);

            Debug.Log($"[{row.name}] Total sockets found: {sockets.Length}");
            for (int i = 0; i < sockets.Length; i++)
            {
                var s = sockets[i];
                Debug.Log($"  [{i}] name='{s.name}' " +
                          $"extractedNum={ExtractNumber(s.name)} " +
                          $"pos={s.transform.position} " +
                          $"node={(s.node != null ? "OK" : "NULL")} " +
                          $"isVS={s.node?.isVoltageSource} V={s.node?.sourceVoltage}");
            }

            // Only log one row — we just need to see the pattern
            break;
        }
        Debug.Log("=== END POWER RAIL DEBUG ===");
    }
    void BuildSocketLookup()
    {
        int rowIndex = 0;
        foreach (Transform row in transform)
        {
            if (!row.name.Contains("Row")) continue;
            BreadboardSocket[] sockets = GetSortedSockets(row);
            for (int col = 0; col < sockets.Length; col++)
                _socketLookup[(rowIndex, col)] = sockets[col];
            rowIndex++;
        }
    }

    public BreadboardSocket GetSocket(int row, int col)
    {
        _socketLookup.TryGetValue((row, col), out var socket);
        return socket;
    }

    // Given a socket, return its (row, col)
    public (int row, int col)? GetSocketCoords(BreadboardSocket target)
    {
        foreach (var kvp in _socketLookup)
            if (kvp.Value == target) return kvp.Key;
        return null;
    }
}
