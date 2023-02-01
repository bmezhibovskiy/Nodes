using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using static UnityEditor.PlayerSettings;

class NodeConnection : IEquatable<NodeConnection>
{
    public GameObject a;
    public GameObject b;
    public NodeConnection(GameObject a, GameObject b)
    {
        this.a = a;
        this.b = b;
        Assert.AreNotEqual(a, b);
    }

    public bool Equals(NodeConnection other)
    {
        return a == other.a && b == other.b || a == other.b && b == other.a;
    }

    public Vector3 Midpoint()
    {
        return (a.transform.position + b.transform.position) * 0.5f;
    }
}
public class Sector: MonoBehaviour
{
    Camera mainCamera;

    string displayName;

    bool is3d;
    float sideLength;
    int numSideNodes;
    Vector3 startPos;
    GameObject[] sectorObjects;

    int numNodes;
    float nodeDistance;
    Vector3 nodeOffset;


    private const float spatialHasherSizeMultiplier = 1.25f;
    SpatialHasher spatialHasher;
    List<GameObject> nodes = new List<GameObject>();
    List<NodeConnection> connections = new List<NodeConnection>();

    GameObject playerShip;
    List<GameObject> otherShips = new List<GameObject>();

    private const int numClosestNodes = 4;

    private static GameObject[] GenerateSectorObjects(SectorObjectInfo[] infos)
    {
        return infos.Select(delegate(SectorObjectInfo info)
        {
            GameObject newSectorObject = new GameObject("SectorObject");
            newSectorObject.transform.position = info.position;
            SectorObject sectorObjectComponent = newSectorObject.AddComponent<SectorObject>();
            sectorObjectComponent.Initialize(info);
            return newSectorObject;
        }).ToArray();
    }

    public void Initialize(SectorInfo info, Camera mainCamera)
    {
        Initialize(info.is3d, info.sideLength, info.sideNodes, info.startPosition, GenerateSectorObjects(info.sectorObjectInfos), mainCamera);
    }
    public void Initialize(bool is3d, float sideLength, int numSideNodes, Vector3 startPos, GameObject[] sectorObjects, Camera mainCamera)
    {
        this.is3d = is3d;
        this.sideLength = sideLength;
        this.numSideNodes = numSideNodes;
        this.startPos = startPos;
        this.sectorObjects = sectorObjects;
        this.mainCamera = mainCamera;

        mainCamera.orthographic = !is3d;

        this.numNodes = is3d ? numSideNodes * numSideNodes * numSideNodes : numSideNodes * numSideNodes;
        this.nodeDistance = sideLength / (float)numSideNodes;
        this.nodeOffset = Vector3.up * nodeDistance;

        spatialHasher = new SpatialHasher(nodeDistance * spatialHasherSizeMultiplier, sideLength * spatialHasherSizeMultiplier, is3d);

        GenerateNodes();
        GenerateConnections();
        playerShip = AddShip(startPos);
    }

    private void GenerateNodes()
    {
        for (int i = 0; i < numNodes; i++)
        {
            int[] raw = is3d ? Utils.to3D(i, numSideNodes) : Utils.to2D(i, numSideNodes);
            float x = (float)(raw[0] - numSideNodes / 2) * nodeDistance;
            float y = (float)(raw[1] - numSideNodes / 2) * nodeDistance;
            if (is3d)
            {
                float z = (float)(raw[2] - numSideNodes / 2) * nodeDistance;
                AddNode(new Vector3(x, y, z) + nodeOffset, IsBorder(raw));
            }
            else
            {
                AddNode(new Vector3(x, y, 0) + nodeOffset, IsBorder(raw));
            }
        }
    }

    private bool IsBorder(int[] raw)
    {
        foreach (int i in raw)
        {
            if (i == 0 || i == numSideNodes - 1)
            {
                return true;
            }
        }
        return false;
    }

    private GameObject AddNode(Vector3 pos, bool isBorder = false)
    {
        GameObject newNode = new GameObject("GridNode");
        newNode.transform.position = pos;
        GridNode nodeComponent = newNode.AddComponent<GridNode>();
        nodeComponent.isBorder = isBorder;
        nodes.Add(newNode);
        spatialHasher.AddObject(newNode);
        return newNode;
    }

