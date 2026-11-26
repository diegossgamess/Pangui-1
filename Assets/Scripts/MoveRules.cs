using System.Collections.Generic;
using UnityEngine;

namespace Komikan
{
    /// <summary>
    /// Mutable snapshot of a KOMIKAN game: where Pangui sits, which nodes hold
    /// Trewas, and how many Trewas have been eaten so far. Pure data, no scene
    /// dependency.
    /// </summary>
    public class GameState
    {
        /// <summary>Node index that Pangui (the puma) currently occupies.</summary>
        public int PanguiNode;

        /// <summary>
        /// Occupancy flags for the 12 Trewas (dogs), one slot per board node.
        /// <c>TrewaNodes[i] == true</c> means a Trewa sits on node <c>i</c>.
        /// </summary>
        public readonly bool[] TrewaNodes = new bool[BoardGraph.NodeCount];

        /// <summary>Number of Trewas Pangui has captured so far.</summary>
        public int TrewasEatenCount;

        /// <summary>Build the opening position (Pangui and 12 Trewas placed).</summary>
        public static GameState CreateInitial()
        {
            var state = new GameState
            {
                PanguiNode = BoardGraph.PanguiStartIndex,
                TrewasEatenCount = 0,
            };

            foreach (int index in BoardGraph.GetTrewaStartIndices())
            {
                state.TrewaNodes[index] = true;
            }

            return state;
        }

        /// <summary>True if a Trewa occupies the node.</summary>
        public bool HasTrewa(int index)
        {
            return index >= 0 && index < TrewaNodes.Length && TrewaNodes[index];
        }

        /// <summary>True if the node holds neither Pangui nor a Trewa.</summary>
        public bool IsEmpty(int index)
        {
            return index != PanguiNode && !HasTrewa(index);
        }

        /// <summary>Deep copy, used when exploring hypothetical move sequences.</summary>
        public GameState Clone()
        {
            var copy = new GameState
            {
                PanguiNode = PanguiNode,
                TrewasEatenCount = TrewasEatenCount,
            };
            System.Array.Copy(TrewaNodes, copy.TrewaNodes, TrewaNodes.Length);
            return copy;
        }
    }

    /// <summary>
    /// A single capture jump: Pangui leaps from <see cref="FromNode"/> over the
    /// Trewa on <see cref="OverNode"/> and lands on <see cref="LandingNode"/>.
    /// </summary>
    public readonly struct CaptureJump
    {
        public readonly int FromNode;
        public readonly int OverNode;
        public readonly int LandingNode;

        public CaptureJump(int fromNode, int overNode, int landingNode)
        {
            FromNode = fromNode;
            OverNode = overNode;
            LandingNode = landingNode;
        }
    }

    /// <summary>
    /// A full capture turn for Pangui: one or more chained jumps performed in a
    /// single turn. Direction may change between jumps.
    /// </summary>
    public class CaptureSequence
    {
        /// <summary>The jumps in order.</summary>
        public readonly List<CaptureJump> Jumps = new List<CaptureJump>();

        /// <summary>Node Pangui ends on after the whole sequence.</summary>
        public int EndNode => Jumps.Count > 0 ? Jumps[Jumps.Count - 1].LandingNode : -1;

        /// <summary>Number of Trewas this sequence captures.</summary>
        public int CaptureCount => Jumps.Count;

        public CaptureSequence Clone()
        {
            var copy = new CaptureSequence();
            copy.Jumps.AddRange(Jumps);
            return copy;
        }
    }

    /// <summary>
    /// Stateless rules engine for KOMIKAN. All methods take a
    /// <see cref="BoardGraph"/> and a <see cref="GameState"/> and compute the
    /// legal moves; they never mutate the passed-in state. Pure C#, no
    /// MonoBehaviour, so the rules can be reasoned about and unit tested.
    /// </summary>
    public static class MoveRules
    {
        /// <summary>
        /// Legal simple moves for Pangui: step one node along an existing line
        /// to an empty node. Returns the destination node indices.
        /// </summary>
        public static List<int> GetPanguiSimpleMoves(BoardGraph board, GameState state)
        {
            var moves = new List<int>();
            foreach (int neighbor in board.Neighbors(state.PanguiNode))
            {
                if (state.IsEmpty(neighbor))
                {
                    moves.Add(neighbor);
                }
            }
            return moves;
        }

        /// <summary>
        /// Legal single capture jumps available to Pangui from its current
        /// node: jump over an adjacent Trewa onto the empty node directly
        /// beyond, along the same straight alquerque line.
        /// </summary>
        public static List<CaptureJump> GetPanguiCaptureJumps(BoardGraph board, GameState state)
        {
            return GetCaptureJumpsFrom(board, state, state.PanguiNode);
        }

