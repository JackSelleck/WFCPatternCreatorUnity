using System.Collections.Generic;
using UnityEngine;

namespace WFCPatternCreator
{
    public class ScatteredIslands : MonoBehaviour, IZoneTemplateManager
    {
        [System.Serializable]
        public class Island
        {
            public Vector2 center = new(10, 10);
            public float radius = 8f;
            [Tooltip("Use ground zone at y=0, aerial zone at other heights")]
            public bool useHeightBasedZones = true;
            public ZoneType groundZone = ZoneType.Beach;
            public ZoneType aerialZone = ZoneType.Forest;
        }

        [Header("Island Configuration")]
        public List<Island> islands = new()
        {
            new Island { center = new Vector2(10, 10), radius = 8f },
            new Island { center = new Vector2(30, 15), radius = 8f },
            new Island { center = new Vector2(15, 30), radius = 8f },
            new Island { center = new Vector2(35, 35), radius = 8f }
        };

        [Header("Default Zones")]
        public ZoneType emptySpace = ZoneType.SpawnNothing;

        public ZoneType GetZoneType(int x, int y, int z)
        {
            foreach (Island island in islands)
            {
                float dist = Vector2.Distance(new Vector2(x, z), island.center);
                if (dist < island.radius)
                {
                    if (island.useHeightBasedZones)
                    {
                        if (y == 0)
                            return island.groundZone;
                        else
                            return island.aerialZone;
                    }
                    else
                    {
                        return island.aerialZone;
                    }
                }
            }

            return emptySpace;
        }

        public void AddIsland(Vector2 position, float radius, ZoneType ground, ZoneType aerial)
        {
            islands.Add(new Island
            {
                center = position,
                radius = radius,
                groundZone = ground,
                aerialZone = aerial
            });
        }
    }
}