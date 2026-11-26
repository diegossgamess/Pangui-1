using System.Collections.Generic;
using UnityEngine;

namespace Komikan
{
    /// <summary>
    /// EASY artificial intelligence for the Trewas (the dogs), the side the
    /// computer plays.
    ///
    /// Heuristic (deliberately simple and beatable, NOT a search):
    ///  1. Collect every legal simple move for every Trewa (from the rules core).
    ///  2. Give each candidate move a small score:
    ///       - a base random jitter so play stays varied and not optimal;
    ///       - a light bonus when the move reduces the grid (Manhattan) distance
    ///         from the moving Trewa to Pangui, nudging the pack to surround it;
    ///       - a small penalty, applied only SOMETIMES (a coin flip), when the
    ///         destination would let Pangui immediately jump that Trewa. This is
    ///         a shallow one-ply safety check, not a lookahead, so the AI still
    ///         makes mistakes and the human can win.
    ///  3. Pick the highest-scoring candidate.
    ///
    /// There is no minimax and no multi-step planning on purpose: the user asked
    /// for an easy AI.
    /// </summary>
    public class TrewasAI
    {
        private readonly System.Random _random;

        public TrewasAI(int seed)
        {
            _random = new System.Random(seed);
        }

        public TrewasAI() : this(System.Environment.TickCount)
        {
        }

        /// <summary>
        /// Pick one Trewa move. Returns false when the Trewas have no legal move
        /// at all (the caller then just skips their turn).
        /// </summary>
        public bool TryPickMove(BoardGraph board, GameState state, out int fromNode, out int toNode)
        {
            fromNode = -1;
            toNode = -1;

            var candidates = new List<(int from, int to)>();
            for (int node = 0; node < BoardGraph.NodeCount; node++)
            {
                if (!state.HasTrewa(node))
                {
                    continue;
                }

                foreach (int dest in MoveRules.GetTrewaSimpleMoves(board, state, node))
                {
                    candidates.Add((node, dest));
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            Vector2Int panguiCoord = board.GetCoord(state.PanguiNode);
            float bestScore = float.NegativeInfinity;
            int bestIndex = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                (int from, int to) = candidates[i];
                float score = (float)_random.NextDouble(); // base jitter in [0,1)

                int distBefore = ManhattanDistance(board.GetCoord(from), panguiCoord);
                int distAfter = ManhattanDistance(board.GetCoord(to), panguiCoord);
                if (distAfter < distBefore)
                {
                    score += 0.6f; // light bias toward closing in on Pangui
                }

                // Shallow safety check, only sometimes, so mistakes still happen.
                if (_random.NextDouble() < 0.5 && LandingIsCapturable(board, state, from, to))
                {
                    score -= 0.8f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            (fromNode, toNode) = candidates[bestIndex];
            return true;
        }

        private static int ManhattanDistance(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        // One-ply check: if this Trewa moved to 'to', could Pangui capture it on
        // the very next turn? Uses a cloned state so the real state is untouched.
        private static bool LandingIsCapturable(BoardGraph board, GameState state, int from, int to)
        {
            GameState hypothetical = state.Clone();
            hypothetical.TrewaNodes[from] = false;
            hypothetical.TrewaNodes[to] = true;

            foreach (CaptureJump jump in MoveRules.GetPanguiCaptureJumps(board, hypothetical))
            {
                if (jump.OverNode == to)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
