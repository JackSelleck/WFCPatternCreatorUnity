using System.Collections.Generic;
using UnityEngine;

namespace WFCPatternCreator
{
    public class HollowSphere : MonoBehaviour, IZoneTemplateManager
    {
        [System.Serializable]
        public class SphereZone
        {
            public Vector3 center = new Vector3(50, 50, 50);
            public float innerRadius = 15f;
            public float outerRadius = 16f;
            public ZoneType shellZone = ZoneType.General;
            public ZoneType hollowInteriorZone = ZoneType.SpawnNothing;
        }

        [Header("Sphere Configuration")]
        public List<SphereZone> spheres = new List<SphereZone>()
        {
            new SphereZone
            {
                center = new Vector3(50, 50, 50),
                innerRadius = 15f,
                outerRadius = 16f,
                shellZone = ZoneType.General,
                hollowInteriorZone = ZoneType.SpawnNothing
            }
        };

        [Header("Default Zone")]
        public ZoneType emptySpace = ZoneType.SpawnNothing;

        public ZoneType GetZoneType(int x, int y, int z)
        {
            Vector3 pos = new Vector3(x, y, z);

            foreach (SphereZone sphere in spheres)
            {
                float dx = pos.x - sphere.center.x;
                float dy = pos.y - sphere.center.y;
                float dz = pos.z - sphere.center.z;

                // bounds check
                if (Mathf.Abs(dx) > sphere.outerRadius ||
                    Mathf.Abs(dy) > sphere.outerRadius ||
                    Mathf.Abs(dz) > sphere.outerRadius)
                    continue;

                float distSquared = dx * dx + dy * dy + dz * dz;
                float innerRadiusSquared = sphere.innerRadius * sphere.innerRadius;
                float outerRadiusSquared = sphere.outerRadius * sphere.outerRadius;

                // Inside hollow interior
                if (distSquared < innerRadiusSquared)
                {
                    return sphere.hollowInteriorZone;
                }

                // shell
                if (distSquared <= outerRadiusSquared)
                {
                    return sphere.shellZone;
                }
            }

            return emptySpace;
        }

        public void AddSphere(Vector3 center, float innerRadius, float outerRadius, ZoneType shell, ZoneType hollow)
        {
            spheres.Add(new SphereZone
            {
                center = center,
                innerRadius = innerRadius,
                outerRadius = outerRadius,
                shellZone = shell,
                hollowInteriorZone = hollow
            });
        }

        public void AddSphere(Vector3 center, float innerRadius, float outerRadius, ZoneType shell)
        {
            AddSphere(center, innerRadius, outerRadius, shell, ZoneType.SpawnNothing);
        }
    }
}