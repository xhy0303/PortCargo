using System;
using System.Collections.Generic;

namespace PortCargo.Core
{
    /// <summary>A complete level and a verified legal removal order. Persist Data with the run.</summary>
    public sealed class GeneratedLevel
    {
        public LevelData Data { get; internal set; }
        public int Seed { get; internal set; }
        public int GeneratorVersion { get; internal set; }
        public string[] Solution { get; internal set; }
        public bool UsedFallback { get; internal set; }
    }

    /// <summary>
    /// Bounded, platform-independent generation. The manifest is reordered through a three-box
    /// buffer, then its known legal pick order is distributed into stacks. Production rules replay
    /// that witness before returning. No time-based search or Unity/random global state is involved.
    /// </summary>
    public static class EndlessGenerator
    {
        public const int Version = 3;
        public const int RecentLimit = 128;
        private static readonly int[] RoundSizes = { 12, 15, 18, 21, 24, 15 };

        public static GeneratedLevel Generate(int seed, int round, ICollection<string> recentLayouts = null)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            var random = new StableRandom(unchecked((uint)seed ^ (uint)round * 0x85EBCA6Bu));
            GeneratedLevel best = null;
            var bestProbability = double.MaxValue;
            var target = round < 20 ? .03 : round < 50 ? .005 : .001;
            // Fixed attempts/state bounds, independent of machine speed. Every candidate is
            // constructed afresh; no campaign/template selection or cosmetic-only shuffle.
            for (var attempt = 0; attempt < 32; attempt++)
            {
                var next = GenerateCandidate(attempt == 0 ? seed : random.Next(int.MaxValue), round);
                if (recentLayouts != null && recentLayouts.Contains(LayoutSignature(next.Data))) continue;
                next.Seed = seed;
                if (round < 7) return next;
                var analysis = new EndlessLayoutAnalysis(next.Data);
                if (analysis.CanSolve(2)) continue;
                var probability = analysis.WinProbability();
                if (probability < bestProbability) { best = next; bestProbability = probability; }
                if (probability <= target) return next;
            }
            if (best != null) return best;
            // Rare fallback is also a fresh random puzzle, with a structural three-slot gate
            // and a replayed witness. It never selects an old template or resets the tier.
            for (var attempt = 0; attempt < 256; attempt++)
            {
                var next = round < 7 ? GenerateCandidate(random.Next(int.MaxValue), round)
                    : CreateGated(ref random, seed, round);
                next.Seed = seed;
                if (recentLayouts == null || !recentLayouts.Contains(LayoutSignature(next.Data))) return next;
            }
            throw new InvalidOperationException("No unseen endless layout within bounded generation attempts");
        }

        // Canonicalize colors by first occurrence in the manifest and ignore stack positions.
        // Thus swapping columns or renaming all colors does not disguise a repeat.
        public static string LayoutSignature(LevelData data)
        {
            var colors = new Dictionary<string, int>();
            var manifest = new System.Text.StringBuilder();
            foreach (var ship in data.ships)
            {
                if (!colors.ContainsKey(ship.type)) colors.Add(ship.type, colors.Count);
                manifest.Append(colors[ship.type]).Append(':').Append(ship.capacity).Append('/');
            }
            var stacks = new List<string>();
            foreach (var stack in data.stacks)
            {
                var text = new System.Text.StringBuilder();
                foreach (var box in stack.boxes) text.Append(colors[box.type]);
                stacks.Add(text.ToString());
            }
            stacks.Sort(StringComparer.Ordinal);
            return SaveRepository.Hash(manifest + "|" + string.Join("/", stacks));
        }

        private static GeneratedLevel CreateGated(ref StableRandom random, int seed, int round)
        {
            var total = round < 20 ? 24 : round < 50 ? 30 : 36;
            // Y appears only in the first ship and underneath three other cargo boxes.
            // Every solution must remove those three before any Y can be reached.
            var suffixShips = CreateManifest(ref random, total - 3, 3);
            var suffix = CreatePickOrder(ref random, suffixShips, 3);
            var ships = new ShipData[suffixShips.Length + 1];
            ships[0] = new ShipData { id = "E01-V0", type = "Y", capacity = 3 };
            Array.Copy(suffixShips, 0, ships, 1, suffixShips.Length);
            var stacks = new List<BoxData>[6];
            for (var s = 0; s < 6; s++) stacks[s] = new List<BoxData>();
            var sequence = new List<string>();
            for (var p = 0; p < total; p++)
            {
                var type = p < 3 ? suffix[p] : p < 6 ? "Y" : suffix[p - 3];
                var available = new List<int>();
                for (var s = 1; s < 6; s++) if (stacks[s].Count < 6) available.Add(s);
                var chosen = p < 6 ? 0 : available[random.Next(available.Count)];
                var box = new BoxData { id = "E01-B" + p, type = type };
                stacks[chosen].Add(box); sequence.Add(box.id);
            }
            var data = new LevelData { id = "E01", schemaVersion = 1, revision = Version,
                bufferCapacity = 3, ships = ships, stacks = new StackData[6] };
            // Move late picks into empty columns; this cannot block the known pick order.
            for (var s = 1; s < 6; s++) if (stacks[s].Count == 0)
            {
                var donor = 1;
                for (var d = 2; d < 6; d++) if (stacks[d].Count > stacks[donor].Count) donor = d;
                stacks[s].Add(stacks[donor][stacks[donor].Count - 1]);
                stacks[donor].RemoveAt(stacks[donor].Count - 1);
            }
            for (var s = 0; s < 6; s++)
            {
                stacks[s].Reverse();
                data.stacks[s] = new StackData { id = "S" + (s + 1), boxes = stacks[s].ToArray() };
            }
            var result = new GeneratedLevel { Data = data, Seed = seed, GeneratorVersion = Version,
                Solution = sequence.ToArray(), UsedFallback = true };
            if (!Verify(result)) throw new InvalidOperationException("Random gated witness failed replay");
            return result;
        }

