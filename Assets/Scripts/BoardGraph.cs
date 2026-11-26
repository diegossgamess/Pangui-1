using System.Collections.Generic;
using UnityEngine;

namespace Komikan
{
    /// <summary>
    /// Pure-C# model of the KOMIKAN board: a 5x5 alquerque grid of 25 nodes.
    ///
    /// Nodes are indexed 0..24. Each node has a (col, row) coordinate with
    /// col in 0..4 and row in 0..4. Row 0 is the valley (valle, bottom),
    /// where the Trewas start; row 4 is the mountain (montana, top), where
    /// Pangui starts.
    ///
    /// Adjacency:
    ///  - Orthogonal neighbours (up/down/left/right) exist for every node.
    ///  - Diagonal neighbours exist only from nodes where (col + row) is even,
    ///    which is the standard alquerque diagonal pattern.
    ///
    /// This class has no MonoBehaviour or scene dependency. It only uses
    /// <see cref="Vector2Int"/> as a lightweight coordinate value type, so the
    /// logic is self-contained and easy to review or unit test.
    /// </summary>
    public class BoardGraph
    {
        /// <summary>Grid side length (5x5 alquerque).</summary>
        public const int Size = 5;

        /// <summary>Total number of nodes on the board (25).</summary>
        public const int NodeCount = Size * Size;

        // Adjacency list: for each node index, the list of adjacent node indices.
        private readonly List<int>[] _adjacency;

        // Coordinate of each node, indexed by node index.
        private readonly Vector2Int[] _coords;

        public BoardGraph()
        {
            _adjacency = new List<int>[NodeCount];
            _coords = new Vector2Int[NodeCount];

            for (int i = 0; i < NodeCount; i++)
            {
                _adjacency[i] = new List<int>();
                _coords[i] = IndexToCoord(i);
            }

            BuildAdjacency();
        }

