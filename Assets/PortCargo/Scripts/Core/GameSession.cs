using System;
using System.Collections.Generic;

namespace PortCargo.Core
{
    [Serializable]
    public sealed class RunData
    {
        public string runId, levelId;
        public int levelRevision;
        public long stateVersion;
        public BoardSnapshot board;
        public List<BoardSnapshot> undo = new List<BoardSnapshot>();
        public bool expanded, freeHintUsed, adHintUsed;
        public int moveCount, undoCount, hintCount;
    }

    /// <summary>Owns one run. All external snapshots are copies, and every command binds run plus version.</summary>
    public sealed class GameSession
    {
        private RunData data;
        public LevelDefinition Level { get; }
        public string RunId => data.runId;
        public long Version => data.stateVersion;
        public int Capacity => data.expanded ? 4 : 3;
        public bool FreeHintUsed => data.freeHintUsed;
        public bool AdHintUsed => data.adHintUsed;
        public bool Busy { get; set; }
        public BoardSnapshot Board => data.board.Clone();
        public BoardStatus Status => GameRules.Status(Level, data.board, Capacity);
        public bool CanUndo => !Busy && data.undo.Count > 0 && Status != BoardStatus.Completed;

        public GameSession(LevelDefinition level, string runId)
        {
            Level = level;
            Restart(runId);
        }

        /// <summary>Restores a validated run including undo. Invalid history rejects the whole active run.</summary>
        public static GameSession Restore(LevelDefinition level, RunData saved)
        {
            LevelValidator.Require(saved != null && saved.levelId == level.Id && saved.levelRevision == level.Revision,
                "RUN_LEVEL", level.Id);
            LevelValidator.Require(!string.IsNullOrWhiteSpace(saved.runId) && saved.stateVersion >= 0 && saved.stateVersion < long.MaxValue,
                "RUN_ID_VERSION", level.Id);
            LevelValidator.Require(saved.undo != null && saved.undo.Count <= 36 && saved.moveCount >= 0 && saved.undoCount >= 0
                && saved.hintCount >= 0 && saved.moveCount >= saved.undo.Count, "RUN_COUNTS", level.Id);
            var capacity = saved.expanded ? 4 : 3;
            GameRules.Validate(level, saved.board, capacity);
            foreach (var board in saved.undo) GameRules.Validate(level, board, capacity);
            // A valid undo sequence must be reachable by one player move at every adjacent step.
            for (var i = 0; i < saved.undo.Count; i++)
            {
                var from = saved.undo[i];
                var to = i + 1 < saved.undo.Count ? saved.undo[i + 1] : saved.board;
                var reachable = false;
                for (var s = 0; s < level.Stacks.Count; s++)
                {
                    var id = GameRules.Top(level, from, s);
                    if (id == null) continue;
                    var move = GameRules.Apply(level, from, capacity, id);
                    if (move.Accepted && move.Board.ExactKey() == to.ExactKey()) { reachable = true; break; }
                }
                LevelValidator.Require(reachable, "RUN_HISTORY", level.Id);
            }
            var session = new GameSession(level, saved.runId) { data = Copy(saved) };
            return session;
        }

        public RunData Export() => Copy(data);

        /// <summary>Rejects stale/foreign commands before changing board, history or counters.</summary>
        public MoveResult TryMove(string boxId, string expectedRunId, long expectedVersion)
        {
            var reason = Guard(expectedRunId, expectedVersion);
            if (reason != RejectReason.None) return new MoveResult(reason, Version, data.board);
            MoveResult move;
            try { move = GameRules.Apply(Level, data.board, Capacity, boxId); }
            catch (DataValidationException) { return new MoveResult(RejectReason.InvalidData, Version, data.board); }
            if (!move.Accepted) return new MoveResult(move.Reason, Version, data.board);
            data.undo.Add(data.board.Clone());
            data.board = move.Board.Clone();
            data.stateVersion++;
            data.moveCount++;
            return new MoveResult(RejectReason.None, Version, data.board, new List<DomainEvent>(move.Events));
        }

        /// <summary>Restores the full previous transaction, keeping hint and expansion entitlements.</summary>
        public bool TryUndo(string expectedRunId, long expectedVersion)
        {
            if (Guard(expectedRunId, expectedVersion) != RejectReason.None || !CanUndo) return false;
            data.board = data.undo[data.undo.Count - 1].Clone();
            data.undo.RemoveAt(data.undo.Count - 1);
            data.stateVersion++;
            data.undoCount++;
            GameRules.Validate(Level, data.board, Capacity);
            return true;
        }

        public void Restart(string newRunId)
        {
            LevelValidator.Require(!string.IsNullOrWhiteSpace(newRunId) && (data == null || data.runId != newRunId),
                "RUN_RESTART_ID", newRunId);
            data = new RunData { runId = newRunId, levelId = Level.Id, levelRevision = Level.Revision,
                board = GameRules.Initial(Level) };
            Busy = false;
        }

        public bool UseHint(string expectedRunId, long expectedVersion, string boxId, bool rewarded)
        {
            if (Guard(expectedRunId, expectedVersion) != RejectReason.None || Status == BoardStatus.Completed) return false;
            if (rewarded ? data.adHintUsed : data.freeHintUsed) return false;
            if (GameRules.CanMove(Level, data.board, Capacity, boxId) != RejectReason.None) return false;
            if (rewarded) data.adHintUsed = true; else data.freeHintUsed = true;
            data.hintCount++;
            return true;
        }

        public bool Expand(string expectedRunId, long expectedVersion)
        {
            if (Guard(expectedRunId, expectedVersion) != RejectReason.None || data.expanded || Status == BoardStatus.Completed) return false;
            data.expanded = true;
            data.stateVersion++;
            return true;
        }

        private RejectReason Guard(string runId, long version)
        {
            if (Busy) return RejectReason.Busy;
            if (runId != RunId) return RejectReason.WrongRun;
            if (version != Version) return RejectReason.StaleVersion;
            return RejectReason.None;
        }

        private static RunData Copy(RunData source)
        {
            var result = new RunData { runId = source.runId, levelId = source.levelId, levelRevision = source.levelRevision,
                stateVersion = source.stateVersion, board = source.board.Clone(), expanded = source.expanded,
                freeHintUsed = source.freeHintUsed, adHintUsed = source.adHintUsed, moveCount = source.moveCount,
                undoCount = source.undoCount, hintCount = source.hintCount };
            foreach (var board in source.undo) result.undo.Add(board.Clone());
            return result;
        }
    }
}
