using System.Collections.Generic;

public class BreadboardNode
{
    public List<BreadboardSocket> sockets = new List<BreadboardSocket>();

    // Electrical simulation
    public float voltage = 0f;
    public bool isVoltageSource = false;
    public bool IsPowered => voltage != 0f;
}