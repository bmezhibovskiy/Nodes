using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class NodeGenerator2 : MonoBehaviour
{
    [SerializeField] private bool drawDebugVisualizations = true;
    [SerializeField] private Camera mainCamera;
    private static int numSideNodes = 37;
    private static int numNodes = numSideNodes * numSideNodes;

    private static float nodeDistance = 0.45f;
    private static Vector3 nodeOffset = Vector3.up * nodeDistance;
    private static float diagDistance = Mathf.Sqrt(2 * nodeDistance * nodeDistance);
    private static float maxDistance = nodeDistance * 2.0f;
    private static float sqrMaxDistance = maxDistance * maxDistance;
    public static float minDistance = nodeDistance * 0.2f;
    private static float compressionFactor = 1.0f;

    private List<GameObject> nodes = new List<GameObject>(numNodes);
    private GameObject ship;
    private List<Connection> connections = new List<Connection>();
    private VelocityField velocityField = new VelocityField();
    private SpatialHasher spatialHasher = new SpatialHasher(nodeDistance * 1.25f, nodeDistance * (float)numSideNodes * 1.25f);

    private const float maxFuel = 5000.0f;
    private float fuel = maxFuel;
    private const float maxShield = 100.0f;
    private float shield = maxShield;
    private const float maxSafeSpeed = 1.1f;

    private int numClosestNodes = 3;

    // Start is called before the first frame update
    void Start()
    {
        GenerateNodes();
        GenerateConnections();
        ship = AddObject(Vector3.zero);
        velocityField.AddFieldObject(new Vector3(2.5f, 2.5f, 0), 2, 1.4f, 0.6f);
    }

    private void GenerateNodes()
    {
        for (int i = 0; i < numNodes; i++)
        {
            int rawX = i % numSideNodes;
            int rawY = i / numSideNodes;
            float x = (float)(rawX - numSideNodes / 2) * nodeDistance;
            float y = (float)(rawY - numSideNodes / 2) * nodeDistance;
            AddNode(new Vector3(x, y, 0) + nodeOffset, IsUnmoving(rawX, rawY));
        }
    }

    private bool IsUnmoving(int rawX, int rawY)
    {
        return rawX == 0 || rawY == 0 || rawX == numSideNodes - 1 || rawY == numSideNodes - 1;
    }

    private void GenerateConnections()
    {
        foreach (GameObject unmovingNode in nodes)
        {
            if (!unmovingNode.GetComponent<PosNode>().isUnmoving) { continue; }

            List<GameObject> closest = ClosestNodes(unmovingNode.transform.position, true);
            foreach (GameObject closestNode in closest)
            {
                if (!closestNode.GetComponent<PosNode>().isUnmoving)
                {
                    float dist = Vector3.Distance(closestNode.transform.position, unmovingNode.transform.position);
                    connections.Add(new Connection(unmovingNode, closestNode, dist));
                    break;
                }
            }
        }
    }

    void Update()
    {
        UpdateMouseClick();

        UpdateNodes();

        UpdateConnections();

        UpdateShip();

        RemoveDeadNodes();

        UpdateFPSCounter();

        DebugDraw();
    }
    private void UpdateMouseClick()
    {
        if (!Input.GetMouseButtonDown(0)) { return; }

        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;
        GameObject closestFieldObj = velocityField.ClosestFieldObject(mousePos);

        if (closestFieldObj != null && (mousePos - closestFieldObj.transform.position).magnitude < closestFieldObj.GetComponent<FieldObject>().radius)
        {
            velocityField.RemoveFieldObject(closestFieldObj);
        }
        else
        {
            velocityField.AddFieldObject(mousePos, 2, 1.5f, 0.6f);
        }

    }
    private void UpdateNodes()
    {
        foreach (GameObject node in nodes)
        {
            node.GetComponent<PosNode>().SetVel(velocityField.velocityAt(node.transform.position));
            GameObject closest = velocityField.ClosestFieldObject(node.transform.position);
            if (closest != null)
            {
                FieldObject fieldObj = closest.GetComponent<FieldObject>();
                if ((closest.transform.position-node.transform.position).sqrMagnitude < fieldObj.radius * fieldObj.radius)
                {
                    node.GetComponent<PosNode>().isDead = true;
                    spatialHasher.RemoveObject(node);
                }
                else
                {
                    spatialHasher.UpdateObject(node);
                }
            }
        }
    }

    private void RemoveDeadNodes()
    {
        nodes.RemoveAll(x => x.GetComponent<PosNode>().isDead);
    }

    private void UpdateConnections()
    {
        for (int i = 0; i < connections.Count; ++i)
        {
            Connection c = connections[i];
            if ((c.a.transform.position - c.b.transform.position).sqrMagnitude > sqrMaxDistance)
            {
                CreateNodeInConnection(c);
                --i;
            }
        }

    }

    private void UpdateShip()
    {
        if (ship == null) { return; }

        float fspeed = 2.0f;
        float rspeed = 2.0f;

        PosObj posObj = ship.GetComponent<PosObj>();
        float totalThrust = 0;

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            posObj.Rotate(rspeed);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            posObj.Rotate(-rspeed);
        }
        if (fuel > 0)
        {
            if (Input.GetKey(KeyCode.UpArrow))
            {
                posObj.AddThrust(fspeed);
                totalThrust += fspeed;
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                posObj.AddThrust(-fspeed);
                totalThrust += fspeed;
            }
            if (Input.GetKeyDown(KeyCode.Space))
            {
                posObj.AddThrust(fspeed * 70.0f);
                totalThrust += fspeed * 70.0f;
            }
            fuel -= totalThrust;
        }

        Vector3 prevPosition = ship.transform.position;
        posObj.Integrate();

        GameObject closest = velocityField.ClosestFieldObject(ship.transform.position);
        if (closest != null)
        {
            FieldObject fieldObj = closest.GetComponent<FieldObject>();
            Vector3 dist = ship.transform.position - closest.transform.position;
            if (dist.magnitude < fieldObj.radius)
            {
                Vector3? intersection = LineSegmentCircleIntersection(closest.transform.position, fieldObj.radius, prevPosition, ship.transform.position);
                if (intersection != null)
                {
                    shield -= Mathf.Max(0, posObj.GetSpeed() - maxSafeSpeed) * 10.0f;
                    posObj.HandleCollisionAt(intersection.Value, (intersection.Value - closest.transform.position).normalized);
                }
            }
        }

        if (posObj.ShouldRebase())
        {
            posObj.Rebase(ClosestNodes(ship.transform.position));
        }

        mainCamera.transform.position = new Vector3(ship.transform.position.x, ship.transform.position.y, mainCamera.transform.position.z);

        if (shield < 0)
        {
            ship = null;
        }
    }

    private Vector3? LineSegmentCircleIntersection(Vector3 center, float r, Vector3 start, Vector3 end)
    {
        Vector3 d = end - start;
        Vector3 f = start - center;

        float e = 0.0001f;
        float a = Vector3.Dot(d, d);
        float b = 2 * Vector3.Dot(f, d);
        float c = Vector3.Dot(f, f) - r * r;

        //Solve using quadratic formula
        float discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
        {
            return null;
        }
        discriminant = Mathf.Sqrt(discriminant);
        float t1 = (-b - discriminant) / (2 * a);
        float t2 = (-b + discriminant) / (2 * a);
        if (t1 >= -e && t1 <= 1 + e)
        {
            return start + t1 * d;
        }

        //Some other strange intersection case where the start is inside or past the circle
        Vector3 dir = start - center;
        return center + dir.normalized * r;
    }
    private void CreateNodeInConnection(Connection c)
    {
        Vector3 newPos = c.Midpoint();
        GameObject newNode = AddNode(newPos);

        if (c.a.GetComponent<PosNode>().isUnmoving)
        {
            float distA = (c.a.transform.position - newPos).magnitude * compressionFactor;
            connections.Add(new Connection(c.a, newNode, distA));
        }
        if (c.b.GetComponent<PosNode>().isUnmoving)
        {
            float distB = (c.b.transform.position - newPos).magnitude * compressionFactor;
            connections.Add(new Connection(newNode, c.b, distB));
        }

        connections.Remove(c);
    }

    private List<GameObject> ClosestNodes(Vector3 pos, bool returnAll = false)
    {
        List<GameObject> sortedNodes;

        if (returnAll)
        {
            List<GameObject> prunedNodes = new List<GameObject>(nodes);
            prunedNodes.RemoveAll(x => x.GetComponent<PosNode>().isDead);
            sortedNodes = new List<GameObject>(prunedNodes);
        }
        else
        {
            sortedNodes = spatialHasher.ClosestObjects(pos, numClosestNodes);
        }

        sortedNodes.Sort((a, b) => (a.transform.position - pos).sqrMagnitude.CompareTo((b.transform.position - pos).sqrMagnitude));

        return returnAll ? sortedNodes : sortedNodes.Take(numClosestNodes).ToList();
    }

    private GameObject AddNode(Vector3 pos, bool isUnmoving = false)
    {
        GameObject newNode = new GameObject("GridNode");
        newNode.transform.position = pos;
        PosNode nodeComponent = newNode.AddComponent<PosNode>();
        nodeComponent.isUnmoving = isUnmoving;
        nodes.Add(newNode);
        spatialHasher.AddObject(newNode);
        return newNode;
    }

    private GameObject AddObject(Vector3 pos)
    {
        GameObject newObj = new GameObject("GridObject");
        newObj.transform.position = pos;
        PosObj posObj = newObj.AddComponent<PosObj>();
        posObj.prevPos = pos;
        List<GameObject> closestNodes = ClosestNodes(pos);
        posObj.Rebase(closestNodes);
        return newObj;
    }

    private void DebugDraw()
    {
        if (!drawDebugVisualizations) { return; }

        foreach (Connection c in connections)
        {
            Debug.DrawLine(c.a.transform.position, c.b.transform.position, new Color(0.35f, 0.35f, 0.35f, 1));
        }
        foreach (GameObject n in nodes)
        {
            n.GetComponent<PosNode>().DebugDraw();
        }
        if (ship != null)
        {
            ship.GetComponent<PosObj>().DebugDraw();
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
        Gizmos.color = new Color(0.7f, 0.5f, 0.1f, 0.5f);
        velocityField.DebugDrawGizmos();
        GUIStyle style = GUI.skin.label;
        style.fontSize = 6;
        Vector3 camPos = mainCamera.transform.position;
        Vector3 labelPos = new Vector3(camPos.x - 4.3f, camPos.y - 4.4f, 0);
        Handles.Label(labelPos, "Fuel: " + ((int)fuel).ToString() + "kg    Shield: " + ((int)shield).ToString() + "%    FPS:" + (int)(fps), style);
    }
}
