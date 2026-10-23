using System;
using System.Collections.Generic;
using System.Text;

namespace PortCargo.Core
{
    /// <summary>Detached serializable board. Capacity belongs to the run, so undo cannot revoke expansion.</summary>
    [Serializable]
    public sealed class BoardSnapshot
    {
        public int[] heights;
        public List<string> buffer = new List<string>();
        public int shipIndex;
        public int shipLoad;

        public BoardSnapshot Clone()
        {
            return new BoardSnapshot {
                heights = heights == null ? null : (int[])heights.Clone(),
                buffer = buffer == null ? null : new List<string>(buffer),
                shipIndex = shipIndex, shipLoad = shipLoad
            };
        }

        /// <summary>Stable exact-state key for evidence; contains IDs, not runtime object references.</summary>
        public string ExactKey()
        {
            var key = new StringBuilder();
            foreach (var height in heights) key.Append(height).Append(',');
            key.Append('|').Append(shipIndex).Append(':').Append(shipLoad).Append('|');
            foreach (var id in buffer) key.Append(id.Length).Append(':').Append(id);
            return key.ToString();
        }
    }

    public enum RejectReason { None, Busy, StaleVersion, WrongRun, NotTop, UnknownBox, BufferFull, AlreadyCompleted, InvalidData }
    public enum BoardStatus { Playing, Deadlocked, Completed }
    public enum EventKind { BoxRemoved, BoxToShip, BoxToBuffer, ShipDeparted, BufferedBoxToShip, LevelCompleted, Deadlocked }

    public sealed class DomainEvent
    {
        public EventKind Kind { get; }
        public string BoxId { get; }
        public string ShipId { get; }
        public int StackIndex { get; }
        public int Amount { get; }
        public DomainEvent(EventKind kind, string boxId = null, string shipId = null, int stackIndex = -1, int amount = 0)
        { Kind = kind; BoxId = boxId; ShipId = shipId; StackIndex = stackIndex; Amount = amount; }
    }

    public sealed class MoveResult
    {
        public bool Accepted => Reason == RejectReason.None;
        public RejectReason Reason { get; }
        public long Version { get; }
        public BoardSnapshot Board { get; }
        public IReadOnlyList<DomainEvent> Events { get; }
        public MoveResult(RejectReason reason, long version, BoardSnapshot board, List<DomainEvent> events = null)
        {
            Reason = reason; Version = version; Board = board.Clone();
            Events = (events ?? new List<DomainEvent>()).AsReadOnly();
        }
    }
}
