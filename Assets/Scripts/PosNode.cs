using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PosNode : MonoBehaviour
{
    public bool isUnmoving = false;
    public bool isDead = false;
    private Vector3 pull = new Vector3();
    private Vector3 vel = new Vector3();
    private const float maxSpeed = 10.0f;

    public void AddPull(Vector3 newPull)
    {
        pull += newPull;
    }

    public void AddVel(Vector3 v)
    {
        vel += v;
    }
    public void SetVel(Vector3 v)
    {
        vel = v;
    }

    public Vector3 GetVel()
    {
        return vel;
    }

    public Vector3 GetNextVel()
    {
        Vector3 nextVel = vel + pull * Time.deltaTime;
        if (nextVel.magnitude > maxSpeed)
        {
            nextVel = nextVel.normalized * maxSpeed;
        }
        return nextVel;
    }

    public void DebugDraw()
    {
        Color color = isUnmoving ? Color.red : Color.gray;
        float raySize = 0.04f;
        Debug.DrawRay(transform.position - new Vector3(0, raySize * 0.5f, 0), new Vector3(0, raySize, 0), color);
        Debug.DrawRay(transform.position - new Vector3(raySize * 0.5f, 0, 0), new Vector3(raySize, 0, 0), color);
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (!isUnmoving && !isDead)
        {
            IntegratePull();
        }
        pull = new Vector3();
    }

    private void IntegratePull()
    {
        vel = GetNextVel();
        transform.position += vel * Time.deltaTime;
    }
}
