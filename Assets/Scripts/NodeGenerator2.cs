using JetBrains.Annotations;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;


public class FieldObject : MonoBehaviour
{
    public int dimension = 2;
    public float strength = 0f;
    public float radius = 0f;
}
public class VelocityField
{
    List<GameObject> fieldObjects = new List<GameObject>();
    public GameObject AddFieldObject(Vector3 pos, int dimension, float strength, float radius)
    {
        GameObject newFObj = new GameObject("FieldObject");
        newFObj.transform.position = pos;

        FieldObject fObjComponent = newFObj.AddComponent<FieldObject>();
        fObjComponent.dimension = dimension;
        fObjComponent.strength = strength;
        fObjComponent.radius = radius;

        fieldObjects.Add(newFObj);
        return newFObj;
    }

    public Vector3 velocityAt(Vector3 position)
    {
        Vector3 totalVelocity = new Vector3();
        foreach (GameObject go in fieldObjects)
        {
            FieldObject fObj = go.GetComponent<FieldObject>();
            Vector3 dir = go.transform.position - position;
            float strength = fObj.strength;
            //Inverse r squared law generalizes to inverse r^(dim-1)
            float denom = Mathf.Pow(dir.magnitude, (float)(fObj.dimension-1));
            totalVelocity += (strength / denom) * dir.normalized;
            
        }
        return totalVelocity;
    }

    public GameObject ClosestFieldObject(Vector3 pos)
    {
        float distance = float.MaxValue;
        GameObject current = null;
        foreach (GameObject go in fieldObjects)
        {
            float currentDistance = Vector3.Distance(go.transform.position, pos);
            if(currentDistance < distance)
            {
                distance = currentDistance;
                current = go;
            }
        }
        return current;
    }

