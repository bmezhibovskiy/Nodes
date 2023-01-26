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
    private Vector3 pull = Vector3.zero;
    private Vector3 vel = Vector3.zero;
    private bool needsRebase = false;
    public Vector3 prevPos = Vector3.zero;
    private Vector3 offset = Vector3.zero;

    public float GetSpeed()
    {
        return vel.magnitude;
    }

    public void AddThrust(float strength)
    {
        pull += transform.up * strength;
    }

    public void Rotate(float speed)
    {
        transform.RotateAround(transform.position, Vector3.forward, speed);
    }

    public void Integrate(Vector3 gridMovement)
    {
        IntegrateVerlet(gridMovement);
    }

    public void HandleCollisionAt(Vector3 collisionPos)
    {
        transform.position = collisionPos;
        needsRebase = true;
    }

    public void Rebase(List<GameObject> closestList)
    {
        closestNodes = closestList.Take(numNodes).ToArray();
        Vector3 avgPos = (closestNodes[0].transform.position + closestNodes[1].transform.position + closestNodes[2].transform.position) / 3;
        offset = transform.position - avgPos;
        needsRebase = false;
    }

    public Vector3 CalculatePosition()
    {
        Vector3 avgPos = (closestNodes[0].transform.position + closestNodes[1].transform.position + closestNodes[2].transform.position) / 3;
        return avgPos + offset;
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

    // Start is called before the first frame update
    void Start()
    {
        transform.up = Vector3.up;
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void IntegrateVerlet(Vector3 gridMovement)
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

    public void DebugDraw()
    {
        Debug.DrawLine(transform.position, transform.position + transform.up * 0.5f, UnityEngine.Color.green);
        Debug.DrawLine(transform.position, transform.position + vel * 0.5f, new UnityEngine.Color(0.6f,0,0));
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
