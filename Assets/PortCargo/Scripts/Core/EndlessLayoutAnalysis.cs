using System;
using System.Collections.Generic;

namespace PortCargo.Core
{
    /// <summary>
    /// Exact finite DAG analysis for generation only. Removed counts determine the normalized
    /// manifest prefix and buffer size. At most 7^6 height states; no wall-clock cutoff.
    /// Production GameRules still independently replays every returned solution.
    /// </summary>
    public sealed class EndlessLayoutAnalysis
    {
        private readonly int[][] stacks;
        private readonly int[][][] remaining;
        private readonly int[] types, loads, heights;
        private readonly int[] totals = new int[4];
        private readonly Dictionary<int, double> memo = new Dictionary<int, double>();
        private int capacity;
        private bool stopAtWin;

        public EndlessLayoutAnalysis(LevelData data)
        {
            // Validate bounds and cargo totals before relying on a base-7 state key.
            new LevelDefinition(data);
            const string palette = "RBGY";
            stacks = new int[data.stacks.Length][];
            remaining = new int[stacks.Length][][];
            heights = new int[stacks.Length];
            for (var s = 0; s < stacks.Length; s++)
            {
                var boxes = data.stacks[s].boxes;
                stacks[s] = new int[boxes.Length]; heights[s] = boxes.Length;
                remaining[s] = new int[boxes.Length + 1][];
                remaining[s][0] = new int[4];
                for (var b = 0; b < boxes.Length; b++)
                {
                    var color = palette.IndexOf(boxes[b].type);
                    stacks[s][b] = color; totals[color]++;
                    remaining[s][b + 1] = (int[])remaining[s][b].Clone();
                    remaining[s][b + 1][color]++;
                }
            }
            types = new int[data.ships.Length]; loads = new int[types.Length];
            for (var i = 0; i < types.Length; i++)
            { types[i] = palette.IndexOf(data.ships[i].type); loads[i] = data.ships[i].capacity; }
        }

        public bool CanSolve(int bufferCapacity)
        { return Analyze(bufferCapacity, true) > 0; }
        public double WinProbability() { return Analyze(3, false); }
        private double Analyze(int bufferCapacity, bool firstWin)
        {
            capacity = bufferCapacity; stopAtWin = firstWin; memo.Clear();
            return Visit();
        }

        private double Visit()
        {
            var key = 0;
            for (var s = 0; s < heights.Length; s++) key = key * 7 + heights[s];
            if (memo.TryGetValue(key, out var cached)) return cached;
            var a = totals[0]; var b = totals[1]; var c = totals[2]; var d = totals[3];
            for (var s = 0; s < heights.Length; s++)
            {
                var counts = remaining[s][heights[s]];
                a -= counts[0]; b -= counts[1]; c -= counts[2]; d -= counts[3];
            }
            var ship = 0;
            while (ship < types.Length)
            {
                var available = types[ship] == 0 ? a : types[ship] == 1 ? b : types[ship] == 2 ? c : d;
                if (available < loads[ship]) break;
                if (types[ship] == 0) a -= loads[ship];
                else if (types[ship] == 1) b -= loads[ship];
                else if (types[ship] == 2) c -= loads[ship]; else d -= loads[ship];
                ship++;
            }
            if (ship == types.Length) return 1;
            var current = types[ship];
            var load = current == 0 ? a : current == 1 ? b : current == 2 ? c : d;
            var buffer = a + b + c + d - load;
            var legal = 0; double sum = 0;
            for (var s = 0; s < heights.Length; s++)
            {
                if (heights[s] == 0 || (stacks[s][heights[s] - 1] != current && buffer >= capacity)) continue;
                legal++; heights[s]--; var probability = Visit(); heights[s]++;
                sum += probability;
                if (stopAtWin && probability > 0) { memo[key] = 1; return 1; }
            }
            var value = legal == 0 ? 0 : sum / legal;
            memo[key] = value; return value;
        }
    }
}
