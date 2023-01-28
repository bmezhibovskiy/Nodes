using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

class Connection
{
    public GameObject a;
    public GameObject b;
    public float length;
    public float stiffness;
    public Connection(GameObject a, GameObject b, float length, float stiffness = 0.1f)
    {
        this.a = a;
        this.b = b;
        Assert.AreNotEqual(a, b);
        this.length = length;
        this.stiffness = stiffness;
    }
    public Vector3 Midpoint()
    {
        return (a.transform.position + b.transform.position) * 0.5f;
    }
}

public class NodeGenerator : MonoBehaviour
{
    [SerializeField] private bool drawDebugVisualizations = true;
    [SerializeField] private Camera mainCamera;
    private static int numSideNodes = 17;
    private static int numNodes = numSideNodes * numSideNodes;

    //Just a square grid:
    private static int numConnections = 2 * (numSideNodes * (numSideNodes - 1)) + 4; //+4 for the 4 diagonal connections at the corners

    //Diagonal only:
    //private static int numConnections = 2 * (numSideNodes - 1) * (numSideNodes - 1);

    //Square grid plus cross connections
    //private static int numConnections = 2*(numSideNodes*(numSideNodes-1)) + 2*(numSideNodes-1)*(numSideNodes-1);

    private static float nodeDistance = 1.0f;
    private static float diagDistance = Mathf.Sqrt(2 * nodeDistance * nodeDistance);
    private static float maxDistance = nodeDistance * 1.8f;
    private static float minDistance = nodeDistance * 0.2f;
    private static float compressionFactor = 1.0f;

    //private int numRecentlyDeletedNodes = 0;
    private List<GameObject> nodes = new List<GameObject>(numNodes);
    private List<Connection> connections = new List<Connection>(numConnections);
    private List<GameObject> objects = new List<GameObject>();

