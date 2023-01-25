using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using static UnityEditor.PlayerSettings;

public class PosObj : MonoBehaviour
{
    public static int numNodes = 3; //Dimension + 1, so for 2d it's 3
    public GameObject[] closestNodes = new GameObject[numNodes];
    private float[] coefficients = new float[numNodes - 1];
    private Vector3 pull = Vector3.zero;
    private Vector3 vel = Vector3.zero;
    private bool concave = false;
    private bool needsRebase = false;
    public Vector3 prevPos = Vector3.zero;

    public float GetSpeed()
    {
        return vel.magnitude;
    }
    public void AddThrust(float strength)
    {
        pull += transform.up * strength;
    }

    public void Integrate(Vector3 gridMovement)
    {
        IntegrateVerlet(gridMovement);
    }
    public void IntegrateVerlet(Vector3 gridMovement)
    {
        //Updating the current pos without updating the prev pos
        //causes the velocity to curve towards the grid movement
        transform.position += gridMovement * Time.deltaTime;

        Vector3 current = transform.position;
        transform.position = 2 * current - prevPos + pull * (Time.deltaTime * Time.deltaTime);
        vel = transform.position - current;
        pull = Vector3.zero;
        prevPos = current;
        vel = (transform.position - current) / Time.deltaTime;
    }

    public void Rotate(float speed)
    {
        transform.RotateAround(transform.position, Vector3.forward, speed);
    }

    public void HandleCollisionAt(Vector3 collisionPos)
    {
        transform.position = collisionPos;
        needsRebase = true;
    }
    // Start is called before the first frame update
    void Start()
    {
        transform.up = Vector3.up;
    }

    // Update is called once per frame
    void Update()
    {

    }

    private Vector3 offset= Vector3.zero;
    public void Rebase(List<GameObject> closestList)
    {
        closestNodes = closestList.Take(numNodes).ToArray();
        Vector3 avgPos = (closestNodes[0].transform.position + closestNodes[1].transform.position + closestNodes[2].transform.position)/3;
        offset = transform.position - avgPos;
        needsRebase = false;
    }

    public Vector3 CalculatePosition()
    {
        Vector3 avgPos = (closestNodes[0].transform.position + closestNodes[1].transform.position + closestNodes[2].transform.position) / 3;
        return avgPos + offset;
    }

    public void Rebase2(List<GameObject> closestList)
    {
        /*
            Currently supports 2D only

            Given Closest node positions A, B, and C; in order of closeness.
            Given Object position P
            Let vectors AB = (B-A) and AC = (C-A) 
            
            We can define this object's position in 2D as:
            P = A + x(AB) + y(AC)
            
            We need to figure out the coefficients x and y by setting up a system of equations

            First rewriting the equation by expanding the 2D vectors P = (P1,P2), A = (A1,A2), AB = (B1, B2), AC = (C1,C2)
            (P1,P2) = (A1,A2) + x(B1,B2) + y(C1,C2)

            Then setting up the resulting system of equations, and solving for x and y:
            P1 = A1 + B1x + C1y
            P2 = A2 + B2x + C2y

            The solution below produced by Wolfram Alpha
        */

        Vector3 pos = transform.position;
        float P1 = pos.x;
        float P2 = pos.y;

        float A1 = 0;
        float A2 = 0;
        float B1 = 0;
        float B2 = 0;
        float C1 = 0;
        float C2 = 0;

        int skipNodes = 0;

        //Skip colinear nodes
        while (skipNodes < closestList.Count - 2 && (B1 * C2 == B2 * C1 || B2 * C1 == B1 * C2 || C1 == 0))
        {
            Vector3 basis1 = closestList[1 + skipNodes].transform.position - closestList[skipNodes].transform.position;
            Vector3 basis2 = closestList[2 + skipNodes].transform.position - closestList[skipNodes].transform.position;
            A1 = closestList[skipNodes].transform.position.x;
            A2 = closestList[skipNodes].transform.position.y;
            B1 = basis1.x;
            B2 = basis1.y;
            C1 = basis2.x;
            C2 = basis2.y;
            ++skipNodes;
        }

        closestNodes[0] = closestList[skipNodes - 1];
        closestNodes[1] = closestList[skipNodes];
        closestNodes[2] = closestList[skipNodes + 1];

        coefficients[0] = (-A1 * C2 + A2 * C1 - C1 * P2 + C2 * P1) / (B1 * C2 - B2 * C1);
        coefficients[1] = (-A1 * B2 + A2 * B1 - B1 * P2 + B2 * P1) / (B2 * C1 - B1 * C2);

        needsRebase = false;

        Vector3 valueToCheck = CalculatePosition2();
        Assert.AreApproximatelyEqual(pos.x, valueToCheck.x);
        Assert.AreApproximatelyEqual(pos.y, valueToCheck.y);
    }

    public Vector3 CalculatePosition2()
    {
        return (closestNodes[0].transform.position + closestNodes[1].transform.position * coefficients[0] + closestNodes[2].transform.position * coefficients[1]) / 3;
    }

    public bool PointInsideTriangle2D(Vector3 p, Vector3 t1, Vector3 t2, Vector3 t3)
    {
        float x1 = t1.x, y1 = t1.y;
        float x2 = t2.x, y2 = t2.y;
        float x3 = t3.x, y3 = t3.y;

        float x, y;
        x = p.x;
        y = p.y;

        float a = ((y2 - y3) * (x - x3) + (x3 - x2) * (y - y3)) / ((y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3));
        float b = ((y3 - y1) * (x - x3) + (x1 - x3) * (y - y3)) / ((y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3));
        float c = 1 - a - b;

        return a >= 0 && a <= 1 && b >= 0 && b <= 1 && c >= 0 && c <= 1;
    }

    public bool HasDeadNode()
    {
        return closestNodes[0].GetComponent<PosNode>().isDead ||
                closestNodes[1].GetComponent<PosNode>().isDead ||
                closestNodes[2].GetComponent<PosNode>().isDead;
    }

    public bool ShouldRebase()
    {
        return needsRebase || vel.sqrMagnitude > 0 || HasDeadNode();
    }

    public void DebugDraw()
    {
        Debug.DrawLine(transform.position, transform.position + transform.up * 0.5f, UnityEngine.Color.green);
        Debug.DrawLine(transform.position, transform.position + vel * 0.5f, UnityEngine.Color.red);
    }

    public void OnDrawGizmos()
    {
        float size = 0.12f;
        Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.7f);
        Gizmos.DrawSphere(transform.position, size);

        size = 0.07f;
        Gizmos.color = UnityEngine.Color.white;
        foreach (GameObject cn in closestNodes)
        {
            Gizmos.DrawSphere(cn.transform.position, size);
        }
    }
}
