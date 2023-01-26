using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;

public class SpatialHasher
{
    public class Bucket
    {
        public int hash = -1;
        public List<GameObject> objects = new List<GameObject>();
    }

    private const float cellSize = 0.4f;
    private const float totalSideLength = 10f;

    private const float inverseCellSize = 1/cellSize;
    private const int numSideCells = (int)(totalSideLength * inverseCellSize);
    private const int numCells = numSideCells * numSideCells;

    private Bucket[] buckets = new Bucket[numCells];
    private int[] spiralCoords = Spiral.GenerateSpiralCoords(numSideCells);

    public SpatialHasher()
    { 
        for(int i = 0; i < numCells; i++) { buckets[i] = new Bucket(); }
    }

    public int Hash(Vector3 point)
    {
        int x = (int)(point.x * inverseCellSize);
        int y = (int)(point.y * inverseCellSize);
        int hash = x + y * numSideCells;
        hash = Math.Clamp(hash, 0, numCells-1);
        return hash;
    }

    public Bucket BucketAt(Vector3 point)
    {
        return buckets[Hash(point)];
    }

    public void Update(GameObject obj)
    {
        PosNode posNode = obj.GetComponent<PosNode>();
        if (posNode != null)
        {
            Bucket currentBucket = posNode.bucket;
            Bucket newBucket = buckets[Hash(obj.transform.position)];
            if(currentBucket != newBucket)
            {
                currentBucket.objects.Remove(obj);
                newBucket.objects.Add(obj);
                posNode.bucket = newBucket;
            }
        }
    }

    public void AddObject(GameObject obj)
    {
        PosNode posNode = obj.GetComponent<PosNode>();
        if (posNode != null)
        {
            int hash = Hash(obj.transform.position);
            Bucket b = buckets[hash];
            b.hash = hash;
            b.objects.Add(obj);
            posNode.bucket = b;
        }
    }

    public void RemoveObject(GameObject obj)
    {
        PosNode posNode = obj.GetComponent<PosNode>();
        if (posNode != null)
        {
            Bucket currentBucket = posNode.bucket;
            currentBucket.objects.Remove(obj);
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