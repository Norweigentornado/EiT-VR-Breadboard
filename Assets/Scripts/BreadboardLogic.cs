using UnityEngine;
using System.Collections.Generic;

public class BreadboardLogic : MonoBehaviour
{
    List<BreadboardNode> nodes = new List<BreadboardNode>();

    void Awake()
    {
        BuildBreadboard();
    }

    void BuildBreadboard(){
        foreach (Transform row in transform)
        {
            if (!row.name.Contains("Row"))
                continue;

            BreadboardSocket[] sockets = row.GetComponentsInChildren<BreadboardSocket>();

            if (sockets.Length < 14)
                continue;

            // Sort sockets left → right
            System.Array.Sort(sockets, (a, b) =>
                a.transform.position.x.CompareTo(b.transform.position.x));

            /*
            Typical order on your board:

            0  = left power -
            1  = left power +

            2-6   = A B C D E
            7-11  = F G H I J

            12 = right power +
            13 = right power -
            */

            CreateNode(sockets, 2, 6);   // A-E
            CreateNode(sockets, 7, 11);  // F-J

            Debug.Log("Built row nodes for " + row.name);
        }

        BuildPowerRails();
    }

    void BuildPowerRails(){
        List<BreadboardSocket> leftPlus = new List<BreadboardSocket>();
        List<BreadboardSocket> leftMinus = new List<BreadboardSocket>();

        List<BreadboardSocket> rightPlus = new List<BreadboardSocket>();
        List<BreadboardSocket> rightMinus = new List<BreadboardSocket>();

        foreach (Transform row in transform)
        {
            if (!row.name.Contains("Row"))
                continue;

            BreadboardSocket[] sockets = row.GetComponentsInChildren<BreadboardSocket>();

            System.Array.Sort(sockets, (a, b) =>
                a.transform.position.x.CompareTo(b.transform.position.x));

            leftMinus.Add(sockets[0]);
            leftPlus.Add(sockets[1]);

            rightPlus.Add(sockets[12]);
            rightMinus.Add(sockets[13]);
        }

        CreateVerticalNode(leftMinus);
        CreateVerticalNode(leftPlus);
        CreateVerticalNode(rightPlus);
        CreateVerticalNode(rightMinus);
    }

    void CreateVerticalNode(List<BreadboardSocket> sockets)
    {
        BreadboardNode node = new BreadboardNode();

        foreach (var socket in sockets)
        {
            socket.node = node;
            node.sockets.Add(socket);
        }

        nodes.Add(node);
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
    }

/*
    void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0) return;

        foreach (var node in nodes)
        {
            if (node.sockets.Count == 0) continue;

            Gizmos.color = Color.yellow;

            Vector3 center = node.sockets[0].transform.position;

            foreach (var socket in node.sockets)
            {
                Gizmos.DrawSphere(socket.transform.position, 0.004f);
                Gizmos.DrawLine(center, socket.transform.position);
            }
        }
    }
*/
}