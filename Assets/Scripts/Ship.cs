using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

public class Ship : MonoBehaviour
{
    public List<GameObject> closestNodes = new List<GameObject>();
    public Vector3 prevPos = Vector3.zero;
    public float size = 0.15f;

    private Vector3 accel = Vector3.zero;
    private Vector3 vel = Vector3.zero;
    private Vector3 offset = Vector3.zero;
    private bool needsRebase = false;

    public Vector3 GetVel()
    {
        return vel;
    }

    public void AddThrust(float strength)
    {
        accel += transform.up * strength;
    }

    public void Rotate(float speed)
    {
        transform.RotateAround(transform.position, Vector3.forward, speed);
    }

    public void Integrate()
    {
        IntegrateVerlet();
    }

    public void HandleCollisionAt(Vector3 collisionPos, Vector3 normal, float bounciness = 0.5f)
    {
        transform.position = collisionPos;
        if (vel.sqrMagnitude < 0.00002f)
        {
            //Velocity too small, set to 0 instead of bouncing forever, which can cause instability
            prevPos = collisionPos;
            return;
        }

        Assert.AreApproximatelyEqual(normal.sqrMagnitude, 1);

        //Reflect vel about normal
        vel = (vel - 2f * Vector3.Dot(vel, normal) * normal) * bounciness;

        //Would need time independent accel because otherwise we would need next frame's deltaTime to get the correct bounce
        //Verlet integration doesn't seem good for velocity based forces, since velocity is derived.
        //timeIndependentAccel += (-2 * normal * Vector3.Dot(vel, normal)) * bounciness;

        prevPos = collisionPos - vel;

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
            if (obj.GetComponent<GridNode>().isDead)
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
        transform.position = 2 * current - prevPos + accel * (Time.deltaTime * Time.deltaTime);
        accel = Vector3.zero;
        prevPos = current;
        vel = transform.position - current;
    }

    public void DebugDraw()
    {
        Debug.DrawLine(transform.position, transform.position + transform.up * 0.5f, UnityEngine.Color.green);
        Debug.DrawLine(transform.position, transform.position + (vel / Time.deltaTime) * 0.5f, new UnityEngine.Color(0.6f, 0, 0));
    }

    public void OnDrawGizmos()
    {
        Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.7f);
        Gizmos.DrawSphere(transform.position, size);

        Gizmos.color = UnityEngine.Color.white;
        foreach (GameObject cn in closestNodes)
        {
            Gizmos.DrawSphere(cn.transform.position, 0.07f);
        }
    }
}
