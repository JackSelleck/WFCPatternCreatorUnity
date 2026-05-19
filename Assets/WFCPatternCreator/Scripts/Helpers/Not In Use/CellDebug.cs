using System.Collections.Generic;
using UnityEngine;

namespace WFCPatternCreator
{
    public class CellDebug : MonoBehaviour
    {
        public CellData data;
        private Dictionary<ZoneType, Color> zoneColors;
        public ZoneType zone; 
    
    #if UNITY_EDITOR
        private void Awake()
        {     
            if (zoneColors == null)
            {
                zoneColors = new Dictionary<ZoneType, Color>();
    
                ColorUtility.TryParseHtmlString("#2e2f2f", out Color c);
                zoneColors.Add(ZoneType.General, c);
    
                ColorUtility.TryParseHtmlString("#E8D39A", out c);
                zoneColors.Add(ZoneType.Beach, c);
    
                ColorUtility.TryParseHtmlString("#96c8e7", out c);
                zoneColors.Add(ZoneType.Sea, c);
    
                ColorUtility.TryParseHtmlString("#2A6FA8", out c);
                zoneColors.Add(ZoneType.River, c);
    
                ColorUtility.TryParseHtmlString("#2F6B3F", out c);
                zoneColors.Add(ZoneType.Forest, c);
    
                ColorUtility.TryParseHtmlString("#7fddf5", out c);
                zoneColors.Add(ZoneType.Lake, c);
    
                ColorUtility.TryParseHtmlString("#dce2f3", out c);
                zoneColors.Add(ZoneType.City, c);
    
                ColorUtility.TryParseHtmlString("#2e2f2f", out c);
                zoneColors.Add(ZoneType.SpawnNothing, c);
            }
        }
        private void OnDrawGizmos()
        {
            if (data == null || data.collapsed)
                return;
    
            if (!zoneColors.TryGetValue(zone, out Color c))
                return;
    
            Gizmos.color = new Color(c.r, c.g, c.b, 0.467f);
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.9567f);
        } 
    #endif
    }
}