    private void GenerateConnections()
    {
        foreach (GameObject borderNode in nodes)
        {
            if (!borderNode.GetComponent<GridNode>().isBorder) { continue; }

            List<GameObject> closest = ClosestNodes(borderNode.transform.position, true);
            foreach (GameObject closestNode in closest)
            {
                if (!closestNode.GetComponent<GridNode>().isBorder)
                {
                    connections.Add(new NodeConnection(borderNode, closestNode));
                    break;
                }
            }
        }
    }
    private List<GameObject> ClosestNodes(Vector3 pos, bool returnAll = false)
    {
        List<GameObject> sortedNodes;

        if (returnAll)
        {
            List<GameObject> prunedNodes = new List<GameObject>(nodes);
            prunedNodes.RemoveAll(x => x.GetComponent<GridNode>().isDead);
            sortedNodes = new List<GameObject>(prunedNodes);
        }
        else
        {
            sortedNodes = spatialHasher.ClosestObjects(pos, numClosestNodes);
        }

        sortedNodes.Sort((a, b) => (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));

        return returnAll ? sortedNodes : sortedNodes.Take(numClosestNodes).ToList();
    }

    private GameObject AddShip(Vector3 pos)
    {
        GameObject newObj = new GameObject("Ship");
        newObj.transform.position = pos;
        Ship ship = newObj.AddComponent<Ship>();
        ship.prevPos = pos;
        List<GameObject> closestNodes = ClosestNodes(pos);
        ship.Rebase(closestNodes);
        return newObj;
    }
    void Update()
    {
        UpdateNodes();

        UpdateConnections();

        UpdateOtherShips();

        UpdatePlayerShip();

        UpdateCamera();

        UpdateFPSCounter();

        DebugDraw();
    }
    private void UpdateNodes()
    {
        foreach (GameObject node in nodes)
        {
            GridNode gridNode = node.GetComponent<GridNode>();
            gridNode.ResetForUpdate();
            foreach(GameObject sectorObject in sectorObjects)
            {
                sectorObject.GetComponent<SectorObject>().UpdateGridNode(gridNode);
            }

            GameObject closest = ClosestSectorObject(node.transform.position);
            if (closest != null)
            {
                SectorObject sectorObject = closest.GetComponent<SectorObject>();
                if ((closest.transform.position - node.transform.position).sqrMagnitude < sectorObject.size * sectorObject.size)
                {
                    gridNode.isDead = true;
                    spatialHasher.RemoveObject(node);
                }
                else
                {
                    spatialHasher.UpdateObject(node);
                }
            }
        }
        RemoveDeadNodes();
    }
    
    public GameObject ClosestSectorObject(Vector3 pos)
    {
        float distance = float.MaxValue;
        GameObject current = null;
        foreach (GameObject go in sectorObjects)
        {
            float currentDistance = (go.transform.position - pos).sqrMagnitude;
            if (currentDistance < distance)
            {
                distance = currentDistance;
                current = go;
            }
        }
        return current;
    }

    private void RemoveDeadNodes()
    {
        nodes.RemoveAll(x => x.GetComponent<GridNode>().isDead);
    }

    private void UpdateConnections()
    {
        float maxConnectionLength = 2f * nodeDistance;
        float sqrMaxConnectionLength = maxConnectionLength * maxConnectionLength;
        for (int i = 0; i < connections.Count; ++i)
        {
            NodeConnection c = connections[i];
            if ((c.a.transform.position - c.b.transform.position).sqrMagnitude > sqrMaxConnectionLength)
            {
                CreateNodeInConnection(c);
                --i;
            }
        }
    }
    private void CreateNodeInConnection(NodeConnection c)
    {
        Vector3 newPos = c.Midpoint();
        GameObject newNode = AddNode(newPos);

        if (c.a.GetComponent<GridNode>().isBorder)
        {
            connections.Add(new NodeConnection(c.a, newNode));
        }
        if (c.b.GetComponent<GridNode>().isBorder)
        {
            connections.Add(new NodeConnection(newNode, c.b));
        }

        connections.Remove(c);
    }
    private void UpdatePlayerShip()
    {
        if (playerShip == null) { return; }

        float fspeed = 2.0f;
        float rspeed = 2.0f;

        Ship ship = playerShip.GetComponent<Ship>();
        if (ship == null) { return; }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            ship.Rotate(rspeed);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            ship.Rotate(-rspeed);
        }
        if (Input.GetKey(KeyCode.UpArrow))
        {
            ship.AddThrust(fspeed);
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            ship.AddThrust(-fspeed);
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ship.AddThrust(fspeed * 50.0f);
        }

