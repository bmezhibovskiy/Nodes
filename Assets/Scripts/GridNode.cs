using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GridNode : MonoBehaviour
{
    public bool isBorder = false;
    public bool isDead = false;
    public Vector3 vel = new Vector3();

    public void DebugDraw()
    {
        Color color = isBorder ? Color.red : Color.gray;
        float raySize = 0.16f;
        Vector3 dir = vel;
        if (dir.sqrMagnitude == 0)
        {
            dir = Vector3.right * 0.25f;
        }
        dir = Vector3.ClampMagnitude(dir * raySize, 0.15f);
        Debug.DrawRay(transform.position - dir * 0.5f, dir, color);
    }

    public void ResetForUpdate()
    {
        vel = Vector3.zero;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (!isBorder && !isDead)
        {
            Integrate();
        }
    }

    private void Integrate()
    {
        transform.position += vel * Time.deltaTime;
    }
}
