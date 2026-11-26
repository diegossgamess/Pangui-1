using System.Collections.Generic;
using UnityEngine;

namespace Komikan
{
    /// <summary>
    /// Draws the KOMIKAN board using only lines and simple shapes (no art
    /// assets). It maps each graph node (col,row) to a world position centred on
    /// the origin, draws every graph edge exactly once as a line, and marks the
    /// montana (mountain, a triangle near the top, row 4) and the valle (valley,
    /// a square near the bottom, row 0). Small dot sprites mark the clickable
    /// nodes. Call <see cref="Build"/> once after construction.
    /// </summary>
    public class BoardRenderer : MonoBehaviour
    {
        /// <summary>World-space distance between adjacent grid nodes.</summary>
        public float Spacing { get; set; } = 1.4f;

        private BoardGraph _board;
        private readonly Vector3[] _nodePositions = new Vector3[BoardGraph.NodeCount];
        private readonly List<GameObject> _lineObjects = new List<GameObject>();
        private readonly List<GameObject> _nodeMarkers = new List<GameObject>();

        private static readonly Color LineColor = new Color(0.25f, 0.25f, 0.30f, 1f);
        private static readonly Color MontanaColor = new Color(0.55f, 0.40f, 0.25f, 1f);
        private static readonly Color ValleColor = new Color(0.25f, 0.55f, 0.30f, 1f);

        private Material _lineMaterial;
        private Sprite _nodeSprite;

        /// <summary>Build all board visuals for the given graph.</summary>
        public void Build(BoardGraph board)
        {
            _board = board;
            ComputeNodePositions();
            EnsureResources();
            DrawEdges();
            DrawNodeMarkers();
            DrawMontanaMarker();
            DrawValleMarker();
        }

        private void EnsureResources()
        {
            if (_lineMaterial == null)
            {
                // Built-in unlit colour shader is always available at runtime.
                _lineMaterial = new Material(Shader.Find("Sprites/Default"));
            }

            if (_nodeSprite == null)
            {
                _nodeSprite = SpriteFactory.CreateCircleSprite(SpriteFactory.NodeColor, 48, 128f);
            }
        }

        private void ComputeNodePositions()
        {
            // Centre the 5x5 grid on the origin.
            float offset = (BoardGraph.Size - 1) * 0.5f * Spacing;
            for (int i = 0; i < BoardGraph.NodeCount; i++)
            {
                Vector2Int c = BoardGraph.IndexToCoord(i);
                float x = c.x * Spacing - offset;
                float y = c.y * Spacing - offset;
                _nodePositions[i] = new Vector3(x, y, 0f);
            }
        }

        /// <summary>World position of a node index.</summary>
        public Vector3 WorldPosOf(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= BoardGraph.NodeCount)
            {
                return Vector3.zero;
            }
            return _nodePositions[nodeIndex];
        }

        private void DrawEdges()
        {
            // Draw each undirected edge once by only emitting a->b when a < b.
            for (int a = 0; a < BoardGraph.NodeCount; a++)
            {
                foreach (int b in _board.Neighbors(a))
                {
                    if (a < b)
                    {
                        CreateLine($"Edge_{a}_{b}", _nodePositions[a], _nodePositions[b], LineColor, 0.05f, -1);
                    }
                }
            }
        }

        private readonly SpriteRenderer[] _nodeRenderers = new SpriteRenderer[BoardGraph.NodeCount];
        private static readonly Color HighlightColor = new Color(0.98f, 0.92f, 0.35f, 1f);
        private float _normalMarkerScale;
        private float _highlightMarkerScale;

        private void DrawNodeMarkers()
        {
            _normalMarkerScale = Spacing * 0.22f;
            _highlightMarkerScale = Spacing * 0.38f;

            for (int i = 0; i < BoardGraph.NodeCount; i++)
            {
                var go = new GameObject($"Node_{i}");
                go.transform.SetParent(transform, false);
                go.transform.position = _nodePositions[i];
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _nodeSprite;
                sr.color = SpriteFactory.NodeColor;
                sr.sortingOrder = 0;
                go.transform.localScale = Vector3.one * _normalMarkerScale;
                _nodeMarkers.Add(go);
                _nodeRenderers[i] = sr;
            }
        }

        /// <summary>
        /// Highlight (or restore) a board node marker so the player can see a
        /// legal destination even when the node is empty.
        /// </summary>
        public void SetNodeHighlight(int nodeIndex, bool on)
        {
            if (nodeIndex < 0 || nodeIndex >= BoardGraph.NodeCount)
            {
                return;
            }

            SpriteRenderer sr = _nodeRenderers[nodeIndex];
            if (sr == null)
            {
                return;
            }

            sr.color = on ? HighlightColor : SpriteFactory.NodeColor;
            sr.transform.localScale = Vector3.one * (on ? _highlightMarkerScale : _normalMarkerScale);
        }

        /// <summary>Restore every node marker to its normal look.</summary>
        public void ClearAllNodeHighlights()
        {
            for (int i = 0; i < BoardGraph.NodeCount; i++)
            {
                SetNodeHighlight(i, false);
            }
        }

        // Triangle outline near the top row (montana / mountain).
        private void DrawMontanaMarker()
        {
            float topY = _nodePositions[BoardGraph.CoordToIndex(2, BoardGraph.Size - 1)].y + Spacing * 0.9f;
            float half = Spacing * 0.6f;
            var apex = new Vector3(0f, topY + half, 0f);
            var left = new Vector3(-half, topY - half * 0.6f, 0f);
            var right = new Vector3(half, topY - half * 0.6f, 0f);

            CreateLine("Montana_1", left, apex, MontanaColor, 0.08f, -2);
            CreateLine("Montana_2", apex, right, MontanaColor, 0.08f, -2);
            CreateLine("Montana_3", right, left, MontanaColor, 0.08f, -2);
        }

        // Square outline near the bottom row (valle / valley).
        private void DrawValleMarker()
        {
            float bottomY = _nodePositions[BoardGraph.CoordToIndex(2, 0)].y - Spacing * 0.9f;
            float half = Spacing * 0.45f;
            var bl = new Vector3(-half, bottomY - half, 0f);
            var br = new Vector3(half, bottomY - half, 0f);
            var tr = new Vector3(half, bottomY + half, 0f);
            var tl = new Vector3(-half, bottomY + half, 0f);

            CreateLine("Valle_1", bl, br, ValleColor, 0.08f, -2);
            CreateLine("Valle_2", br, tr, ValleColor, 0.08f, -2);
            CreateLine("Valle_3", tr, tl, ValleColor, 0.08f, -2);
            CreateLine("Valle_4", tl, bl, ValleColor, 0.08f, -2);
        }

        private void CreateLine(string lineName, Vector3 from, Vector3 to, Color color, float width, int sortingOrder)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCapVertices = 2;
            lr.material = _lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.sortingOrder = sortingOrder;
            _lineObjects.Add(go);
        }
    }
}
