using UnityEngine;
using System.Collections.Generic;

public class BreadboardLogic : MonoBehaviour
{
    List<BreadboardNode> nodes = new List<BreadboardNode>();

    void Start()
    {
        BuildBreadboard();
    }

    void BuildBreadboard()
    {
        foreach (Transform row in transform)
        {
            if (!row.name.Contains("Row"))
                continue;

            BreadboardSocket[] sockets = row.GetComponentsInChildren<BreadboardSocket>();

            if (sockets.Length < 10)
                continue;

            // Left side A-E
            CreateNode(sockets, 0, 4);

            // Right side F-J
            CreateNode(sockets, 5, 9);

            Debug.Log("Built nodes for " + row.name);
        }

        Debug.Log("Total nodes created: " + nodes.Count);
    }

    void CreateNode(BreadboardSocket[] sockets, int start, int end)
    {
        BreadboardNode node = new BreadboardNode();

        for (int i = start; i <= end; i++)
        {
            sockets[i].node = node;
            node.sockets.Add(sockets[i]);
        }

        nodes.Add(node);

        Color color = Random.ColorHSV();

        foreach (var socket in node.sockets)
        {
            Renderer r = socket.GetComponentInChildren<Renderer>();
            if (r != null)
                r.material.color = color;
        }
    }
}