using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using static UnityEditor.PlayerSettings;

public class SectorConnection : IEquatable<SectorConnection>
{
    public int id1, id2;

    public bool Equals(SectorConnection other)
    {
        return id1 == other.id1 && id2 == other.id2 || id1 == other.id2 && id2 == other.id1;
    }
}

public class Map: MonoBehaviour
{
    HashSet<SectorConnection> connections = new HashSet<SectorConnection>();
    GameObject currentSector;

    private static string infoFilename = "Map1.json";
    private MapInfo mapInfo;

    private Camera mainCamera;

    public void Instantiate(Camera mainCamera)
    {
        this.mainCamera = mainCamera;
        this.mapInfo = MapInfo.fromJsonFile(infoFilename);
        Assert.IsTrue(mapInfo.sectorInfos.Length > 0);
        LoadSector(0);
    }

    public void LoadSector(int sectorIndex)
    {
        SectorInfo sectorInfo = mapInfo.sectorInfos[sectorIndex];
        GameObject newSector = new GameObject("Sector "+sectorIndex.ToString());
        Sector sectorComponent = newSector.AddComponent<Sector>();
        sectorComponent.Initialize(sectorInfo, mainCamera);
        currentSector = newSector;
    }


}