    public void DebugDrawGizmos()
    {
        foreach (GameObject go in fieldObjects)
        {
            FieldObject fieldObj = go.GetComponent<FieldObject>();
            Gizmos.DrawSphere(go.transform.position, fieldObj.radius);
        }
    }
}

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
    public static float minDistance = nodeDistance * 0.2f;
    private static float compressionFactor = 1.0f;

    //private int numRecentlyDeletedNodes = 0;
    private List<GameObject> nodes = new List<GameObject>(numNodes);
    private List<GameObject> objects = new List<GameObject>();
    private List<Connection> connections = new List<Connection>();
    private VelocityField velocityField = new VelocityField();
    private SpatialHasher spatialHasher = new SpatialHasher();

    private const float maxFuel = 4000.0f;
    private float fuel = maxFuel;
    private const float maxShield = 100.0f;
    private float shield = maxShield;
    private const float maxSafeSpeed = 1.1f;

    // Start is called before the first frame update
    void Start()
    {
        GenerateNodes();
        foreach (GameObject unmovingNode in nodes)
        {
            if (!unmovingNode.GetComponent<PosNode>().isUnmoving) { continue; }

            List<GameObject> closest = ClosestNodes(unmovingNode.transform.position);
            foreach(GameObject closestNode in closest)
            {
                if(!closestNode.GetComponent<PosNode>().isUnmoving)
                {
                    float dist = Vector3.Distance(closestNode.transform.position, unmovingNode.transform.position);
                    connections.Add(new Connection(unmovingNode, closestNode, dist));
                    break;
                }
            }
        }
        //velocityField.AddFieldObject(new Vector3(1.72f, 1.71f, 0), 2, 1.5f, 0.6f);
        //velocityField.AddFieldObject(new Vector3(-1.72f, -1.71f, 0), 2, 1.5f, 0.6f);
        AddObject(Vector3.zero);
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

    void Update()
    {
        if(Input.GetMouseButtonDown(0))
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            velocityField.AddFieldObject(mousePos, 2, 1.5f, 0.6f);
        }

        foreach (GameObject node in nodes)
        {
            node.GetComponent<PosNode>().SetVel(velocityField.velocityAt(node.transform.position));
            GameObject closest = velocityField.ClosestFieldObject(node.transform.position);
            if (closest != null)
            {
                FieldObject fieldObj = closest.GetComponent<FieldObject>();
                if (Vector3.Distance(closest.transform.position, node.transform.position) < fieldObj.radius)
                {
                    node.GetComponent<PosNode>().isDead = true;
                    spatialHasher.RemoveObject(node);
                }
                else
                {
                    spatialHasher.Update(node);
                }
            }
        }

        int connectionIndex = 0;
        while (connectionIndex < connections.Count)
        {
            for (connectionIndex = 0; connectionIndex < connections.Count; ++connectionIndex)
            {
                Connection c = connections[connectionIndex];
                if (Vector3.Distance(c.a.transform.position, c.b.transform.position) > maxDistance)
                {
                    CreateNodeInConnection(c);
                    break;
                }
            }
        }

        float fspeed = 2.0f;
        float rspeed = 2.0f;
        foreach (GameObject obj in objects)
        {
            PosObj posObj = obj.GetComponent<PosObj>();
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

            Vector3 prevPosition = obj.transform.position;
            Vector3 nextGridPos = posObj.CalculatePosition();

            Vector3 gridMovement = nextGridPos - prevPosition;
            posObj.Integrate(gridMovement);


            GameObject closest = velocityField.ClosestFieldObject(obj.transform.position);
            if (closest != null)
            {
                FieldObject fieldObj = closest.GetComponent<FieldObject>();
                Vector3 dist = obj.transform.position - closest.transform.position;
                if (dist.magnitude < fieldObj.radius)
                {
                    Vector3? intersection = LineSegmentCircleIntersection(closest.transform.position, fieldObj.radius, prevPosition, obj.transform.position);
                    if (intersection != null)
                    {
                        shield -= Mathf.Max(0, posObj.GetSpeed() - maxSafeSpeed) * 10.0f;
                        posObj.HandleCollisionAt(intersection.Value);
                    }
                }
            }
            
            if (posObj.ShouldRebase())
            {
                List<GameObject> closestNodes = ClosestNodes(obj.transform.position);
                if (closestNodes.Count > 2)
                {
                    posObj.Rebase(closestNodes);
                }
            }
        }

        nodes.RemoveAll(x => x.GetComponent<PosNode>().isDead);
        if (shield < 0)
        {
            objects.Clear();
        }
        UpdateFPSCounter();

        if (objects.Count > 0)
        {
            mainCamera.transform.position = new Vector3(objects[0].transform.position.x, objects[0].transform.position.y, mainCamera.transform.position.z);
        }
        DebugDraw();
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

        if(discriminant < 0)
        {
            return null;
        }
        discriminant = Mathf.Sqrt(discriminant);
        float t1 = (-b - discriminant) / (2 * a);
        float t2 = (-b + discriminant) / (2 * a);
        if (t1 >= -e && t1 <= 1+e)
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

        if(c.a.GetComponent<PosNode>().isUnmoving)
        {
            float distA = (c.a.transform.position - newPos).magnitude * compressionFactor;
            connections.Add(new Connection(c.a, newNode, distA));
        }
        if(c.b.GetComponent<PosNode>().isUnmoving)
        {
            float distB = (c.b.transform.position - newPos).magnitude * compressionFactor;
            connections.Add(new Connection(newNode, c.b, distB));
        }

        connections.Remove(c);
    }

    private List<GameObject> ClosestNodes(Vector3 pos)
    {
        List<GameObject> sortedNodes;

        //Not exactly sure why I need to skip the first two frames
        //But if spacialHasher.ClosestObjects second parameter is less than 5, it messes up some connections
        //But only in the first two frames.
        if (Input.GetKey(KeyCode.LeftAlt) || Time.frameCount < 2)
        {
            List<GameObject> prunedNodes = new List<GameObject>(nodes);
            prunedNodes.RemoveAll(x => x.GetComponent<PosNode>().isDead);
            sortedNodes = new List<GameObject>(prunedNodes);
            
        }
        else
        {
            sortedNodes = spatialHasher.ClosestObjects(pos, 3);
        }

        sortedNodes.Sort((a, b) =>
        {
            float mA = (a.transform.position - pos).magnitude;
            float mB = (b.transform.position - pos).magnitude;
            return mA.CompareTo(mB);
        });
        return sortedNodes;
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
        objects.Add(newObj);
        return newObj;
    }

    private void DebugDraw()
    {
        if (!drawDebugVisualizations) { return; }

        foreach (Connection c in connections)
        {
            Debug.DrawLine(c.a.transform.position, c.b.transform.position, new Color(0.35f,0.35f,0.35f,1));
        }
        foreach (GameObject n in nodes)
        {
            n.GetComponent<PosNode>().DebugDraw();
        }
        
        foreach (GameObject o in objects)
        {
            o.GetComponent<PosObj>().DebugDraw();
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
        Vector3 labelPos = new Vector3(camPos.x-4.3f, camPos.y-4.4f, 0);
        Handles.Label(labelPos, "Fuel: " + ((int)fuel).ToString() + "kg    Shield: " + ((int)shield).ToString() + "%    FPS:" + (int)(fps), style);
    }
}
