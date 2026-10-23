using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PortCargo.Core
{
    // These field-only DTOs are the versioned JSON contract, not mutable gameplay definitions.
    [Serializable] public sealed class CatalogData { public int catalogVersion; public LevelData[] levels; }
    [Serializable] public sealed class LevelData
    {
        public int schemaVersion, revision, bufferCapacity;
        public string id;
        public StackData[] stacks;
        public ShipData[] ships;
    }
    [Serializable] public sealed class StackData { public string id; public BoxData[] boxes; }
    [Serializable] public sealed class BoxData { public string id, type; }
    [Serializable] public sealed class ShipData { public string id, type; public int capacity; }

    public sealed class BoxDefinition
    {
        public string Id { get; }
        public string Type { get; }
        internal BoxDefinition(BoxData data) { Id = data.id; Type = data.type; }
    }

    public sealed class StackDefinition
    {
        public string Id { get; }
        public ReadOnlyCollection<BoxDefinition> Boxes { get; }
        internal StackDefinition(StackData data)
        {
            Id = data.id;
            var boxes = new List<BoxDefinition>();
            foreach (var box in data.boxes) boxes.Add(new BoxDefinition(box));
            Boxes = boxes.AsReadOnly();
        }
    }

    public sealed class ShipDefinition
    {
        public string Id { get; }
        public string Type { get; }
        public int Capacity { get; }
        internal ShipDefinition(ShipData data) { Id = data.id; Type = data.type; Capacity = data.capacity; }
    }

    /// <summary>Validated, immutable level configuration. IDs are opaque, never parsed for coordinates.</summary>
    public sealed class LevelDefinition
    {
        public string Id { get; }
        public int Revision { get; }
        public int BoxCount { get; }
        public ReadOnlyCollection<StackDefinition> Stacks { get; }
        public ReadOnlyCollection<ShipDefinition> Ships { get; }
        private readonly Dictionary<string, BoxDefinition> boxesById = new Dictionary<string, BoxDefinition>();

        public LevelDefinition(LevelData data)
        {
            LevelValidator.Validate(data);
            Id = data.id;
            Revision = data.revision;
            var stacks = new List<StackDefinition>();
            foreach (var stack in data.stacks)
            {
                var definition = new StackDefinition(stack);
                stacks.Add(definition);
                foreach (var box in definition.Boxes) boxesById.Add(box.Id, box);
            }
            var ships = new List<ShipDefinition>();
            foreach (var ship in data.ships) ships.Add(new ShipDefinition(ship));
            Stacks = stacks.AsReadOnly();
            Ships = ships.AsReadOnly();
            BoxCount = boxesById.Count;
        }

        public bool TryGetBox(string id, out BoxDefinition box)
        {
            box = null;
            return id != null && boxesById.TryGetValue(id, out box);
        }
    }
}
