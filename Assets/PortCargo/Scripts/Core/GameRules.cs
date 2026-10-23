using System;
using System.Collections.Generic;

namespace PortCargo.Core
{
    /// <summary>Deterministic transactional rules shared by the game, solver and regression replay.</summary>
    public static class GameRules
    {
        public static BoardSnapshot Initial(LevelDefinition level)
        {
            var board = new BoardSnapshot { heights = new int[level.Stacks.Count] };
            for (var i = 0; i < board.heights.Length; i++) board.heights[i] = level.Stacks[i].Boxes.Count;
            return board;
        }

        public static string Top(LevelDefinition level, BoardSnapshot board, int stack)
        {
            return board.heights[stack] == 0 ? null : level.Stacks[stack].Boxes[board.heights[stack] - 1].Id;
        }

        public static RejectReason CanMove(LevelDefinition level, BoardSnapshot board, int capacity, string boxId)
        {
            if (board.shipIndex >= level.Ships.Count) return RejectReason.AlreadyCompleted;
            if (!level.TryGetBox(boxId, out var box)) return RejectReason.UnknownBox;
            var isTop = false;
            for (var i = 0; i < board.heights.Length; i++) if (Top(level, board, i) == boxId) isTop = true;
            if (!isTop) return RejectReason.NotTop;
            return box.Type == level.Ships[board.shipIndex].Type || board.buffer.Count < capacity
                ? RejectReason.None : RejectReason.BufferFull;
        }

        /// <summary>Works on a clone; every rejection leaves the caller's board untouched.</summary>
        public static MoveResult Apply(LevelDefinition level, BoardSnapshot source, int capacity, string boxId)
        {
            Validate(level, source, capacity);
            var reason = CanMove(level, source, capacity, boxId);
            if (reason != RejectReason.None) return new MoveResult(reason, 0, source);
            var board = source.Clone();
            var events = new List<DomainEvent>();
            var stack = 0;
            while (Top(level, board, stack) != boxId) stack++;
            board.heights[stack]--;
            events.Add(new DomainEvent(EventKind.BoxRemoved, boxId, stackIndex: stack));
            level.TryGetBox(boxId, out var box);
            var ship = level.Ships[board.shipIndex];
            if (box.Type == ship.Type)
            {
                board.shipLoad++;
                events.Add(new DomainEvent(EventKind.BoxToShip, boxId, ship.Id, amount: board.shipLoad));
            }
            else
            {
                board.buffer.Add(boxId);
                events.Add(new DomainEvent(EventKind.BoxToBuffer, boxId, amount: board.buffer.Count));
            }
            Normalize(level, board, events);
            Validate(level, board, capacity);
            var status = Status(level, board, capacity);
            if (status == BoardStatus.Completed) events.Add(new DomainEvent(EventKind.LevelCompleted));
            if (status == BoardStatus.Deadlocked) events.Add(new DomainEvent(EventKind.Deadlocked));
            return new MoveResult(RejectReason.None, 0, board, events);
        }

        // Earliest matching buffer entry, not just index zero: mixed colors cannot block each other.
        private static void Normalize(LevelDefinition level, BoardSnapshot board, List<DomainEvent> events)
        {
            while (board.shipIndex < level.Ships.Count)
            {
                var ship = level.Ships[board.shipIndex];
                if (board.shipLoad == ship.Capacity)
                {
                    events.Add(new DomainEvent(EventKind.ShipDeparted, shipId: ship.Id, amount: ship.Capacity));
                    board.shipIndex++;
                    board.shipLoad = 0;
                    continue;
                }
                var match = -1;
                for (var i = 0; i < board.buffer.Count; i++)
                {
                    level.TryGetBox(board.buffer[i], out var box);
                    if (box.Type == ship.Type) { match = i; break; }
                }
                if (match < 0) return;
                var id = board.buffer[match];
                board.buffer.RemoveAt(match);
                board.shipLoad++;
                events.Add(new DomainEvent(EventKind.BufferedBoxToShip, id, ship.Id, amount: board.shipLoad));
            }
        }

        public static BoardStatus Status(LevelDefinition level, BoardSnapshot board, int capacity)
        {
            if (board.shipIndex == level.Ships.Count) return BoardStatus.Completed;
            for (var i = 0; i < board.heights.Length; i++)
            {
                var top = Top(level, board, i);
                if (top != null && CanMove(level, board, capacity, top) == RejectReason.None) return BoardStatus.Playing;
            }
            return BoardStatus.Deadlocked;
        }

        /// <summary>Checks exact ID ownership, per-type cargo totals and the normalized-state invariant.</summary>
        public static void Validate(LevelDefinition level, BoardSnapshot board, int capacity)
        {
            LevelValidator.Require(board != null && board.heights != null && board.buffer != null, "BOARD_MISSING", level.Id);
            LevelValidator.Require(capacity >= 0 && capacity <= 4 && board.buffer.Count <= capacity, "BOARD_CAPACITY", level.Id);
            LevelValidator.Require(board.heights.Length == level.Stacks.Count, "BOARD_STACKS", level.Id);
            LevelValidator.Require(board.shipIndex >= 0 && board.shipIndex <= level.Ships.Count, "BOARD_SHIP", level.Id);
            LevelValidator.Require(board.shipLoad >= 0 && (board.shipIndex == level.Ships.Count ? board.shipLoad == 0
                : board.shipLoad < level.Ships[board.shipIndex].Capacity), "BOARD_LOAD", level.Id);
            var remaining = new HashSet<string>();
            var cargo = new Dictionary<string, int> { { "R", 0 }, { "B", 0 }, { "G", 0 }, { "Y", 0 } };
            for (var s = 0; s < board.heights.Length; s++)
            {
                LevelValidator.Require(board.heights[s] >= 0 && board.heights[s] <= level.Stacks[s].Boxes.Count,
                    "BOARD_HEIGHT", level.Id);
                for (var b = 0; b < level.Stacks[s].Boxes.Count; b++)
                {
                    var box = level.Stacks[s].Boxes[b];
                    cargo[box.Type]++;
                    if (b < board.heights[s]) { remaining.Add(box.Id); cargo[box.Type]--; }
                }
            }
            foreach (var id in board.buffer)
            {
                LevelValidator.Require(level.TryGetBox(id, out var box) && remaining.Add(id), "BOARD_BOX_ID", id);
                cargo[box.Type]--;
                LevelValidator.Require(board.shipIndex >= level.Ships.Count || box.Type != level.Ships[board.shipIndex].Type,
                    "BOARD_NOT_NORMALIZED", id);
            }
            for (var i = 0; i < board.shipIndex; i++) cargo[level.Ships[i].Type] -= level.Ships[i].Capacity;
            if (board.shipIndex < level.Ships.Count) cargo[level.Ships[board.shipIndex].Type] -= board.shipLoad;
            foreach (var count in cargo) LevelValidator.Require(count.Value == 0, "BOARD_CONSERVATION", count.Key);
            if (board.shipIndex == level.Ships.Count) LevelValidator.Require(remaining.Count == 0, "BOARD_LEFTOVERS", level.Id);
        }
    }
}
