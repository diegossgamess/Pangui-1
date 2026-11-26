using UnityEngine;

namespace Komikan
{
    /// <summary>
    /// Visual representation of a single piece (Pangui or one Trewa). Owns a
    /// <see cref="SpriteRenderer"/> and knows which board node it sits on. The
    /// piece can be highlighted (selected) to show the player which piece or
    /// which legal destinations are active.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PieceView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _baseColor = Color.white;
        private Vector3 _baseScale = Vector3.one;

        /// <summary>Board node index this piece currently occupies (-1 if none).</summary>
        public int NodeIndex { get; private set; } = -1;

        private void Awake()
        {
            EnsureRenderer();
        }

        private void EnsureRenderer()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
                if (_renderer == null)
                {
                    _renderer = gameObject.AddComponent<SpriteRenderer>();
                }
            }
        }

        /// <summary>
        /// Configure the piece's sprite, base colour, sorting order and the
        /// scale that "unhighlighted" returns to. Called once at setup.
        /// </summary>
        public void Initialize(Sprite sprite, Color baseColor, int sortingOrder, Vector3 scale)
        {
            EnsureRenderer();
            _renderer.sprite = sprite;
            _renderer.sortingOrder = sortingOrder;
            _baseColor = baseColor;
            _renderer.color = baseColor;
            _baseScale = scale;
            transform.localScale = scale;
        }

        /// <summary>Place the piece at a node and the matching world position.</summary>
        public void SetNode(int nodeIndex, Vector3 worldPosition)
        {
            NodeIndex = nodeIndex;
            transform.position = worldPosition;
        }

        /// <summary>Only move the world position (node stays the same).</summary>
        public void SetWorldPosition(Vector3 worldPosition)
        {
            transform.position = worldPosition;
        }

        /// <summary>Show or hide the piece (used when a Trewa is captured).</summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// Toggle the selected/highlight look: a brighter tint and a slightly
        /// larger scale so the active piece stands out.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            EnsureRenderer();
            if (highlighted)
            {
                _renderer.color = Color.Lerp(_baseColor, Color.white, 0.45f);
                transform.localScale = _baseScale * 1.2f;
            }
            else
            {
                _renderer.color = _baseColor;
                transform.localScale = _baseScale;
            }
        }
    }
}
