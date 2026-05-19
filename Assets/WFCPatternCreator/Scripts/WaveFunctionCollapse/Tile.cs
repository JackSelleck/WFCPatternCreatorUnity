#if DOTWEEN
using DG.Tweening;
#endif

using System.Collections.Generic;
using UnityEngine;

namespace WFCPatternCreator
{
    public class Tile : MonoBehaviour
    {
        [Header("Options")]
        public ZoneType allowedZone;
        [Tooltip("Automatically assigns the opposite neighbor on the other tile (Doubles Efficiency).")]
        [HideInInspector] public bool applyReverse = true;
        [HideInInspector] public bool adjustYPosition = false;
        [HideInInspector] public float yPosAdjustment = -1f;
        [Space]
        [HideInInspector] public Tile[] northNeighbours;
        [HideInInspector] public Tile[] southNeighbours;
        [HideInInspector] public Tile[] eastNeighbours;
        [HideInInspector] public Tile[] westNeighbours;
        [HideInInspector] public Tile[] upNeighbours;
        [HideInInspector] public Tile[] downNeighbours;

#if DOTWEEN
        private void Awake()
        {
            // You can install the free DOTween package from the unity asset store if you want the tiles to bounce as they spawn in
            // The package is efficient and will not slow down generation very much
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 1f).SetEase(Ease.OutElastic);
        }
#endif

        public enum NeighbourDirection
        {
            North,
            South,
            East,
            West,
            Up,
            Down
        }

        #region Editor only for custom tile inspector
#if UNITY_EDITOR

        // Log previously added tile for mirrored assigning
        [SerializeField, HideInInspector] private Tile[] prevNorth;
        [SerializeField, HideInInspector] private Tile[] prevSouth;
        [SerializeField, HideInInspector] private Tile[] prevEast;
        [SerializeField, HideInInspector] private Tile[] prevWest;
        [SerializeField, HideInInspector] private Tile[] prevUp;
        [SerializeField, HideInInspector] private Tile[] prevDown;

        public void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (UnityEditor.Undo.isProcessing)
                return;

            ProcessChanges(northNeighbours, prevNorth, NeighbourDirection.South);
            ProcessChanges(southNeighbours, prevSouth, NeighbourDirection.North);
            ProcessChanges(eastNeighbours, prevEast, NeighbourDirection.West);
            ProcessChanges(westNeighbours, prevWest, NeighbourDirection.East);
            ProcessChanges(upNeighbours, prevUp, NeighbourDirection.Down);
            ProcessChanges(downNeighbours, prevDown, NeighbourDirection.Up);