        /// <summary>
        /// Capture jumps available from an arbitrary node, given the current
        /// Trewa layout in <paramref name="state"/>. Used both for the first
        /// jump and for continuations of a chain.
        /// </summary>
        private static List<CaptureJump> GetCaptureJumpsFrom(BoardGraph board, GameState state, int fromNode)
        {
            var jumps = new List<CaptureJump>();
            Vector2Int fromCoord = board.GetCoord(fromNode);

            foreach (Vector2Int step in BoardGraph.StepDirections(fromCoord))
            {
                if (!board.TryGetJumpLanding(fromNode, step, out int over, out int landing))
                {
                    continue;
                }

                // Must jump over a Trewa and land on an empty node.
                if (state.HasTrewa(over) && IsLandingClear(state, landing, fromNode))
                {
                    jumps.Add(new CaptureJump(fromNode, over, landing));
                }
            }

            return jumps;
        }

        // The landing node must be free of Trewas, and either genuinely empty or
        // the node Pangui is currently vacating during a chain (the start of the
        // whole sequence). During chaining Pangui leaves its origin, so that
        // origin counts as clear.
        private static bool IsLandingClear(GameState state, int landing, int chainOrigin)
        {
            if (state.HasTrewa(landing))
            {
                return false;
            }

            return landing != state.PanguiNode || landing == chainOrigin;
        }

        /// <summary>
        /// All maximal chained-capture sequences for Pangui. Each sequence is a
        /// run of jumps performed in a single turn; the direction may change
        /// between jumps. A jumped Trewa is removed before the next jump, and
        /// the same Trewa cannot be jumped twice in one sequence.
        /// </summary>
        public static List<CaptureSequence> GetPanguiCaptureSequences(BoardGraph board, GameState state)
        {
            var results = new List<CaptureSequence>();
            var working = state.Clone();
            ExploreCaptures(board, working, state.PanguiNode, new CaptureSequence(), results);
            return results;
        }

        private static void ExploreCaptures(
            BoardGraph board,
            GameState working,
            int currentNode,
            CaptureSequence sequenceSoFar,
            List<CaptureSequence> results)
        {
            // Jump candidates from the current node given Trewas removed so far.
            var candidates = GetCaptureJumpsFromDuringChain(board, working, currentNode);

            if (candidates.Count == 0)
            {
                // Dead end: record the sequence if at least one jump happened.
                if (sequenceSoFar.Jumps.Count > 0)
                {
                    results.Add(sequenceSoFar.Clone());
                }
                return;
            }

            foreach (CaptureJump jump in candidates)
            {
                // Apply the jump on a scratch copy so siblings are independent.
                working.TrewaNodes[jump.OverNode] = false;

                sequenceSoFar.Jumps.Add(jump);
                ExploreCaptures(board, working, jump.LandingNode, sequenceSoFar, results);
                sequenceSoFar.Jumps.RemoveAt(sequenceSoFar.Jumps.Count - 1);

                // Undo the removal for the next sibling branch.
                working.TrewaNodes[jump.OverNode] = true;
            }
        }

        // Capture candidates during a chain: landing must be empty with respect
        // to the Trewas still on the board and must not be Pangui's live node,
        // except it may be the chain's original start node (now vacated).
        private static List<CaptureJump> GetCaptureJumpsFromDuringChain(BoardGraph board, GameState working, int fromNode)
        {
            var jumps = new List<CaptureJump>();
            Vector2Int fromCoord = board.GetCoord(fromNode);

            foreach (Vector2Int step in BoardGraph.StepDirections(fromCoord))
            {
                if (!board.TryGetJumpLanding(fromNode, step, out int over, out int landing))
                {
                    continue;
                }

                if (!working.HasTrewa(over))
                {
                    continue;
                }

                // Landing is clear if no Trewa sits there. Pangui's own node is
                // the current fromNode during a chain and is being vacated, so a
                // landing equal to a previously occupied node is allowed as long
                // as no Trewa holds it.
                if (!working.HasTrewa(landing))
                {
                    jumps.Add(new CaptureJump(fromNode, over, landing));
                }
            }

            return jumps;
        }

        /// <summary>
        /// Legal simple moves for a single Trewa sitting on <paramref name="trewaNode"/>:
        /// step one node along an existing line to an empty node. Trewas never
        /// capture.
        /// </summary>
        public static List<int> GetTrewaSimpleMoves(BoardGraph board, GameState state, int trewaNode)
        {
            var moves = new List<int>();
            if (!state.HasTrewa(trewaNode))
            {
                return moves;
            }

            foreach (int neighbor in board.Neighbors(trewaNode))
            {
                if (state.IsEmpty(neighbor))
                {
                    moves.Add(neighbor);
                }
            }
            return moves;
        }

        /// <summary>
        /// True if Pangui has any legal action: either a simple move or at least
        /// one capture. When this is false on Pangui's turn, the Trewas win.
        /// </summary>
        public static bool PanguiHasAnyMove(BoardGraph board, GameState state)
        {
            if (GetPanguiCaptureJumps(board, state).Count > 0)
            {
                return true;
            }

            return GetPanguiSimpleMoves(board, state).Count > 0;
        }
    }
}