    // Start is called before the first frame update
    void Start()
    {
        GenerateNodes();
        GenerateConnections();
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
            AddNode(new Vector3(x, y, 0), IsUnmoving(rawX, rawY));
        }
    }

    private bool IsUnmoving(int rawX, int rawY)
    {
        return rawX == 0 || rawY == 0 || rawX == numSideNodes - 1 || rawY == numSideNodes - 1;
    }

    private float Stiffness(GameObject a, GameObject b)
    {
        if (a.GetComponent<PosNode>().isUnmoving || b.GetComponent<PosNode>().isUnmoving)
        {
            return 0.05f;
        }
        return 0.1f;
    }
    private void GenerateConnections()
    {
        connections.Add(new Connection(nodes[0], nodes[numSideNodes + 1], diagDistance * compressionFactor));
        connections.Add(new Connection(nodes[numSideNodes - 1], nodes[(numSideNodes) * 2 - 2], diagDistance * compressionFactor));
        connections.Add(new Connection(nodes[numSideNodes * (numSideNodes - 1)], nodes[(numSideNodes - 1) * (numSideNodes - 1)], diagDistance * compressionFactor));
        connections.Add(new Connection(nodes[numSideNodes * numSideNodes - 1], nodes[numSideNodes * (numSideNodes - 1) - 2], diagDistance * compressionFactor));

        for (int i = 0; i < numSideNodes; ++i)
        {
            for (int j = 0; j < numSideNodes - 1; ++j)
            {
                /**/
                //horizontal
                GameObject hNodeA = nodes[i * numSideNodes + j];
                GameObject hNodeB = nodes[i * numSideNodes + j + 1];
                connections.Add(new Connection(hNodeA, hNodeB, nodeDistance * compressionFactor, Stiffness(hNodeA, hNodeB)));

                //vertical
                GameObject vNodeA = nodes[i + j * numSideNodes];
                GameObject vNodeB = nodes[i + j * numSideNodes + numSideNodes];
                connections.Add(new Connection(vNodeA, vNodeB, nodeDistance * compressionFactor, Stiffness(vNodeA, vNodeB)));
                /**/
                //diagonal
                /*
                if (i < numSideNodes - 1 && j < numSideNodes - 1)
                {
                    GameObject dNodeA = nodes[i * numSideNodes + j];
                    GameObject dNodeB = nodes[(i + 1) * numSideNodes + j + 1];
                    connections.Add(new Connection(dNodeA, dNodeB, diagDistance * compressionFactor, Stiffness(dNodeA, dNodeB)));

                    GameObject dNodeC = nodes[i * numSideNodes + j + 1];
                    GameObject dNodeD = nodes[(i + 1) * numSideNodes + j];
                    connections.Add(new Connection(dNodeC, dNodeD, diagDistance * compressionFactor, Stiffness(dNodeC, dNodeD)));
                }
                /**/
            }
        }
    }

    private void RegenerateConnections()
    {
        connections = new List<Connection>();
        foreach (GameObject node in nodes)
        {
            List<GameObject> closestNodes = ClosestNodes(node.transform.position);
            for (int i = 1; i < 5; ++i)
            {
                if (!ConnectionExists(node, closestNodes[i]))
                {
                    connections.Add(new Connection(node, closestNodes[i], (node.transform.position - closestNodes[i].transform.position).magnitude));
                }
            }
        }
    }

    // Update is called once per frame
    private int mouseDownFrames = 0;
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            ++mouseDownFrames;
            List<GameObject> closest = nodes;//ClosestNodes(mousePos);

            for (int i = 0; i < closest.Count; ++i)
            {
                Vector3 dir = (mousePos - closest[i].transform.position);
                if (dir.sqrMagnitude > 0.05f)
                {
                    if (dir.sqrMagnitude < 1.8 * 1.8 * nodeDistance * nodeDistance)
                    {
                        float mag = Mathf.Log((float)mouseDownFrames) * 0.005f / dir.sqrMagnitude;
                        closest[i].GetComponent<PosNode>().AddPull(dir.normalized * mag);
                    }
                }
                else
                {
                    //closest[i].GetComponent<PosNode>().isDead = true;
                }
            }
        }
        else
        {
            mouseDownFrames = 0;
        }

        UpdatePulls();
        //UpdateNodes();
        //UpdateConnections();

        float fspeed = 0.001f;
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

            posObj.Integrate();

            if (posObj.ShouldRebase())
            {
                obj.GetComponent<PosObj>().Rebase(ClosestNodes(obj.transform.position));
            }
        }

        DebugDraw();
    }

    private void UpdateNodes()
    {
        foreach (GameObject a in nodes)
        {
            foreach (GameObject b in nodes)
            {
                if (a == b) { continue; }
                if (ConnectionExists(a, b)) { continue; }
                if ((a.transform.position - b.transform.position).magnitude > nodeDistance * 0.01f) { continue; }
                connections.Add(new Connection(a, b, nodeDistance));
                return;
            }
        }
    }

    private void UpdatePulls()
    {
        foreach (Connection c in connections)
        {
            AddSpringPull(c);
        }
    }

    private void AddSpringPull(Connection c)
    {
        //sub is a vector starting at a, pointing at b
        Vector3 sub = c.b.transform.position - c.a.transform.position;
        float actualDistance = sub.magnitude;
        float newMagnitude = PullMagnitude(actualDistance - c.length, c.stiffness);
        if (newMagnitude == 0) { return; }
        Vector3 pull = sub.normalized * newMagnitude;
        PosNode posNodeA = c.a.GetComponent<PosNode>();
        PosNode posNodeB = c.b.GetComponent<PosNode>();
        posNodeA.AddPull(-pull);
        posNodeA.AddPull(damping(posNodeA.GetNextVel()));
        posNodeB.AddPull(pull);
        posNodeB.AddPull(damping(posNodeB.GetNextVel()));
    }

    private void UpdateConnections()
    {
        int i = 0;
        /**/
        while (i < connections.Count - 1)
        {
            for (i = 0; i < connections.Count; ++i)
            {
                Connection c = connections[i];

                //Don't remove connections to unmoving nodes
                if (c.a.GetComponent<PosNode>().isUnmoving || c.b.GetComponent<PosNode>().isUnmoving) { continue; }

                float dist = (c.a.transform.position - c.b.transform.position).magnitude;
                if (dist < minDistance || c.a.GetComponent<PosNode>().isDead || c.b.GetComponent<PosNode>().isDead)
                {
                    RemoveConnection(c);
                    //Since we're modifying the list, start over every time we modify it.
                    break;
                }

            }
        }
        /**/
        /**/
        i = 0;
        while (i < connections.Count - 1)
        {
            for (i = 0; i < connections.Count; ++i)
            {
                Connection c = connections[i];

                //Only create nodes if connected to an unmoving node
                if (!c.a.GetComponent<PosNode>().isUnmoving && !c.b.GetComponent<PosNode>().isUnmoving) { continue; }

                float dist = (c.a.transform.position - c.b.transform.position).magnitude;
                if (dist > maxDistance)
                {
                    CreateNodeInConnection(c);
                    //Since we're modifying the list, start over every time we modify it.
                    break;
                }

            }
        }
        /**/

    }

    private void CreateNodeInConnection(Connection c)
    {
        Vector3 newPos = c.Midpoint();
        GameObject newNode = AddNode(newPos);

        connections.Remove(c);
        float distA = (c.a.transform.position - newPos).magnitude * compressionFactor;
        float distB = (c.b.transform.position - newPos).magnitude * compressionFactor;
        Connection cA = new Connection(c.a, newNode, distA);
        Connection cB = new Connection(newNode, c.b, distB);
        if (cA.a.GetComponent<PosNode>().isUnmoving)
        {
            connections.Add(cA);
            CreateExtraConnectionsForNewNode(newNode, cA);
        }
        else if (cB.b.GetComponent<PosNode>().isUnmoving)
        {
            connections.Add(cB);
            CreateExtraConnectionsForNewNode(newNode, cB);
        }

        //RegenerateConnections();

    }

    private void CreateExtraConnectionsForNewNode(GameObject node, Connection c)
    {
        GameObject unMoving1 = null;
        GameObject unMoving2 = null;
        GameObject unMoving3 = null;
        if (c.a.GetComponent<PosNode>().isUnmoving)
        {
            unMoving1 = c.a;
        }
        else if (c.b.GetComponent<PosNode>().isUnmoving)
        {
            unMoving1 = c.b;
        }

        foreach (Connection current in connections)
        {
            if (current == c) { continue; }
            if (current.a == unMoving1 && current.b.GetComponent<PosNode>().isUnmoving)
            {
                unMoving2 = current.b;
                break;
            }
            if (current.b == unMoving1 && current.a.GetComponent<PosNode>().isUnmoving)
            {
                unMoving2 = current.a;
                break;
            }
        }

        foreach (Connection current in connections)
        {
            if (current == c) { continue; }
            if (current.a == unMoving1 && current.b != unMoving2 && current.b.GetComponent<PosNode>().isUnmoving)
            {
                unMoving3 = current.b;
                break;
            }
            if (current.b == unMoving1 && current.a != unMoving2 && current.a.GetComponent<PosNode>().isUnmoving)
            {
                unMoving3 = current.a;
                break;
            }
        }

        if (unMoving2 != null)
        {
            foreach (Connection current in connections)
            {
                if (current.a == unMoving2 && !current.b.GetComponent<PosNode>().isUnmoving)
                {
                    float length = (node.transform.position - current.b.transform.position).magnitude;
                    connections.Add(new Connection(node, current.b, length));
                    break;
                }
                else if (current.b == unMoving2 && !current.a.GetComponent<PosNode>().isUnmoving)
                {
                    float length = (node.transform.position - current.a.transform.position).magnitude;
                    connections.Add(new Connection(node, current.a, length));
                    break;
                }
            }
        }
        if (unMoving3 != null)
        {
            foreach (Connection current in connections)
            {
                if (current.a == unMoving3 && !current.b.GetComponent<PosNode>().isUnmoving)
                {
                    float length = (node.transform.position - current.b.transform.position).magnitude;
                    connections.Add(new Connection(node, current.b, length));
                    break;
                }
                else if (current.b == unMoving3 && !current.a.GetComponent<PosNode>().isUnmoving)
                {
                    float length = (node.transform.position - current.a.transform.position).magnitude;
                    connections.Add(new Connection(node, current.a, length));
                    break;
                }
            }
        }
    }

    private bool ConnectionExists(GameObject a, GameObject b)
    {
        foreach (Connection c in connections)
        {
            if (c.a == a && c.b == b || c.a == b && c.b == a)
            {
                return true;
            }
        }
        return false;
    }

    private List<GameObject> ClosestNodes(Vector3 pos)
    {
        List<GameObject> sortedNodes = new List<GameObject>(nodes);
        sortedNodes.Sort((a, b) =>
        {
            float mA = (a.transform.position - pos).magnitude;
            float mB = (b.transform.position - pos).magnitude;
            return mA.CompareTo(mB);
        });
        return sortedNodes;
    }

    private List<Connection> ClosestConnections(Vector3 pos)
    {
        List<Connection> sorted = new List<Connection>(connections);
        sorted.Sort((a, b) =>
        {
            float mA = (a.Midpoint() - pos).magnitude;
            float mB = (b.Midpoint() - pos).magnitude;
            return mA.CompareTo(mB);
        });
        return sorted;
    }

    private void RemoveConnection(Connection midC)
    {
        //Diagram below shows f-g as the connection to remove
        //
        //a-b-c-d
        //| | | |
        //e-f-g-h
        //| | | |
        //i-j-k-l

        //f-g is replaced by New, and connections are updated.
        //
        //a-b-c-d
        //| \ / |
        //e-New-h
        //| / \ |
        //i-j-k-l

        GameObject newNode = AddNode(midC.Midpoint());

        foreach (Connection current in connections)
        {
            if (current == midC) { continue; }
            if (current.a == midC.b || current.a == midC.a)
            {
                current.a = newNode;
                //current.length = (current.b.transform.position - newNode.transform.position).magnitude * compressionFactor;
            }
            else if (current.b == midC.a || current.b == midC.b)
            {
                current.b = newNode;
                //current.length = (current.a.transform.position - newNode.transform.position).magnitude * compressionFactor;
            }
        }
        nodes.Remove(midC.a);
        nodes.Remove(midC.b);
        connections.Remove(midC);
    }

    private void StabilizeConnections()
    {
        foreach (Connection c in connections)
        {
            c.length = (c.a.transform.position - c.b.transform.position).magnitude;
        }
    }

    private float PullMagnitude(float x, float k)
    {
        //spring
        return -k * x;
    }

    private Vector3 damping(Vector3 vel)
    {
        float dampingStrength1 = 0.01f;
        Vector3 term1 = -dampingStrength1 * vel;

        float dampingStrength2 = 0.05f;
        Vector3 term2 = -dampingStrength2 * vel.magnitude * vel;

        return term1 + term2;
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
}
