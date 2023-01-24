using JetBrains.Annotations;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;


public class FieldObject: MonoBehaviour
{
    public enum Type { LinearPuller, LinearPusher, SquarePuller, SquarePusher }
    public Type type = Type.LinearPuller;
    public float strength = 0f;
    public float radius = 0f;
    public bool isPuller() { return type == Type.LinearPuller || type == Type.SquarePuller; }
    public bool isLinear() { return type == Type.LinearPuller || type == Type.LinearPusher; }
}
public class VelocityField
{
    List<GameObject> fieldObjects = new List<GameObject>();
    public GameObject AddFieldObject(FieldObject.Type type, Vector3 pos, float strength, float radius)
    {
        GameObject newFObj = new GameObject("Empty");
        newFObj.transform.position = pos;

        FieldObject fObjComponent = newFObj.AddComponent<FieldObject>();
        fObjComponent.type = type;
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
            float strength = fObj.strength * (fObj.isPuller() ? 1f : -1f);
            totalVelocity += (strength / (fObj.isLinear() ? dir.magnitude : dir.sqrMagnitude)) * dir.normalized;
            
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
    private static int numSideNodes = 27;
    private static int numNodes = numSideNodes * numSideNodes;

    private static float nodeDistance = 0.4f;
    private static float diagDistance = Mathf.Sqrt(2 * nodeDistance * nodeDistance);
    private static float maxDistance = nodeDistance * 1.8f;
    public static float minDistance = nodeDistance * 0.2f;
    private static float compressionFactor = 1.0f;

    //private int numRecentlyDeletedNodes = 0;
    private List<GameObject> nodes = new List<GameObject>(numNodes);
    private List<GameObject> objects = new List<GameObject>();
    private List<Connection> connections = new List<Connection>();
    private VelocityField velocityField = new VelocityField();

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
        velocityField.AddFieldObject(FieldObject.Type.LinearPuller, new Vector3(0.92f, 0.91f, 0), 1.5f, 0.6f);
        velocityField.AddFieldObject(FieldObject.Type.LinearPuller, new Vector3(-0.92f, -0.91f, 0), 1.5f, 0.6f);
    }

    private void GenerateNodes()
    {
        for (int i = 0; i < numNodes; i++)
        {
            int rawX = i % numSideNodes;
            int rawY = i / numSideNodes;
            float x = (float)(rawX - numSideNodes / 2) * nodeDistance;
            float y = (float)(rawY - numSideNodes / 2) * nodeDistance;
            AddNode(new Vector3(x, y, 0), IsUnmoving(rawX, rawY));
        }
    }

    private bool IsUnmoving(int rawX, int rawY)
    {
        return rawX == 0 || rawY == 0 || rawX == numSideNodes - 1 || rawY == numSideNodes - 1;
    }

    // Update is called once per frame
    private int mouseDownFrames = 0;
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            if(mouseDownFrames == 0)
            {
                AddObject(mousePos);
            }
            ++mouseDownFrames;
        }
        else
        {
            mouseDownFrames = 0;
        }

        foreach (GameObject node in nodes)
        {
            node.GetComponent<PosNode>().SetVel(velocityField.velocityAt(node.transform.position));
            GameObject closest = velocityField.ClosestFieldObject(node.transform.position);
            FieldObject fieldObj = closest.GetComponent<FieldObject>();
            if (Vector3.Distance(closest.transform.position,node.transform.position) < fieldObj.radius)
            {
                node.GetComponent<PosNode>().isDead = true;
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

        float fspeed = 1.1f;
        float rspeed = 2.1f;
        foreach (GameObject obj in objects)
        {
            PosObj posObj = obj.GetComponent<PosObj>();

            if (Input.GetKey(KeyCode.UpArrow))
            {
                posObj.AddThrust(fspeed);
            }
            if (Input.GetKey(KeyCode.DownArrow))
            {
                posObj.AddThrust(-fspeed);
            }
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                posObj.Rotate(rspeed);
            }
            if (Input.GetKey(KeyCode.RightArrow))
            {
                posObj.Rotate(-rspeed);
            }

            Vector3 prevPosition = obj.transform.position;
            Vector3 nextGridPos = posObj.CalculatePosition();
            bool shouldRebase = false;

            GameObject closest = velocityField.ClosestFieldObject(nextGridPos);
            FieldObject fieldObj = closest.GetComponent<FieldObject>();
            Vector3 dist = nextGridPos - closest.transform.position;
            if (dist.magnitude < fieldObj.radius)
            {
                Vector3? intersection = LineSegmentCircleIntersection(closest.transform.position, fieldObj.radius, prevPosition, nextGridPos);
                if (intersection != null)
                {
                    nextGridPos = intersection.Value;
                    shouldRebase = true;
                }
            }

            Vector3 gridMovement = nextGridPos - prevPosition;
            posObj.Integrate(gridMovement);
            shouldRebase |= posObj.ShouldRebase();
            
            if (shouldRebase)
            {
                posObj.Rebase(ClosestNodes(obj.transform.position));
            }
        }

        nodes.RemoveAll(x => x.GetComponent<PosNode>().isDead);

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
        List<GameObject> prunedNodes = new List<GameObject>(nodes);
        prunedNodes.RemoveAll(x => x.GetComponent<PosNode>().isDead);
        List<GameObject> sortedNodes = new List<GameObject>(prunedNodes);
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
        GameObject newNode = new GameObject("Empty");
        newNode.transform.position = pos;
        PosNode nodeComponent = newNode.AddComponent<PosNode>();
        nodeComponent.isUnmoving = isUnmoving;
        nodes.Add(newNode);
        return newNode;
    }

    private GameObject AddObject(Vector3 pos)
    {
        GameObject newObj = new GameObject("Empty");
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
            Debug.DrawLine(c.a.transform.position, c.b.transform.position, Color.gray);
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

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        velocityField.DebugDrawGizmos();
    }
}