        // Candidate step directions. The first four are orthogonal; the last
        // four are diagonal. Diagonal steps are only valid from even-parity nodes.
        private static readonly Vector2Int[] OrthogonalSteps =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1),
        };

        private static readonly Vector2Int[] DiagonalSteps =
        {
            new Vector2Int(1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, -1),
        };

        private void BuildAdjacency()
        {
            for (int index = 0; index < NodeCount; index++)
            {
                Vector2Int c = _coords[index];

                foreach (Vector2Int step in OrthogonalSteps)
                {
                    TryAddEdge(index, c + step);
                }

                // Diagonals exist only where (col + row) is even.
                if (((c.x + c.y) & 1) == 0)
                {
                    foreach (Vector2Int step in DiagonalSteps)
                    {
                        TryAddEdge(index, c + step);
                    }
                }
            }
        }

        private void TryAddEdge(int fromIndex, Vector2Int toCoord)
        {
            if (!IsValidCoord(toCoord))
            {
                return;
            }

            int toIndex = CoordToIndex(toCoord);
            if (!_adjacency[fromIndex].Contains(toIndex))
            {
                _adjacency[fromIndex].Add(toIndex);
            }
        }

        /// <summary>True if the coordinate is inside the 5x5 grid.</summary>
        public static bool IsValidCoord(Vector2Int coord)
        {
            return coord.x >= 0 && coord.x < Size && coord.y >= 0 && coord.y < Size;
        }

        /// <summary>True if the node index is in range 0..24.</summary>
        public static bool IsValidIndex(int index)
        {
            return index >= 0 && index < NodeCount;
        }

        /// <summary>Convert a (col, row) coordinate into a node index.</summary>
        public static int CoordToIndex(Vector2Int coord)
        {
            return coord.y * Size + coord.x;
        }

        /// <summary>Convert a (col, row) coordinate into a node index.</summary>
        public static int CoordToIndex(int col, int row)
        {
            return row * Size + col;
        }

        /// <summary>Convert a node index into its (col, row) coordinate.</summary>
        public static Vector2Int IndexToCoord(int index)
        {
            return new Vector2Int(index % Size, index / Size);
        }

        /// <summary>Coordinate of the given node index.</summary>
        public Vector2Int GetCoord(int index)
        {
            return _coords[index];
        }

        /// <summary>
        /// Adjacent node indices for the given node. Returns the internal list;
        /// callers must not mutate it.
        /// </summary>
        public IReadOnlyList<int> Neighbors(int index)
        {
            return _adjacency[index];
        }

        /// <summary>True if <paramref name="b"/> is directly connected to <paramref name="a"/>.</summary>
        public bool AreAdjacent(int a, int b)
        {
            return _adjacency[a].Contains(b);
        }

        /// <summary>
        /// Resolve the "jump landing" for a capture. Given a start node and a
        /// step direction, the landing is the node two steps along that straight
        /// line, i.e. at coordinate (start + 2 * step).
        ///
        /// The jump is only valid when both legs of the line are real edges of
        /// the graph: start must be adjacent to the intermediate node
        /// (start + step), and the intermediate node must be adjacent to the
        /// landing node (start + 2 * step). This guarantees the whole jump runs
        /// along a single existing alquerque line (orthogonal, or diagonal only
        /// where such edges exist).
        /// </summary>
        /// <param name="fromIndex">Node the jump starts from (Pangui's node).</param>
        /// <param name="step">Unit-ish step direction (components in -1..1).</param>
        /// <param name="overIndex">The jumped-over (intermediate) node index.</param>
        /// <param name="landingIndex">The landing node index.</param>
        /// <returns>True if a valid in-line jump landing exists.</returns>
        public bool TryGetJumpLanding(int fromIndex, Vector2Int step, out int overIndex, out int landingIndex)
        {
            overIndex = -1;
            landingIndex = -1;

            Vector2Int from = _coords[fromIndex];
            Vector2Int overCoord = from + step;
            Vector2Int landingCoord = from + step + step;

            if (!IsValidCoord(overCoord) || !IsValidCoord(landingCoord))
            {
                return false;
            }

            int over = CoordToIndex(overCoord);
            int landing = CoordToIndex(landingCoord);

            // Both legs must be actual graph edges along the same straight line.
            if (!AreAdjacent(fromIndex, over) || !AreAdjacent(over, landing))
            {
                return false;
            }

            overIndex = over;
            landingIndex = landing;
            return true;
        }

        /// <summary>
        /// The step directions to test from a node: the four orthogonal steps
        /// always, plus the four diagonal steps when (col + row) is even.
        /// Used by the move/capture rules to enumerate lines out of a node.
        /// </summary>
        public static IEnumerable<Vector2Int> StepDirections(Vector2Int coord)
        {
            foreach (Vector2Int step in OrthogonalSteps)
            {
                yield return step;
            }

            if (((coord.x + coord.y) & 1) == 0)
            {
                foreach (Vector2Int step in DiagonalSteps)
                {
                    yield return step;
                }
            }
        }

        // --- Documented starting positions --------------------------------

        /// <summary>Pangui's starting node: coordinate (col=2, row=4).</summary>
        public static int PanguiStartIndex => CoordToIndex(2, 4);

        /// <summary>
        /// The 12 Trewa starting node indices: all of row 0 (5 nodes), all of
        /// row 1 (5 nodes), plus (1,2) and (3,2).
        /// </summary>
        public static IReadOnlyList<int> GetTrewaStartIndices()
        {
            var starts = new List<int>(12);

            for (int col = 0; col < Size; col++)
            {
                starts.Add(CoordToIndex(col, 0));
            }

            for (int col = 0; col < Size; col++)
            {
                starts.Add(CoordToIndex(col, 1));
            }

            starts.Add(CoordToIndex(1, 2));
            starts.Add(CoordToIndex(3, 2));

            return starts;
        }
    }
}
