using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Komikan
{
    public enum Turn
    {
        Pangui,
        Trewas
    }

    public enum GameStatus
    {
        Playing,
        PanguiWins,
        TrewasWin
    }

    /// <summary>
    /// Central coordinator. Owns the <see cref="BoardGraph"/> and
    /// <see cref="GameState"/>, holds references to the renderer and the piece
    /// views, runs turn management (Pangui = human, Trewas = AI), applies moves
    /// and captures to the state, keeps the views in sync, and detects the two
    /// win conditions:
    ///   - Pangui wins when it has eaten 6 or more Trewas;
    ///   - the Trewas win when it is Pangui's turn and Pangui has no legal move.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const int TrewasToWin = 6;

        public BoardGraph Board { get; private set; }
        public GameState State { get; private set; }
        public Turn CurrentTurn { get; private set; }
        public GameStatus Status { get; private set; }

        private BoardRenderer _renderer;
        private PieceView _panguiView;
        private List<PieceView> _trewaViews;
        private TrewasAI _ai;

        // Maps a board node index to the Trewa view sitting there (null if none).
        private readonly Dictionary<int, PieceView> _trewaByNode = new Dictionary<int, PieceView>();

        /// <summary>Raised whenever turn, status, or eaten count may have changed.</summary>
        public event System.Action OnStateChanged;

        public int TrewasEaten => State != null ? State.TrewasEatenCount : 0;

        /// <summary>
        /// Wire up the manager with everything it needs. Called once by the
        /// bootstrap after it has created the views.
        /// </summary>
        public void Configure(
            BoardGraph board,
            BoardRenderer renderer,
            PieceView panguiView,
            List<PieceView> trewaViews,
            TrewasAI ai)
        {
            Board = board;
            _renderer = renderer;
            _panguiView = panguiView;
            _trewaViews = trewaViews;
            _ai = ai;
        }

        /// <summary>Start (or restart) a fresh game from the opening position.</summary>
        public void StartNewGame()
        {
            State = GameState.CreateInitial();
            CurrentTurn = Turn.Pangui;
            Status = GameStatus.Playing;
            SyncAllViews();
            CheckWinConditions();
            RaiseStateChanged();
        }

        /// <summary>Public restart used by the UI's "Reiniciar" button.</summary>
        public void Restart()
        {
            StopAllCoroutines();
            StartNewGame();
        }

        public bool IsPlaying => Status == GameStatus.Playing;
        public bool IsPanguiTurn => CurrentTurn == Turn.Pangui && IsPlaying;

        /// <summary>World position of a board node (delegates to the renderer).</summary>
        public Vector3 WorldPosOf(int nodeIndex) => _renderer.WorldPosOf(nodeIndex);

        /// <summary>The Trewa view currently on a node, or null.</summary>
        public PieceView TrewaViewAt(int nodeIndex)
        {
            return _trewaByNode.TryGetValue(nodeIndex, out PieceView view) ? view : null;
        }

        /// <summary>Toggle the highlight glow on Pangui's piece (used on selection).</summary>
        public void SetPanguiHighlight(bool on)
        {
            if (_panguiView != null)
            {
                _panguiView.SetHighlighted(on);
            }
        }

        /// <summary>Highlight a board node marker (used to show legal destinations).</summary>
        public void SetNodeHighlight(int nodeIndex, bool on)
        {
            _renderer.SetNodeHighlight(nodeIndex, on);
        }

        // --- Applying Pangui actions (called by HumanInputController) --------

        /// <summary>Apply a simple one-step move for Pangui, then end the turn.</summary>
        public void ApplyPanguiSimpleMove(int destinationNode)
        {
            if (!IsPanguiTurn)
            {
                return;
            }

            State.PanguiNode = destinationNode;
            _panguiView.SetNode(destinationNode, WorldPosOf(destinationNode));
            EndPanguiTurn();
        }

        /// <summary>
        /// Apply one capture jump: move Pangui to the landing node and remove the
        /// jumped Trewa (state + view), incrementing the eaten counter. This does
        /// NOT end the turn; the input controller checks for continuation jumps
        /// and decides when the chain is over.
        /// </summary>
        public void ApplyPanguiCaptureJump(CaptureJump jump)
        {
            if (!IsPanguiTurn)
            {
                return;
            }

            // Remove the captured Trewa from the state.
            State.TrewaNodes[jump.OverNode] = false;
            State.TrewasEatenCount += 1;

            // Hide the matching Trewa view.
            if (_trewaByNode.TryGetValue(jump.OverNode, out PieceView captured))
            {
                captured.SetVisible(false);
                _trewaByNode.Remove(jump.OverNode);
            }

            // Move Pangui to the landing node.
            State.PanguiNode = jump.LandingNode;
            _panguiView.SetNode(jump.LandingNode, WorldPosOf(jump.LandingNode));

            RaiseStateChanged();

            // Pangui may have just reached the win threshold mid-chain.
            CheckWinConditions();
        }

        /// <summary>
        /// Continuation capture jumps available from Pangui's current node on the
        /// current state. Empty list means the chain is finished.
        /// </summary>
        public List<CaptureJump> GetPanguiContinuationJumps()
        {
            return MoveRules.GetPanguiCaptureJumps(Board, State);
        }

        /// <summary>End Pangui's turn and hand control to the Trewas (unless won).</summary>
        public void EndPanguiTurn()
        {
            RaiseStateChanged();

            if (CheckWinConditions())
            {
                return;
            }

            CurrentTurn = Turn.Trewas;
            RaiseStateChanged();
            StartCoroutine(RunTrewasTurn());
        }

        // --- Trewas (AI) turn -----------------------------------------------

        private IEnumerator RunTrewasTurn()
        {
            // Small pause so the human can see the AI move happen.
            yield return new WaitForSeconds(0.45f);

            if (Status != GameStatus.Playing)
            {
                yield break;
            }

            if (_ai.TryPickMove(Board, State, out int fromNode, out int toNode))
            {
                ApplyTrewaMove(fromNode, toNode);
            }
            // If no move exists the Trewas simply pass; the real losing condition
            // for Pangui is being trapped, checked when control returns to it.

            CurrentTurn = Turn.Pangui;
            RaiseStateChanged();
            CheckWinConditions();
        }

        private void ApplyTrewaMove(int fromNode, int toNode)
        {
            State.TrewaNodes[fromNode] = false;
            State.TrewaNodes[toNode] = true;

            if (_trewaByNode.TryGetValue(fromNode, out PieceView view))
            {
                _trewaByNode.Remove(fromNode);
                view.SetNode(toNode, WorldPosOf(toNode));
                _trewaByNode[toNode] = view;
            }
        }

        // --- Win detection ---------------------------------------------------

        /// <summary>
        /// Evaluate both win conditions and update <see cref="Status"/>. Returns
        /// true if the game has ended.
        /// </summary>
        private bool CheckWinConditions()
        {
            if (Status != GameStatus.Playing)
            {
                return true;
            }

            if (State.TrewasEatenCount >= TrewasToWin)
            {
                Status = GameStatus.PanguiWins;
                RaiseStateChanged();
                return true;
            }

            // Trewas win only when it is Pangui's turn and Pangui is trapped.
            if (CurrentTurn == Turn.Pangui && !MoveRules.PanguiHasAnyMove(Board, State))
            {
                Status = GameStatus.TrewasWin;
                RaiseStateChanged();
                return true;
            }

            return false;
        }

        // --- View syncing ----------------------------------------------------

        private void SyncAllViews()
        {
            _trewaByNode.Clear();

            _panguiView.SetVisible(true);
            _panguiView.SetHighlighted(false);
            _panguiView.SetNode(State.PanguiNode, WorldPosOf(State.PanguiNode));

            // Reassign the pool of Trewa views to the occupied Trewa nodes.
            var occupied = new List<int>();
            for (int node = 0; node < BoardGraph.NodeCount; node++)
            {
                if (State.HasTrewa(node))
                {
                    occupied.Add(node);
                }
            }

            for (int i = 0; i < _trewaViews.Count; i++)
            {
                PieceView view = _trewaViews[i];
                view.SetHighlighted(false);
                if (i < occupied.Count)
                {
                    int node = occupied[i];
                    view.SetVisible(true);
                    view.SetNode(node, WorldPosOf(node));
                    _trewaByNode[node] = view;
                }
                else
                {
                    view.SetVisible(false);
                }
            }
        }

        private void RaiseStateChanged()
        {
            OnStateChanged?.Invoke();
        }
    }
}
