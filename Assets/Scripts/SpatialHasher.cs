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

    public static SpatialHasher CreateSpatialHasher(float bucketSize, float totalSideLength, bool is3D)
    {
        return is3D ? new SpatialHasher3D(bucketSize, totalSideLength) : new SpatialHasher2D(bucketSize, totalSideLength);
    }

    protected float bucketSize;
    protected float totalSideLength;
    protected float inverseBucketSize;
    protected int numSideBuckets;
    protected float offset;
    protected int numBuckets;
    protected Bucket[] buckets;
    protected int[][] shellCoords;

    protected SpatialHasher(float bucketSize, float totalSideLength)
    {
        this.bucketSize = bucketSize;
        this.totalSideLength = totalSideLength;
        this.inverseBucketSize = 1 / bucketSize;
        this.numSideBuckets = (int)(totalSideLength * inverseBucketSize);
        this.offset = totalSideLength * 0.5f;
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

    public virtual List<GameObject> ClosestObjects(Vector3 point, int numClosest)
    {
        throw new NotSupportedException();
    }

    protected virtual int Hash(Vector3 point)
    {
        throw new NotSupportedException();
    }
}

public class SpatialHasher2D: SpatialHasher
{ 
    
    public SpatialHasher2D(float bucketSize, float totalSideLength) : base(bucketSize, totalSideLength)
    {
        this.numBuckets = numSideBuckets * numSideBuckets;

        this.buckets = new Bucket[numBuckets];
        for (int i = 0; i < numBuckets; i++) { buckets[i] = new Bucket(); }

        this.shellCoords = SearchCoords.GenerateShells(false, numSideBuckets);
    }

    //To get the most accurate closest objects to the point,
    //we need to consider every object in a shell and compare their position to the point.
    //Otherwise, we won't get the actual closest points, and the result will be biased in a certain direction.
    public override List<GameObject> ClosestObjects(Vector3 point, int numberOfObjectsToFetch)
    {
        List<GameObject> closestObjects = new List<GameObject>();
        int[] hashCoords = Utils.to2D(Hash(point), numSideBuckets);
        int level = 0;
        while (closestObjects.Count < numberOfObjectsToFetch)
        {
            List<GameObject> shellObjects = new List<GameObject>();
            int[] shell = shellCoords[level];
            foreach (int shellCoord1d in shell)
            {
                int[] shellCoord2d = Utils.to2D(shellCoord1d, numSideBuckets);
                int[] nextBucketCoord = new int[2];
                nextBucketCoord[0] = hashCoords[0] + shellCoord2d[0];
                nextBucketCoord[1] = hashCoords[1] + shellCoord2d[1];
                int nextBucketHash = Utils.to1D(nextBucketCoord[0], nextBucketCoord[1], numSideBuckets);
                if(nextBucketHash < 0 || nextBucketHash >= numBuckets)
                {
                    continue;
                }
                shellObjects.AddRange(buckets[nextBucketHash].objects);
            }

            shellObjects.Sort((a, b) => (a.transform.position - point).sqrMagnitude.CompareTo((b.transform.position - point).sqrMagnitude));
            
            for (int i = 0; i < shellObjects.Count && closestObjects.Count < numberOfObjectsToFetch; i++)
            {
                closestObjects.Add(shellObjects[i]);
            }

            ++level;
        }

        return closestObjects;
    }

    protected override int Hash(Vector3 point)
    {
        int x = (int)((point.x + offset) * inverseBucketSize);
        int y = (int)((point.y + offset) * inverseBucketSize);

        int hash = Utils.to1D(x, y, numSideBuckets);
        hash = Math.Clamp(hash, 0, numBuckets - 1);

        return hash;
    }
}
public class SpatialHasher3D : SpatialHasher
{
    public SpatialHasher3D(float bucketSize, float totalSideLength) : base(bucketSize, totalSideLength)
    {
        this.numBuckets = numSideBuckets * numSideBuckets * numSideBuckets;

        this.buckets = new Bucket[numBuckets];
        for (int i = 0; i < numBuckets; i++) { buckets[i] = new Bucket(); }

        this.shellCoords = SearchCoords.GenerateShells(true, numSideBuckets);
    }

    //To get the most accurate closest objects to the point,
    //we need to consider every object in a shell and compare their position to the point.
    //Otherwise, we won't get the actual closest points, and the result will be biased in a certain direction.