        public static GeneratedLevel GenerateCandidate(int seed, int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            var tier = round < 7 ? 0 : round < 20 ? 1 : round < 50 ? 2 : 3;
            var phase = tier == 0 ? Math.Min(round - 1, 4) : 3;
            var random = new StableRandom(unchecked((uint)seed ^ ((uint)round * 0x9E3779B9u) ^ 0xA341316Cu));
            var total = tier == 0 ? RoundSizes[phase] : tier == 1 ? 24 : tier == 2 ? 30 : 36;
            var colors = tier > 0 ? 4 : 3;
            var ships = CreateManifest(ref random, total, colors);
            var stacks = tier > 0 ? 6 : Math.Max((total + 5) / 6, Math.Min(6, 3 + phase % 4));
            GeneratedLevel candidate = null;
            var bestScore = int.MinValue;
            // A fixed number of inexpensive construction attempts favors buried matches and
            // mixed stacks without introducing search latency or machine-dependent results.
            for (var attempt = 0; attempt < (phase == 0 || phase == 5 ? 4 : 8); attempt++)
            {
                var attemptOrder = CreatePickOrder(ref random, ships, phase);
                var next = Pack(ref random, ships, attemptOrder, stacks, seed, phase);
                var score = LayoutScore(next.Data);
                if (score > bestScore) { candidate = next; bestScore = score; }
            }
            if (Verify(candidate)) return candidate;

            // Defensive fixed-size fallback: direct manifest order is always legal, regardless
            // of how it is distributed. This is only reached if a candidate witness is rejected.
            var order = new List<string>();
            foreach (var ship in ships)
                for (var i = 0; i < ship.capacity; i++) order.Add(ship.type);
            var fallback = Pack(ref random, ships, order, stacks, seed, 0);
            fallback.UsedFallback = true;
            if (!Verify(fallback)) throw new InvalidOperationException("Endless fallback failed production-rule replay");
            return fallback;
        }

        private static int LayoutScore(LevelData data)
        {
            var firstColor = data.ships[0].type;
            var firstDepth = 6;
            var changes = 0;
            foreach (var stack in data.stacks)
            {
                for (var b = stack.boxes.Length - 1; b >= 0; b--)
                {
                    if (stack.boxes[b].type == firstColor) firstDepth = Math.Min(firstDepth, stack.boxes.Length - 1 - b);
                    if (b > 0 && stack.boxes[b].type != stack.boxes[b - 1].type) changes++;
                }
            }
            return 20 * firstDepth + changes;
        }

        private static ShipData[] CreateManifest(ref StableRandom random, int total, int colorCount)
        {
            var palette = new[] { "R", "B", "G", "Y" };
            for (var i = colorCount - 1; i > 0; i--)
            {
                var other = random.Next(i + 1);
                var swap = palette[i]; palette[i] = palette[other]; palette[other] = swap;
            }
            // Short and long cargo orders alternate; each palette color appears at least once.
            var count = Math.Min(12, Math.Max(colorCount + 1, (total + 2) / 3));
            var capacities = new int[count];
            for (var i = 0; i < count; i++) capacities[i] = 2;
            for (var remaining = total - count * 2; remaining > 0; remaining--)
            {
                var index = random.Next(count);
                while (capacities[index] >= 5) index = (index + 1) % count;
                capacities[index]++;
            }
            var ships = new ShipData[count];
            for (var i = 0; i < count; i++)
            {
                var color = i < colorCount ? i : random.Next(colorCount);
                if (i > 0 && palette[color] == ships[i - 1].type) color = (color + 1) % colorCount;
                ships[i] = new ShipData { id = "E01-V" + (i + 1), type = palette[color], capacity = capacities[i] };
            }
            return ships;
        }

