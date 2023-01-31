using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Utils
{
    public static int to1D(int x, int y, int z, int numSideNodes)
    {
        return (z * numSideNodes * numSideNodes) + (y * numSideNodes) + x;
    }

    public static int[] to3D(int idx, int numSideNodes)
    {
        int z = idx / (numSideNodes * numSideNodes);
        idx -= (z * numSideNodes * numSideNodes);
        int y = idx / numSideNodes;
        int x = idx % numSideNodes;
        return new int[] { x, y, z };
    }

    public static int to1D(int x, int y, int numSideNodes)
    {
        return (y * numSideNodes) + x;
    }

    public static int[] to2D(int idx, int numSideNodes)
    {
        int y = idx / numSideNodes;
        int x = idx % numSideNodes;
        return new int[] { x, y };
    }
}