    public override List<GameObject> ClosestObjects(Vector3 point, int numberOfObjectsToFetch)
    {
        List<GameObject> closest = new List<GameObject>();
        int hash = Hash(point);
        int[] hashCoords = Utils.to3D(hash, numSideBuckets);
        int level = 0;
        while (closest.Count < numberOfObjectsToFetch)
        {
            List<GameObject> shellObjects = new List<GameObject>();
            int[] shell = shellCoords[level];
            foreach (int shellCoord1d in shell)
            {
                int[] shellCoord2d = Utils.to3D(shellCoord1d, numSideBuckets);
                int[] nextBucketCoord = new int[3];
                nextBucketCoord[0] = hashCoords[0] + shellCoord2d[0];
                nextBucketCoord[1] = hashCoords[1] + shellCoord2d[1];
                nextBucketCoord[2] = hashCoords[2] + shellCoord2d[2];
                int nextBucketHash = Utils.to1D(nextBucketCoord[0], nextBucketCoord[1], nextBucketCoord[2], numSideBuckets);
                if (nextBucketHash < 0 || nextBucketHash >= numBuckets)
                {
                    continue;
                }
                shellObjects.AddRange(buckets[nextBucketHash].objects);
            }

            shellObjects.Sort((a, b) => (a.transform.position - point).sqrMagnitude.CompareTo((b.transform.position - point).sqrMagnitude));

            for (int i = 0; i < shellObjects.Count && closest.Count < numberOfObjectsToFetch; i++)
            {
                closest.Add(shellObjects[i]);
            }

            ++level;
        }
        return closest;
    }

    protected override int Hash(Vector3 point)
    {
        int x = (int)((point.x + offset) * inverseBucketSize);
        int y = (int)((point.y + offset) * inverseBucketSize);
        int z = (int)((point.z + offset) * inverseBucketSize);

        int hash = Utils.to1D(x, y, z, numSideBuckets);
        hash = Math.Clamp(hash, 0, numBuckets - 1);

        return hash;
    }
}
public class SearchCoords
{
    public static int[][] GenerateShells(bool is3D, int numSideCells)
    {
        int[][] shells = new int[NumTotalShells(numSideCells)][];
        for (int i = 0; i < shells.Length; ++i)
        {
            shells[i] = is3D ? Generate3DShell(i, numSideCells) : Generate2DShell(i, numSideCells);
        }
        return shells;
    }

    private static int NumTotalShells(int numSideCells)
    {
        return (numSideCells + 1) / 2;
    }

    private static int[] Generate2DShell(int level, int numSideCells)
    {
        //Slow and inefficient brute force algorithm, because this will be precomputed

        List<int> shell = new List<int>();

        int max = level;
        int min = -max;
        for (int i = min; i <= max; ++i)
        {
            for (int j = min; j <= max; ++j)
            {
                if (i == min || j == min || i == max || j == max)
                {
                    shell.Add(Utils.to1D(i, j, numSideCells));
                }
            }
        }
        shell.Sort(delegate (int a, int b)
        {
            int[] a2d = Utils.to2D(a, numSideCells);
            int[] b2d = Utils.to2D(b, numSideCells);
            Vector2 aVec = new Vector2(a2d[0], a2d[1]);
            Vector2 bVec = new Vector2(b2d[0], b2d[1]);
            float distA = (aVec - Vector2.zero).sqrMagnitude;
            float distB = (bVec - Vector2.zero).sqrMagnitude;
            return distA.CompareTo(distB);
        });
        return shell.ToArray();
    }

    private static int[] Generate3DShell(int level, int numSideCells)
    {
        //Slow and inefficient brute force algorithm, because this will be precomputed

        List<int> shell = new List<int>();

        int max = level;
        int min = -max;
        for (int i = min; i <= max; ++i)
        {
            for (int j = min; j <= max; ++j)
            {
                for (int k = min; k <= max; ++k)
                {
                    if (i == min || j == min || k == min || i == max || j == max || k == max)
                    {
                        shell.Add(Utils.to1D(i, j, k, numSideCells));
                    }
                }
            }
        }
        shell.Sort(delegate (int a, int b)
        {
            int[] a3d = Utils.to3D(a, numSideCells);
            int[] b3d = Utils.to3D(b, numSideCells);
            Vector3 aVec = new Vector3(a3d[0], a3d[1], a3d[2]);
            Vector3 bVec = new Vector3(b3d[0], b3d[1], b3d[2]);
            float distA = (aVec - Vector3.zero).sqrMagnitude;
            float distB = (bVec - Vector3.zero).sqrMagnitude;
            return distA.CompareTo(distB);
        });
        return shell.ToArray();
    }
}