            CacheCurrent();
        }

        public void ProcessChanges(Tile[] current, Tile[] previous, NeighbourDirection opposite)
        {
            if (!applyReverse)
                return;

            // Add reverse neighbor
            if (current != null)
            {
                foreach (Tile tile in current)
                {
                    if (tile == null)
                        continue;

                    if (!Contains(previous, tile))
                        tile.AddNeighbourEditorOnly(this, opposite);
                }
            }

            // Remove reverse neighbor
            if (previous != null)
            {
                foreach (Tile tile in previous)
                {
                    if (tile == null)
                        continue;

                    if (!Contains(current, tile))
                        tile.RemoveNeighbourEditorOnly(this, opposite);
                }
            }
        }

        public void AddNeighbourEditorOnly(Tile tile, NeighbourDirection direction)
        {
            Tile[] array = GetArrayByDirection(direction);

            if (Contains(array, tile))
                return;

            array = Append(array, tile);
            SetArrayByDirection(direction, array);

            UpdateCacheForDirection(direction, array);

            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void RemoveNeighbourEditorOnly(Tile tile, NeighbourDirection direction)
        {
            Tile[] array = GetArrayByDirection(direction);

            if (!Contains(array, tile))
                return;

            array = Remove(array, tile);
            SetArrayByDirection(direction, array);

            UpdateCacheForDirection(direction, array);

            UnityEditor.EditorUtility.SetDirty(this);
        }

        // Cache = Save
        private void UpdateCacheForDirection(NeighbourDirection direction, Tile[] value)
        {
            switch (direction)
            {
                case NeighbourDirection.North: prevNorth = Copy(value); break;
                case NeighbourDirection.South: prevSouth = Copy(value); break;
                case NeighbourDirection.West: prevWest = Copy(value); break;
                case NeighbourDirection.East: prevEast = Copy(value); break;
                case NeighbourDirection.Up: prevUp = Copy(value); break;
                case NeighbourDirection.Down: prevDown = Copy(value); break;
            }
        }

        public void CacheCurrent()
        {
            prevNorth = Copy(northNeighbours);
            prevSouth = Copy(southNeighbours);
            prevEast = Copy(eastNeighbours);
            prevWest = Copy(westNeighbours);
            prevUp = Copy(upNeighbours);
            prevDown = Copy(downNeighbours);
        }

        // Try to remove random empty neighbors that show up
        public void RemoveEmptyNeighbours()
        {
            bool wasApplyingReverse = applyReverse;
            applyReverse = false;

            northNeighbours = RemoveNulls(northNeighbours);
            southNeighbours = RemoveNulls(southNeighbours);
            eastNeighbours = RemoveNulls(eastNeighbours);
            westNeighbours = RemoveNulls(westNeighbours);
            upNeighbours = RemoveNulls(upNeighbours);
            downNeighbours = RemoveNulls(downNeighbours);

            CacheCurrent();

            applyReverse = wasApplyingReverse;

            UnityEditor.EditorUtility.SetDirty(this);
        }

        private Tile[] RemoveNulls(Tile[] array)
        {
            if (array == null) return null;

            List<Tile> clean = new List<Tile>();
            foreach (Tile t in array)
                if (t != null)
                    clean.Add(t);

            return clean.Count == 0 ? null : clean.ToArray();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0066:Convert switch statement to expression", Justification = "<More Readable>")]
        public Tile[] GetArrayByDirection(NeighbourDirection direction)
        {
            switch (direction)
            {
                case NeighbourDirection.North: return northNeighbours;
                case NeighbourDirection.South: return southNeighbours;
                case NeighbourDirection.West: return westNeighbours;
                case NeighbourDirection.East: return eastNeighbours;
                case NeighbourDirection.Up: return upNeighbours;
                case NeighbourDirection.Down: return downNeighbours;
                default: return null;
            }
        }

        public void SetArrayByDirection(NeighbourDirection direction, Tile[] value)
        {
            switch (direction)
            {
                case NeighbourDirection.North: northNeighbours = value; break;
                case NeighbourDirection.South: southNeighbours = value; break;
                case NeighbourDirection.West: westNeighbours = value; break;
                case NeighbourDirection.East: eastNeighbours = value; break;
                case NeighbourDirection.Up: upNeighbours = value; break;
                case NeighbourDirection.Down: downNeighbours = value; break;
            }
        }

        public Tile[] Append(Tile[] array, Tile tile)
        {
            if (array == null)
                return new[] { tile };

            Tile[] result = new Tile[array.Length + 1];
            array.CopyTo(result, 0);
            result[array.Length] = tile;
            return result;
        }

        public Tile[] Remove(Tile[] array, Tile tile)
        {
            if (array == null)
                return null;

            int count = 0;
            for (int i = 0; i < array.Length; i++)
                if (array[i] != tile)
                    count++;

            if (count == 0)
                return null;

            Tile[] result = new Tile[count];
            int index = 0;

            for (int i = 0; i < array.Length; i++)
                if (array[i] != tile)
                    result[index++] = array[i];

            return result;
        }

        public Tile[] Copy(Tile[] source)
        {
            if (source == null)
                return null;

            Tile[] copy = new Tile[source.Length];
            source.CopyTo(copy, 0);
            return copy;
        }

        public bool Contains(Tile[] array, Tile tile)
        {
            if (array == null)
                return false;

            for (int i = 0; i < array.Length; i++)
                if (array[i] == tile)
                    return true;

            return false;
        }

#endif
        #endregion
    }
}