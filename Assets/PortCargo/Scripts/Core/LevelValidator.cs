using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PortCargo.Core
{
    public sealed class DataValidationException : Exception
    {
        public string Code { get; }
        public DataValidationException(string code, string detail) : base(code + ": " + detail) { Code = code; }
    }

    public static class LevelValidator
    {
        /// <summary>Rejects malformed catalogs before constructing immutable definitions.</summary>
        public static LevelDefinition[] Load(CatalogData data)
        {
            Require(data != null && data.catalogVersion == 1, "CATALOG_VERSION", "Expected catalogVersion 1");
            Require(data.levels != null && data.levels.Length >= 1 && data.levels.Length <= 200,
                "LEVEL_COUNT", "Expected 1..200 levels");
            var ids = new HashSet<string>();
            var result = new LevelDefinition[data.levels.Length];
            for (var i = 0; i < result.Length; i++)
            {
                Validate(data.levels[i]);
                Require(Regex.IsMatch(data.levels[i].id, "^L(?:[0-9]{2}|1[0-9]{2}|200)$"),
                    "CATALOG_LEVEL_ID", "Campaign catalogs only contain L levels");
                Require(ids.Add(data.levels[i].id), "DUPLICATE_LEVEL", data.levels[i].id);
                result[i] = new LevelDefinition(data.levels[i]);
            }
            return result;
        }

        /// <summary>Checks bounds, unique IDs and per-type conservation; never repairs bad source data.</summary>
        public static void Validate(LevelData data)
        {
            Require(data != null && data.schemaVersion == 1, "LEVEL_VERSION", "Expected schemaVersion 1");
            Require(data.id != null && Regex.IsMatch(data.id, "^(L(?:[0-9]{2}|1[0-9]{2}|200)|E01)$"), "LEVEL_ID", data.id);
            Require(data.revision >= 1 && data.bufferCapacity == 3, "LEVEL_SETTINGS", data.id);
            Require(data.stacks != null && data.stacks.Length >= 1 && data.stacks.Length <= 6,
                "STACK_COUNT", data.id);
            Require(data.ships != null && data.ships.Length >= 1 && data.ships.Length <= 12,
                "SHIP_COUNT", data.id);
            var stackIds = new HashSet<string>();
            var boxIds = new HashSet<string>();
            var shipIds = new HashSet<string>();
            var balance = new Dictionary<string, int> { { "R", 0 }, { "B", 0 }, { "G", 0 }, { "Y", 0 } };
            foreach (var stack in data.stacks)
            {
                Require(stack != null, "MISSING_STACK", data.id);
                Unique(stack.id, stackIds, "STACK_ID");
                Require(stack.boxes != null && stack.boxes.Length >= 1 && stack.boxes.Length <= 6,
                    "STACK_DEPTH", stack.id);
                foreach (var box in stack.boxes)
                {
                    Require(box != null, "MISSING_BOX", stack.id);
                    Unique(box.id, boxIds, "BOX_ID");
                    Require(box.type != null && balance.ContainsKey(box.type), "BOX_TYPE", box.id);
                    balance[box.type]++;
                }
            }
            Require(boxIds.Count <= 36, "BOX_COUNT", data.id);
            foreach (var ship in data.ships)
            {
                Require(ship != null, "MISSING_SHIP", data.id);
                Unique(ship.id, shipIds, "SHIP_ID");
                Require(ship.type != null && balance.ContainsKey(ship.type), "SHIP_TYPE", ship.id);
                Require(ship.capacity >= 1 && ship.capacity <= 6, "SHIP_CAPACITY", ship.id);
                balance[ship.type] -= ship.capacity;
            }
            foreach (var pair in balance) Require(pair.Value == 0, "CARGO_BALANCE", pair.Key);
        }

        internal static void Require(bool condition, string code, string detail)
        {
            if (!condition) throw new DataValidationException(code, detail ?? "missing");
        }

        private static void Unique(string id, HashSet<string> ids, string code)
        {
            Require(!string.IsNullOrWhiteSpace(id) && ids.Add(id), code, id);
        }
    }
}
