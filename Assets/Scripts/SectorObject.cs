using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using static SectorObjectModuleFactory;

public interface ISectorObjectModule
{
    SectorObject parent { get; set; }
    public void UpdateGridNode(GridNode gridNode);
    public void UpdateShip(Ship ship);
}
public class SectorObjectModuleFactory
{
    public static ISectorObjectModule CreateModule(SectorObjectModuleInfo info, SectorObject parent)
    {
        switch (info.type)
        {
            case "NodePuller":
                return new SectorObjectPullerModule(info, parent);
            case "ShipRepellent":
                return new SectorObjectShipRepellentModule(info, parent);
            case "Dock":
                return new SectorObjectDockModule(info, parent);
        }
        return null;
    }

    private class SectorObjectPullerModule : ISectorObjectModule
    {
        public SectorObject parent { get; set; }
        private float order;
        private float pullStrength;
        private float perpendicularStrength;
        public SectorObjectPullerModule(SectorObjectModuleInfo info, SectorObject parent)
        {
            this.order = info.parameters[0];
            this.pullStrength = info.parameters[1];
            this.perpendicularStrength = info.parameters[2];
            this.parent = parent;
        }

        public void UpdateGridNode(GridNode gridNode)
        {
            if (gridNode.isDead) { return; }

            if ((parent.transform.position - gridNode.transform.position).sqrMagnitude < parent.size * parent.size)
            {
                gridNode.isDead = true;
            }
            else
            {
                gridNode.vel += NodeVelocityAt(gridNode.transform.position);
            }
        }

        public void UpdateShip(Ship ship)
        {
            //Do nothing, since Puller modules only affect grid nodes
        }

        private Vector3 NodeVelocityAt(Vector3 position)
        {
            Vector3 dir = parent.transform.position - position;
            Vector3 dir2 = Vector3.Cross(dir, Vector3.forward);
            //Inverse r squared law generalizes to inverse r^(dim-1)
            //However, we need to multiply denom by dir.magnitude to normalize dir
            //So that cancels with the fObj.dimension - 1, removing the - 1
            //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
            float denom = Mathf.Pow(dir.sqrMagnitude, (order - 1f));
            return (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
        }
    }

    private class SectorObjectShipRepellentModule : ISectorObjectModule
    {
        public SectorObject parent { get; set; }
        private float order;
        private float pullStrength;
        private float perpendicularStrength;
        public SectorObjectShipRepellentModule(SectorObjectModuleInfo info, SectorObject parent)
        {
            this.order = (int)info.parameters[0];
            this.pullStrength = info.parameters[1];
            this.perpendicularStrength = info.parameters[2];
            this.parent = parent;
        }

        public void UpdateGridNode(GridNode gridNode)
        {
            //Do nothing, as this module only affects ships
        }

        public void UpdateShip(Ship ship)
        {
            ship.AddAccel(ShipAccelAt(ship.transform.position));
        }
        private Vector3 ShipAccelAt(Vector3 position)
        {
            Vector3 dir = position - parent.transform.position;
            Vector3 dir2 = Vector3.Cross(dir, Vector3.forward);
            //Inverse r squared law generalizes to inverse r^(dim-1)
            //However, we need to multiply denom by dir.magnitude to normalize dir
            //So that cancels with the fObj.dimension - 1, removing the - 1
            //However #2, dir.sqrMagnitude is cheaper, but will require bringing back the - 1
            float denom = Mathf.Pow(dir.sqrMagnitude, (order - 1f));
            return (pullStrength / denom) * dir + (perpendicularStrength / denom) * dir2;
        }
    }

    private class SectorObjectDockModule : ISectorObjectModule
    {
        public SectorObject parent { get; set; }
        public int capacity;

        private const float sqrMaxSafeSpeed = 0.5f * 0.5f;
        public SectorObjectDockModule(SectorObjectModuleInfo info, SectorObject parent)
        {
            this.parent = parent;
            this.capacity = (int)info.parameters[0];
        }
        public void UpdateGridNode(GridNode gridNode)
        {
            //Do nothing, as this module only affects ships
        }

        public void UpdateShip(Ship ship)
        {
            float dt2 = Time.deltaTime * Time.deltaTime;
            if (!ship.IsDocked() && ship.GetVel().sqrMagnitude / dt2 < sqrMaxSafeSpeed)
            {
                if(Vector3.Distance(ship.transform.position, parent.transform.position) < parent.size + ship.size)
                {
                    ship.DockAt(this);
                }
            }
        }

    }
}


public class SectorObject : MonoBehaviour
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
        //Some modules don't affect nodes, so we don't do anything extra here
        foreach (ISectorObjectModule module in modules)
        {
            module.UpdateGridNode(gridNode);
        }
    }

    public void UpdateShip(Ship ship)
    {
        //All sector objects collide with ships regardless of what modules it has
        Vector3 dist = ship.transform.position - transform.position;
        if (dist.magnitude < size + ship.size)
        {
            Vector3? intersection = Utils.LineSegmentCircleIntersection(transform.position, size + ship.size, ship.prevPos, ship.transform.position);
            if (intersection != null)
            {
                ship.HandleCollisionAt(intersection.Value, (intersection.Value - transform.position).normalized);
            }
        }

        foreach (ISectorObjectModule module in modules)
        {
            module.UpdateShip(ship);
        }
    }
}
