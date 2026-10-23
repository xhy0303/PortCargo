using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PortCargo.Core
{
    public enum SearchStatus { Searching, Found, Unsolvable, Unknown }

    public sealed class SearchBudget
    {
        public int MaxNodes { get; }
        public double WallMilliseconds { get; }
        public SearchBudget(int maxNodes, double wallMilliseconds)
        { MaxNodes = maxNodes; WallMilliseconds = wallMilliseconds; }
    }

    /// <summary>Incremental DFS. Only exhausted branches enter the failure cache; a timeout is never no-solution.</summary>
    public sealed class Solver : IDisposable
    {
        private sealed class Frame
        {
            public BoardSnapshot Board;
            public string Via;
            public int NextStack;
            public bool Entered;
            public int Key;
        }

        private readonly LevelDefinition level;
        private readonly int capacity;
        private readonly SearchBudget budget;
        private readonly Stopwatch wall = Stopwatch.StartNew();
        private readonly List<Frame> frames = new List<Frame>();
        private readonly HashSet<int> failed = new HashSet<int>();
        public SearchStatus Status { get; private set; } = SearchStatus.Searching;
        public int Nodes { get; private set; }
        public double ElapsedMilliseconds => wall.Elapsed.TotalMilliseconds;
        public string[] Solution { get; private set; } = Array.Empty<string>();
        public long SourceVersion { get; }

        public Solver(LevelDefinition level, BoardSnapshot board, int capacity, long sourceVersion, SearchBudget budget)
        {
            GameRules.Validate(level, board, capacity);
            this.level = level; this.capacity = capacity; this.budget = budget; SourceVersion = sourceVersion;
            frames.Add(new Frame { Board = board.Clone() });
        }

        /// <summary>Runs for at most a small slice; caller invokes again next frame while Searching.</summary>
        public SearchStatus Step(double sliceMilliseconds = 2)
        {
            var slice = Stopwatch.StartNew();
            while (Status == SearchStatus.Searching)
            {
                if (wall.Elapsed.TotalMilliseconds >= budget.WallMilliseconds) { Finish(SearchStatus.Unknown); break; }
                if (frames.Count == 0) { Finish(SearchStatus.Unsolvable); break; }
                if (slice.Elapsed.TotalMilliseconds >= sliceMilliseconds) break;
                VisitTop();
            }
            return Status;
        }

        private void VisitTop()
        {
            var frame = frames[frames.Count - 1];
            if (!frame.Entered)
            {
                if (GameRules.Status(level, frame.Board, capacity) == BoardStatus.Completed)
                {
                    var path = new List<string>();
                    for (var i = 1; i < frames.Count; i++) path.Add(frames[i].Via);
                    Solution = path.ToArray();
                    Finish(SearchStatus.Found);
                    return;
                }
                if (Nodes >= budget.MaxNodes) { Finish(SearchStatus.Unknown); return; }
                frame.Key = StateKey(frame.Board);
                if (failed.Contains(frame.Key)) { frames.RemoveAt(frames.Count - 1); return; }
                frame.Entered = true;
                Nodes++;
            }
            while (frame.NextStack < level.Stacks.Count)
            {
                var top = GameRules.Top(level, frame.Board, frame.NextStack++);
                if (top == null || GameRules.CanMove(level, frame.Board, capacity, top) != RejectReason.None) continue;
                var move = GameRules.Apply(level, frame.Board, capacity, top);
                frames.Add(new Frame { Board = move.Board, Via = top });
                return;
            }
            failed.Add(frame.Key);
            frames.RemoveAt(frames.Count - 1);
        }

        // For a fixed definition, removed counts determine the maximal satisfied ship prefix,
        // load and remaining buffer multiset. Buffer ordering cannot change the next legal types.
        // Six stacks of depth <=6 fit in 7^6 states; merge every equivalent pick permutation.
        private static int StateKey(BoardSnapshot board)
        {
            var key=0;
            foreach(var height in board.heights)key=key*7+height;
            return key;
        }

        public void Cancel() { if (Status == SearchStatus.Searching) Finish(SearchStatus.Unknown); }
        public void Dispose() { Cancel(); frames.Clear(); failed.Clear(); }
        private void Finish(SearchStatus status) { Status = status; wall.Stop(); frames.Clear(); failed.Clear(); }
    }
}
