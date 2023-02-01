using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public interface ISectorObjectModule
{
    SectorObject parent { get; set; } 
    public void UpdateGridNode(GridNode gridNode);
}
public class SectorObjectModuleFactory
{
    public static ISectorObjectModule CreateModule(SectorObjectModuleInfo info, SectorObject parent)
    {
        switch (info.type)
        {
            case "Puller":
                return new SectorObjectPullerModule(info, parent);
        }
        return null;
    }

    private class SectorObjectPullerModule: ISectorObjectModule
    {
        public SectorObject parent { get; set; }
        private int dimension;
        private float pullStrength;
        private float perpendicularStrength;
        public SectorObjectPullerModule(SectorObjectModuleInfo info, SectorObject parent)
        {
            this.dimension = (int)info.parameters[0];
            this.pullStrength = info.parameters[1];
            this.perpendicularStrength = info.parameters[2];
            this.parent = parent;
        }

        public void UpdateGridNode(GridNode gridNode)
        {
            gridNode.vel += NodeVelocityAt(gridNode.transform.position);
        }

        private Vector3 NodeVelocityAt(Vector3 position)
        {
            Vector3 dir = parent.transform.position - position;
            Vector3 dir2 = Vector3.Cross(dir, Vector3.forward);
            //Inverse r squared law generalizes to inverse r^(dim-1)
            //However, we need to multiply denom by dir.magnitude to normalize dir
            //So that cancels with the fObj.dimension - 1, removing the - 1
            //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
            float denom = Mathf.Pow(dir.sqrMagnitude, (float)(dimension - 1));
            return (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
        }
    }
}


public class SectorObject: MonoBehaviour
{
    public string displayName;
    public Vector3 position;
    public float size;
    public int factionIndex;
    public ISectorObjectModule[] modules;
    public void Initialize(SectorObjectInfo info)
    {
        this.displayName = info.name;
        this.position = info.position;
        this.size = info.size;
        this.factionIndex = info.factionIndex;
        this.modules = info.moduleInfos.Select(moduleInfo => SectorObjectModuleFactory.CreateModule(moduleInfo, this)).ToArray();
    }

    public void UpdateGridNode(GridNode gridNode)
    {
        foreach(ISectorObjectModule module in modules)
        {
            module.UpdateGridNode(gridNode);
        }
    }
}
