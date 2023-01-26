using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public void RemoveFieldObject(GameObject obj)
    {
        fieldObjects.Remove(obj);
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
            float denom = Mathf.Pow(dir.magnitude, (float)(fObj.dimension - 1));
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
            if (currentDistance < distance)
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