        private static List<string> CreatePickOrder(ref StableRandom random, ShipData[] ships, int phase)
        {
            var remaining = new Dictionary<string, int> { { "R", 0 }, { "B", 0 }, { "G", 0 }, { "Y", 0 } };
            foreach (var ship in ships) remaining[ship.type] += ship.capacity;
            var result = new List<string>();
            var buffer = new List<string>();
            var shipIndex = 0;
            var load = 0;
            var limit = phase == 0 || phase == 5 ? 2 : 3;
            while (shipIndex < ships.Length)
            {
                var current = ships[shipIndex].type;
                var options = new List<string>();
                if (buffer.Count < limit)
                {
                    // Prefetch only colors in the next two orders, keeping the early rounds readable.
                    for (var i = shipIndex + 1; i < Math.Min(ships.Length, shipIndex + 3); i++)
                    {
                        var color = ships[i].type;
                        if (color != current && remaining[color] > 0 && !options.Contains(color)) options.Add(color);
                    }
                }
                var prefetch = options.Count > 0 && random.Next(100) < (phase == 0 || phase == 5 ? 32 : 57);
                var pick = prefetch ? options[random.Next(options.Count)] : current;
                if (remaining[pick] <= 0) throw new InvalidOperationException("Manifest conservation failed");
                remaining[pick]--;
                result.Add(pick);
                if (pick == current) load++; else buffer.Add(pick);
                while (shipIndex < ships.Length)
                {
                    if (load == ships[shipIndex].capacity) { shipIndex++; load = 0; continue; }
                    var buffered = buffer.IndexOf(ships[shipIndex].type);
                    if (buffered < 0) break;
                    buffer.RemoveAt(buffered); load++;
                }
            }
            if (buffer.Count != 0) throw new InvalidOperationException("Unconsumed generated cargo");
            return result;
        }

        private static GeneratedLevel Pack(ref StableRandom random, ShipData[] ships, List<string> order,
            int stackCount, int seed, int phase)
        {
            var stacks = new List<BoxData>[stackCount];
            for (var i = 0; i < stackCount; i++) stacks[i] = new List<BoxData>();
            var sequence = new string[order.Count];
            var previous = random.Next(stackCount);
            for (var pick = 0; pick < order.Count; pick++)
            {
                var available = new List<int>();
                var min = 6;
                for (var s = 0; s < stacks.Length; s++) min = Math.Min(min, stacks[s].Count);
                for (var s = 0; s < stacks.Length; s++)
                    if (stacks[s].Count < 6 && (stacks[s].Count <= min + 1 || phase == 3)) available.Add(s);
                // Occasionally keep two consecutive picks in a stack, creating buried matches.
                var chosen = available.Contains(previous) && phase > 0 && random.Next(100) < 35
                    ? previous : available[random.Next(available.Count)];
                var box = new BoxData { id = "E01-B" + (pick + 1), type = order[pick] };
                stacks[chosen].Add(box);
                sequence[pick] = box.id;
                previous = chosen;
            }
            // Avoid empty stacks even for the deliberately uneven distribution phase.
            for (var empty = 0; empty < stacks.Length; empty++)
            {
                if (stacks[empty].Count != 0) continue;
                var donor = 0;
                for (var s = 1; s < stacks.Length; s++) if (stacks[s].Count > stacks[donor].Count) donor = s;
                stacks[empty].Add(stacks[donor][stacks[donor].Count - 1]);
                stacks[donor].RemoveAt(stacks[donor].Count - 1);
            }
            var data = new LevelData { id = "E01", schemaVersion = 1, revision = Version,
                bufferCapacity = 3, ships = ships, stacks = new StackData[stackCount] };
            for (var s = 0; s < stacks.Length; s++)
            {
                stacks[s].Reverse();
                data.stacks[s] = new StackData { id = "S" + (s + 1), boxes = stacks[s].ToArray() };
            }
            return new GeneratedLevel { Data = data, Seed = seed, GeneratorVersion = Version,
                Solution = sequence, UsedFallback = false };
        }

        private static bool Verify(GeneratedLevel generated)
        {
            var level = new LevelDefinition(generated.Data);
            var board = GameRules.Initial(level);
            foreach (var box in generated.Solution)
            {
                var move = GameRules.Apply(level, board, 3, box);
                if (!move.Accepted) return false;
                board = move.Board;
            }
            return GameRules.Status(level, board, 3) == BoardStatus.Completed;
        }

        private struct StableRandom
        {
            private uint state;
            public StableRandom(uint seed) { state = seed == 0 ? 0x6D2B79F5u : seed; }
            public int Next(int exclusiveMax)
            {
                state ^= state << 13; state ^= state >> 17; state ^= state << 5;
                return (int)(state % (uint)exclusiveMax);
            }
        }
    }
}
