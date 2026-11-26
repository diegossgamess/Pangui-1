using System.Collections.Generic;
using UnityEngine;

namespace Komikan
{
    /// <summary>
    /// Handles the human player's mouse input while it is Pangui's turn.
    ///
    /// Flow:
    ///  - A click is converted to the nearest board node within a small radius.
    ///  - Clicking Pangui's own node selects it and highlights every legal
    ///    destination: simple-move neighbours and first-jump capture landings.
    ///  - Clicking a highlighted simple destination moves Pangui and ends the turn.
    ///  - Clicking a highlighted capture landing performs that jump, removes the
    ///    Trewa, increments the eaten count, then recomputes continuation jumps
    ///    from the new landing node on the updated state. While continuations
    ///    exist the turn stays active and the player keeps choosing jumps
    ///    (direction changes allowed). When none remain the turn ends.
    /// </summary>
    public class HumanInputController : MonoBehaviour
    {
        private GameManager _game;
        private BoardGraph _board;
        private Camera _camera;

        private bool _pieceSelected;
        private readonly HashSet<int> _simpleTargets = new HashSet<int>();
        // Maps a highlighted landing node to the capture jump that reaches it.
        private readonly Dictionary<int, CaptureJump> _captureTargets = new Dictionary<int, CaptureJump>();
        private bool _inCaptureChain;

        /// <summary>Max world-space distance between a click and a node to count as a hit.</summary>
        public float ClickRadius { get; set; } = 0.55f;

        public void Configure(GameManager game, BoardGraph board, Camera cam)
        {
            _game = game;
            _board = board;
            _camera = cam;
        }

        private void Update()
        {
            if (_game == null || !_game.IsPanguiTurn)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandleClick();
            }
        }

        private void HandleClick()
        {
            if (!TryGetClickedNode(out int node))
            {
                // Clicking empty space deselects (only when not mid-chain).
                if (!_inCaptureChain)
                {
                    ClearSelection();
                }
                return;
            }

            // Mid-chain: only continuation landings are actionable.
            if (_inCaptureChain)
            {
                if (_captureTargets.TryGetValue(node, out CaptureJump chainJump))
                {
                    PerformCapture(chainJump);
                }
                return;
            }

            if (!_pieceSelected)
            {
                if (node == _game.State.PanguiNode)
                {
                    SelectPangui();
                }
                return;
            }

            // A piece is selected: resolve the clicked node against the targets.
            if (_captureTargets.TryGetValue(node, out CaptureJump jump))
            {
                PerformCapture(jump);
            }
            else if (_simpleTargets.Contains(node))
            {
                ClearSelection();
                _game.ApplyPanguiSimpleMove(node);
            }
            else if (node == _game.State.PanguiNode)
            {
                // Clicking Pangui again toggles the selection off.
                ClearSelection();
            }
            else
            {
                ClearSelection();
            }
        }

        private void SelectPangui()
        {
            _pieceSelected = true;
            HighlightPanguiPiece(true);
            RecomputeTargetsFromCurrentNode();
        }

        // Fill the simple and capture target sets from Pangui's current node.
        private void RecomputeTargetsFromCurrentNode()
        {
            ClearHighlights();
            _simpleTargets.Clear();
            _captureTargets.Clear();

            foreach (int dest in MoveRules.GetPanguiSimpleMoves(_board, _game.State))
            {
                _simpleTargets.Add(dest);
            }

            foreach (CaptureJump jump in MoveRules.GetPanguiCaptureJumps(_board, _game.State))
            {
                _captureTargets[jump.LandingNode] = jump;
            }

            ApplyTargetHighlights();
        }

        private void PerformCapture(CaptureJump jump)
        {
            _game.ApplyPanguiCaptureJump(jump);

            // The capture may have ended the game (6th Trewa eaten).
            if (!_game.IsPlaying)
            {
                ResetInputState();
                return;
            }

            // Recompute continuation jumps from the new landing node.
            List<CaptureJump> continuations = _game.GetPanguiContinuationJumps();
            if (continuations.Count > 0)
            {
                _inCaptureChain = true;
                _pieceSelected = true;
                HighlightPanguiPiece(true);

                ClearHighlights();
                _simpleTargets.Clear();
                _captureTargets.Clear();
                foreach (CaptureJump next in continuations)
                {
                    _captureTargets[next.LandingNode] = next;
                }
                ApplyTargetHighlights();
            }
            else
            {
                // Chain finished: end the turn.
                ResetInputState();
                _game.EndPanguiTurn();
            }
        }

        private bool TryGetClickedNode(out int node)
        {
            node = -1;
            Vector3 screen = Input.mousePosition;
            screen.z = Mathf.Abs(_camera.transform.position.z);
            Vector3 world = _camera.ScreenToWorldPoint(screen);
            world.z = 0f;

            float best = ClickRadius;
            for (int i = 0; i < BoardGraph.NodeCount; i++)
            {
                Vector3 pos = _game.WorldPosOf(i);
                float d = Vector2.Distance(new Vector2(world.x, world.y), new Vector2(pos.x, pos.y));
                if (d <= best)
                {
                    best = d;
                    node = i;
                }
            }

            return node >= 0;
        }

        private void ApplyTargetHighlights()
        {
            foreach (int node in _simpleTargets)
            {
                HighlightNode(node, true);
            }
            foreach (int node in _captureTargets.Keys)
            {
                HighlightNode(node, true);
            }
        }

        private void ClearHighlights()
        {
            foreach (int node in _simpleTargets)
            {
                HighlightNode(node, false);
            }
            foreach (int node in _captureTargets.Keys)
            {
                HighlightNode(node, false);
            }
        }

        // Highlight the board node marker so the player sees a legal destination
        // even though it is an empty node.
        private void HighlightNode(int node, bool on)
        {
            _game.SetNodeHighlight(node, on);
        }

        private void HighlightPanguiPiece(bool on)
        {
            // Pangui's own view is highlighted through the GameManager's piece.
            // We reach it via the node lookup only for Trewas, so toggle through
            // the manager's exposed Pangui highlight if available.
            _game.SetPanguiHighlight(on);
        }

        private void ClearSelection()
        {
            ClearHighlights();
            _simpleTargets.Clear();
            _captureTargets.Clear();
            _pieceSelected = false;
            HighlightPanguiPiece(false);
        }

        private void ResetInputState()
        {
            ClearHighlights();
            _simpleTargets.Clear();
            _captureTargets.Clear();
            _pieceSelected = false;
            _inCaptureChain = false;
            HighlightPanguiPiece(false);
        }
    }
}
