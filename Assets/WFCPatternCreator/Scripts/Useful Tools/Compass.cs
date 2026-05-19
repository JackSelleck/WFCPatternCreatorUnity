using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace WFCPatternCreator
{
    public class Compass : MonoBehaviour
    {
        [Header("Options")]
        [SerializeField] private bool _enable = true;
        private float arrowLength;

        private void OnDrawGizmos()
        {
            if (!_enable) return;

            Vector3 pos = transform.position;
            float scaleX = transform.localScale.x;
            float scaleZ = transform.localScale.z;

            if (transform.localScale.z >= 0)
            {
                DrawDirection(pos, Vector3.forward, Color.blue, "North", scaleZ, 1f);
                DrawDirection(pos, Vector3.back, Color.yellow, "South", scaleZ, 1f);
            }
            else // If object scale is negative then it needs to be reversed
            {
                DrawDirection(pos, Vector3.back, Color.blue, "North", scaleZ, -1f);
                DrawDirection(pos, Vector3.forward, Color.yellow, "South", scaleZ, -1f);
            }
            if (transform.localScale.x >= 0)
            {
                DrawDirection(pos, Vector3.right, Color.red, "East", scaleX, 1f);
                DrawDirection(pos, Vector3.left, Color.green, "West", scaleX, 1f);
            }
            else
            {
                DrawDirection(pos, Vector3.left, Color.red, "East", scaleX, -1f);
                DrawDirection(pos, Vector3.right, Color.green, "West", scaleX, -1f);
            }

            // I imagine people can figure these out on their own
            // DrawDirection(pos, Vector3.up, Color.green, "Up");
            // DrawDirection(pos, Vector3.down, Color.yellow, "Down");
        }

        private void DrawDirection(Vector3 origin, Vector3 direction, Color color, string label, float scale, float labelDistance)
        {
            // arrow length needs to extend with scale so it dosent get consumed
            arrowLength = scale;

            Gizmos.color = color;
            Vector3 end = origin + direction * arrowLength;
            Gizmos.DrawLine(origin, end);
            DrawArrowHead(end, direction);

            Camera cam = Camera.current;


#if UNITY_EDITOR
            GUIStyle style = new(EditorStyles.whiteBoldLabel)
            {
                fontSize = 30
            };
            style.normal.textColor = Color.white;
            Vector3 labelPos = end + direction * labelDistance;
            Handles.color = color;
            Handles.Label(labelPos, label, style);

            Handles.BeginGUI();
            Vector2 size = style.CalcSize(new GUIContent(label));
            Vector3 screenPos = HandleUtility.WorldToGUIPoint(labelPos);

            Rect rect = new(screenPos, size);
            EditorGUI.DrawRect(rect, new Color(0, 0, 0, 1));
            GUI.Label(rect, label, style);
            Handles.EndGUI();
#endif
        }

        private void DrawArrowHead(Vector3 position, Vector3 direction)
        {
            float headSize = arrowLength * 0.2f;

            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 160, 0) * Vector3.forward;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -160, 0) * Vector3.forward;

            Gizmos.DrawLine(position, position + right * headSize);
            Gizmos.DrawLine(position, position + left * headSize);
        }
    }

}