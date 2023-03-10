using System.Collections.Generic;
using UnityEngine;

public class FieldObject : MonoBehaviour
{
    public int dimension = 2;
    public float strength = 0f;
    public float radius = 0f;
    public float spiralStrength = 0f;
}
public class VelocityField
{
    List<GameObject> fieldObjects = new List<GameObject>();
    public GameObject AddFieldObject(Vector3 pos, int dimension, float radius, float strength, float spiralStrength)
    {
        GameObject newFObj = new GameObject("FieldObject");
        newFObj.transform.position = pos;

        FieldObject fObjComponent = newFObj.AddComponent<FieldObject>();
        fObjComponent.dimension = dimension;
        fObjComponent.radius = radius;
        fObjComponent.strength = strength;
        fObjComponent.spiralStrength = spiralStrength;

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
            Vector3 dir2 = Vector3.Cross(dir, Vector3.forward);
            //Inverse r squared law generalizes to inverse r^(dim-1)
            //However, we need to multiply denom by dir.magnitude to normalize dir
            //So that cancels with the fObj.dimension - 1, removing the - 1
            //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
            float denom = Mathf.Pow(dir.sqrMagnitude, (float)(fObj.dimension - 1));
            totalVelocity += (fObj.strength / denom) * dir + (fObj.spiralStrength / denom) * dir2;

        }
        return totalVelocity;
    }

    public GameObject ClosestFieldObject(Vector3 pos)
    {
        float distance = float.MaxValue;
        GameObject current = null;
        foreach (GameObject go in fieldObjects)
        {
            float currentDistance = (go.transform.position - pos).sqrMagnitude;
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
