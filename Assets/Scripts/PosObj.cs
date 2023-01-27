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
    public List<GameObject> closestNodes = new List<GameObject>();
    private Vector3 pull = Vector3.zero;
    private Vector3 vel = Vector3.zero;
    public Vector3 prevPos = Vector3.zero;
    private Vector3 offset = Vector3.zero;
    private bool needsRebase = false;

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

    public void Integrate()
    {
        IntegrateVerlet();
    }

    public void HandleCollisionAt(Vector3 collisionPos)
    {
        transform.position = collisionPos;
        prevPos = collisionPos;
        needsRebase = true;
    }

    public void Rebase(List<GameObject> closestList)
    {
        closestNodes = closestList;
        offset = transform.position - AverageNodePos();
        needsRebase = false;
    }

    private Vector3 AverageNodePos()
    {
        Vector3 avgPos = Vector3.zero;
        foreach (GameObject obj in closestNodes)
        {
            avgPos += obj.transform.position;
        }
        avgPos /= (float)closestNodes.Count;
        return avgPos;
    }

    public Vector3 GridPosition()
    {
        return AverageNodePos() + offset;
    }

    public bool HasDeadNode()
    {
        foreach (GameObject obj in closestNodes)
        {
            if(obj.GetComponent<PosNode>().isDead)
            {
                return true;
            }
        }
        return false;
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

    private void IntegrateVerlet()
    {
        //Updating the current pos without updating the prev pos
        //causes the velocity to curve towards the grid movement
        transform.position += (GridPosition() - transform.position) * Time.deltaTime;

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
