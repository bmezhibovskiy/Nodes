using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;

public class SpatiallyHashed: MonoBehaviour
{
    public SpatialHasher.Bucket bucket;
}

public class SpatialHasher
{
    public class Bucket
    {
        public List<GameObject> objects = new List<GameObject>();
    }

    private float cellSize;
    private float totalSideLength;
    private float inverseCellSize;
    private int numSideCells;
    private int numCells;
    private Bucket[] buckets;
    private int[] spiralCoords;

    public SpatialHasher(float cellSize, float totalSideLength)
    {
        this.cellSize = cellSize;
        this.totalSideLength = totalSideLength;

        this.inverseCellSize = 1 / cellSize;
        this.numSideCells = (int)(totalSideLength * inverseCellSize);
        this.numCells = numSideCells * numSideCells;

        this.buckets = new Bucket[numCells];
        for(int i = 0; i < numCells; i++) { buckets[i] = new Bucket(); }

        this.spiralCoords = Spiral.GenerateSpiralCoords(numSideCells);
    }

    public void AddObject(GameObject obj)
    {
        SpatiallyHashed hashed = obj.AddComponent<SpatiallyHashed>();
        Bucket b = buckets[Hash(obj.transform.position)];
        b.objects.Add(obj);
        hashed.bucket = b;
    }

    public void UpdateObject(GameObject obj)
    {
        SpatiallyHashed hashed = obj.GetComponent<SpatiallyHashed>();
        if (hashed != null)
        {
            Bucket currentBucket = hashed.bucket;
            Bucket newBucket = buckets[Hash(obj.transform.position)];
            if(currentBucket != newBucket)
            {
                currentBucket.objects.Remove(obj);
                newBucket.objects.Add(obj);
                hashed.bucket = newBucket;
            }
        }
    }

    public void RemoveObject(GameObject obj)
    {
        SpatiallyHashed hashed = obj.GetComponent<SpatiallyHashed>();
        if (hashed != null)
        {
            Bucket currentBucket = hashed.bucket;
            currentBucket.objects.Remove(obj);
            MonoBehaviour.Destroy(hashed);
        }
    }

    public List<GameObject> ClosestObjects(Vector3 point, int numClosest)
    {
        List<GameObject> closest = new List<GameObject>();
        int hash = Hash(point);
        int x = hash % numSideCells;
        int y = hash / numSideCells;

        for(int i = 0; i < spiralCoords.Length; ++i)
        {
            int spiralHash = spiralCoords[i];
            int spiralX = spiralHash % numSideCells;
            int spiralY = spiralHash / numSideCells;
            int nextX = x + spiralX;
            int nextY = y + spiralY;
            int nextHash = nextX + nextY * numSideCells;
            if(nextHash >= numCells || nextHash < 0)
            {
                continue;
            }
            closest.AddRange(buckets[nextHash].objects);
            if(closest.Count >= numClosest)
            {
                break;
            }
        }

        return closest;
    }

    private int Hash(Vector3 point)
    {
        int x = (int)(point.x * inverseCellSize);
        int y = (int)(point.y * inverseCellSize);
        int hash = x + y * numSideCells;
        hash = Math.Clamp(hash, 0, numCells - 1);
        return hash;
    }
}

//Adapted from https://stackoverflow.com/a/33639875/1864591
public class Spiral
{
    public static int[] GenerateSpiralCoords(int sideLength)
    {
        int totalCoords = sideLength * sideLength;
        int currentCoord = 0;
        int[] coords = new int[totalCoords];

        int x = 0;
        int y = 0;
        int d = 1;
        int m = 1;

        while (currentCoord < totalCoords)
        {
            while (2 * x * d < m)
            {
                coords[currentCoord++] = x + y * sideLength;
                if(currentCoord == totalCoords) { return coords; }
                x += d;
            }
            while (2 * y * d < m)
            {
                coords[currentCoord++] = x + y * sideLength;
                if (currentCoord == totalCoords) { return coords; }
                y += d;
            }
            d = -d;
            ++m;
        }
        return coords;
    }
}