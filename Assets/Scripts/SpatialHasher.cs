using System;
using System.Collections.Generic;
using UnityEngine;

public class SpatiallyHashed : MonoBehaviour
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
    private int[] searchCoords;
    private float offset;
    private bool is3D = false;

    public SpatialHasher(float cellSize, float totalSideLength, bool is3D = false)
    {
        this.is3D = is3D;

        this.cellSize = cellSize;
        this.totalSideLength = totalSideLength;
        this.offset = totalSideLength * 0.5f;

        this.inverseCellSize = 1 / cellSize;
        this.numSideCells = (int)(totalSideLength * inverseCellSize);
        this.numCells = numSideCells * numSideCells * numSideCells;

        this.buckets = new Bucket[numCells];
        for (int i = 0; i < numCells; i++) { buckets[i] = new Bucket(); }

        if (is3D)
        {
            this.searchCoords = SearchCoords.Generate3DSearchCoords(numSideCells);
        }
        else
        {
            this.searchCoords = Spiral.GenerateSpiralCoords(numSideCells);
            //this.searchCoords = SearchCoords.Generate2DSearchCoords(numSideCells);
        }
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
            if (currentBucket != newBucket)
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
        int[] hashCoords = is3D ? Utils.to3D(hash, numSideCells) : Utils.to2D(hash, numSideCells);

        closest.AddRange(buckets[hash].objects);

        for (int i = 0; i < searchCoords.Length; ++i)
        {
            if (closest.Count >= numClosest)
            {
                break;
            }
            int searchCoord1D = searchCoords[i];
            int[] searchCoord = is3D ? Utils.to3D(searchCoord1D, numSideCells) : Utils.to2D(searchCoord1D, numSideCells);
            int nextHash = 0;
            int nextX = hashCoords[0] + searchCoord[0];
            int nextY = hashCoords[1] + searchCoord[1];
            if (is3D)
            {
                int nextZ = hashCoords[2] + searchCoord[2];
                nextHash = Utils.to1D(nextX, nextY, nextZ, numSideCells);
            }
            else
            {
                nextHash = Utils.to1D(nextX, nextY, numSideCells);
            }
            if (nextHash >= numCells || nextHash < 0)
            {
                continue;
            }
            closest.AddRange(buckets[nextHash].objects);
        }

        return closest;
    }

    private int Hash(Vector3 point)
    {
        int x = (int)((point.x + offset) * inverseCellSize);
        int y = (int)((point.y + offset) * inverseCellSize);
        int hash = 0;
        if(is3D)
        {
            int z = (int)((point.z + offset) * inverseCellSize);
            hash = Utils.to1D(x, y, z, numSideCells);
        }
        else
        {
            hash = Utils.to1D(x,y, numSideCells);
        }
        hash = Math.Clamp(hash, 0, numCells - 1);
        return hash;
    }
}

public class SearchCoords
{
    public static int[] Generate2DSearchCoords(int sideLength)
    {
        int totalCoords = sideLength * sideLength;
        int currentCoord = 0;
        int[] coords = new int[totalCoords];

        int[,] offsets = new int[,] {
            {0, 1},
            {1, 0},
            {0, -1},
            {-1, 0},
            {1, 1},
            {-1, 1},
            {1, -1},
            {-1, -1}
        };

        int currentLevel = 0;
        int currentOffset = 0;
        while (currentCoord < totalCoords)
        {
            if (currentOffset > 7)
            {
                ++currentLevel;
                currentOffset = 0;
            }
            coords[currentCoord++] = Utils.to1D(offsets[currentOffset, 0] * currentLevel,
                offsets[currentOffset, 1] * currentLevel,
                sideLength);
            ++currentOffset;
        }
        return coords;
    }
    public static int[] Generate3DSearchCoords(int sideLength)
    {
        int totalCoords = sideLength * sideLength * sideLength;
        int currentCoord = 0;
        int[] coords = new int[totalCoords];

        int[,] offsets = new int[,] {
            { 0, 1, 0},
            { 1, 0, 0},
            { 0,-1, 0},
            {-1, 0, 0},
            { 1, 1, 0},
            { 1,-1, 0},
            {-1, 1, 0},
            {-1, -1, 0},

            { 0, 0, 1},
            { 0, 1, 1},
            { 1, 0, 1},
            { 0,-1, 1},
            {-1, 0, 1},
            { 1, 1, 1},
            { 1,-1, 1},
            {-1, 1, 1},
            {-1,-1, 1},

            { 0, 0,-1},
            { 0, 1,-1},
            { 1, 0,-1},
            { 0,-1,-1},
            {-1, 0,-1},
            { 1, 1,-1},
            { 1,-1,-1},
            {-1, 1,-1},
            {-1,-1,-1}
        };

        int currentLevel = 0;
        int currentOffset = 0;
        while (currentCoord < totalCoords)
        {
            if(currentOffset > 25)
            {
                ++currentLevel;
                currentOffset = 0;
            }
            coords[currentCoord++] = Utils.to1D(offsets[currentOffset, 0] * currentLevel,
                offsets[currentOffset, 1] * currentLevel,
                offsets[currentOffset, 2] * currentLevel,
                sideLength);
            ++currentOffset;
        }
        return coords;
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
                if (currentCoord == totalCoords) { return coords; }
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