        IntegrateCollideAndRebaseShip(playerShip);
    }

    private void UpdateOtherShips()
    {
        foreach(GameObject gameObject in otherShips)
        {
            //TODO: Set rotation and pull with AI
            IntegrateCollideAndRebaseShip(gameObject);
        }
    }

    private void IntegrateCollideAndRebaseShip(GameObject gameObject)
    {
        Ship ship = gameObject.GetComponent<Ship>();
        if (ship == null) { return; }

        Vector3 prevPosition = gameObject.transform.position;
        ship.Integrate();
        Vector3 currentPosition = gameObject.transform.position;
        Vector3 postCollisionPosition = currentPosition;

        //Collide with sector objects
        GameObject closest = ClosestSectorObject(currentPosition);
        if (closest != null)
        {
            Vector3 closestPosition = closest.transform.position;
            SectorObject sectorObject = closest.GetComponent<SectorObject>();
            Vector3 dist = currentPosition - closestPosition;
            if (dist.magnitude < sectorObject.size + ship.size)
            {
                Vector3? intersection = Utils.LineSegmentCircleIntersection(closestPosition, sectorObject.size + ship.size, prevPosition, currentPosition);
                if (intersection != null)
                {
                    ship.HandleCollisionAt(intersection.Value, (intersection.Value - closestPosition).normalized);
                    postCollisionPosition = gameObject.transform.position;
                }
            }
        }
        //

        //Collide with other ships
        foreach (GameObject otherGameObject in otherShips)
        {
            if(otherGameObject == gameObject) { continue; }
            Ship otherShip = otherGameObject.GetComponent<Ship>();
            if (otherShip == null) { continue; }
            Vector3 otherShipPos = otherGameObject.transform.position;

            Vector3 dist = postCollisionPosition - otherShipPos;
            if (dist.magnitude < otherShip.size + ship.size)
            {
                Vector3? intersection = Utils.LineSegmentCircleIntersection(otherShipPos, otherShip.size + ship.size, prevPosition, postCollisionPosition);
                if (intersection != null)
                {
                    //TODO: have the smaller ship handle the collision, or some type of momentum transfer?
                    ship.HandleCollisionAt(intersection.Value, (intersection.Value - otherShipPos).normalized);
                    postCollisionPosition = gameObject.transform.position;
                }
            }
        }
        //

        if (ship.ShouldRebase())
        {
            ship.Rebase(ClosestNodes(postCollisionPosition));
        }
    }

    private void UpdateCamera()
    {
        if (playerShip == null || mainCamera == null) { return; }
        mainCamera.transform.position = new Vector3(playerShip.transform.position.x, playerShip.transform.position.y, mainCamera.transform.position.z);
    }

    private void DebugDraw()
    {
        foreach (NodeConnection c in connections)
        {
            Debug.DrawLine(c.a.transform.position, c.b.transform.position, new Color(0.35f, 0.35f, 0.35f, 1));
        }
        foreach (GameObject n in nodes)
        {
            n.GetComponent<GridNode>().DebugDraw();
        }
        if (playerShip != null)
        {
            playerShip.GetComponent<Ship>().DebugDraw();
        }
    }
    private const int maxFpsHistoryCount = 30;
    private List<float> fpsHistory = new List<float>();
    private float fps = 0;
    private void UpdateFPSCounter()
    {
        fpsHistory.Add(1f / Time.deltaTime);
        if (fpsHistory.Count > maxFpsHistoryCount) { fpsHistory.RemoveAt(0); }
        if (Time.frameCount % maxFpsHistoryCount == 0)
        {
            float total = 0;
            foreach (float f in fpsHistory)
            {
                total += f;
            }
            fps = total / fpsHistory.Count;
        }
    }

    private void OnDrawGizmos()
    {
        foreach (GameObject go in sectorObjects)
        {
            SectorObject sectorObject = go.GetComponent<SectorObject>();
            Gizmos.DrawSphere(go.transform.position, sectorObject.size);
        }

        Gizmos.color = new Color(0.7f, 0.5f, 0.1f, 0.5f);
        GUIStyle style = GUI.skin.label;
        style.fontSize = 6;
        Vector3 camPos = mainCamera.transform.position;
        Vector3 labelPos = new Vector3(camPos.x - 4.3f, camPos.y - 4.4f, 0);
        Handles.Label(labelPos, "FPS:" + (int)(fps), style);
    }
}
