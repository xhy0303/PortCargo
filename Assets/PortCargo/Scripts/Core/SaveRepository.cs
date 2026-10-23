using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace PortCargo.Core
{
    public interface IStoragePort { string ReadString(string key); void WriteString(string key, string value); }
    public interface IJsonCodec { string Encode<T>(T value); T Decode<T>(string text); }

    [Serializable] public sealed class SaveEnvelope
    { public int saveSchema; public long revision; public string payload, checksum; }
    [Serializable] public sealed class PlayerProfile
    {
        public int unlockedCount = 1;
        public List<string> completed = new List<string>();
        public bool sound = true, music = true, reducedMotion;
        public bool tutorialCompleted;
    }
    [Serializable] public sealed class PlayerSave
    {
        public PlayerProfile profile = new PlayerProfile();
        // Retains the campaign run even when the player is currently in endless mode.
        public RunData activeRun;
        // Missing in v0.1 saves: null/empty continues the campaign without a migration reset.
        public string mode;
        public EndlessProgress endless;
    }
    [Serializable] public sealed class EndlessProgress
    {
        public int generatorVersion, seed, round;
        // Optional in v1/v2 saves. Canonical structural hashes, oldest first.
        public List<string> recentLayouts = new List<string>();
        // Persist the full board definition so later generator versions cannot change an active round.
        public LevelData level;
        public RunData run;
    }
    public sealed class LoadResult
    {
        public PlayerSave Value;
        public bool Reset, RecoveredOlderSlot;
        public long Revision;
    }

    /// <summary>Two independent full payload slots; read-back confirmation and semantic validation are required.</summary>
    public sealed class SaveRepository
    {
        public const int MaxPayloadBytes = 128 * 1024;
        private readonly IStoragePort storage;
        private readonly IJsonCodec codec;
        private readonly Action<PlayerSave> validate;
        private readonly long[] revisions = { -1, -1 };
        private SaveEnvelope pending;
        private long nextRevision;
        public bool HasPending => pending != null;
        public string LastError { get; private set; }
        public long CommittedRevision { get; private set; }

        public SaveRepository(IStoragePort storage, IJsonCodec codec, Action<PlayerSave> validate)
        { this.storage = storage; this.codec = codec; this.validate = validate; }

        public LoadResult Load()
        {
            PlayerSave best = null;
            long bestRevision = -1;
            var any = false;
            var invalid = false;
            for (var slot = 0; slot < 2; slot++)
            {
                try
                {
                    var text = storage.ReadString(Key(slot));
                    if (string.IsNullOrEmpty(text)) continue;
                    any = true;
                    var envelope = Parse(text, out var value);
                    revisions[slot] = envelope.revision;
                    nextRevision = Math.Max(nextRevision, envelope.revision);
                    if (envelope.revision > bestRevision) { best = value; bestRevision = envelope.revision; }
                }
                catch (Exception) { invalid = true; }
            }
            CommittedRevision = Math.Max(0, bestRevision);
            return new LoadResult { Value = best, Revision = CommittedRevision,
                Reset = best == null && (any || invalid), RecoveredOlderSlot = best != null && invalid };
        }

        /// <summary>Stages a new immutable payload. Replaces any older failed pending save in revision order.</summary>
        public bool Save(PlayerSave value)
        {
            try
            {
                validate(value);
                var payload = codec.Encode(value);
                if (Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes) throw new InvalidOperationException("SAVE_TOO_LARGE");
                if (nextRevision == long.MaxValue) throw new InvalidOperationException("SAVE_REVISION_OVERFLOW");
                pending = new SaveEnvelope { saveSchema = 1, revision = ++nextRevision, payload = payload, checksum = Hash(payload) };
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
            return Retry();
        }

        /// <summary>Retries the exact pending bytes. Never replays a game action or a reward.</summary>
        public bool Retry()
        {
            if (pending == null) return true;
            try
            {
                var slot = revisions[0] <= revisions[1] ? 0 : 1;
                var text = codec.Encode(pending);
                storage.WriteString(Key(slot), text);
                var readback = storage.ReadString(Key(slot));
                if (readback != text) throw new InvalidOperationException("SAVE_READBACK_MISMATCH");
                var verified = Parse(readback, out _);
                revisions[slot] = verified.revision;
                CommittedRevision = verified.revision;
                pending = null;
                LastError = null;
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        private SaveEnvelope Parse(string text, out PlayerSave value)
        {
            if (Encoding.UTF8.GetByteCount(text) > MaxPayloadBytes * 2 + 4096) throw new InvalidOperationException("SAVE_ENVELOPE_SIZE");
            var envelope = codec.Decode<SaveEnvelope>(text);
            if (envelope == null || envelope.saveSchema != 1 || envelope.revision < 1 || envelope.payload == null
                || Encoding.UTF8.GetByteCount(envelope.payload) > MaxPayloadBytes || Hash(envelope.payload) != envelope.checksum)
                throw new InvalidOperationException("SAVE_CHECKSUM_SCHEMA");
            value = codec.Decode<PlayerSave>(envelope.payload);
            validate(value);
            return envelope;
        }

        public static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }

        public static string Key(int slot) => slot == 0 ? "portcargo.save.a" : "portcargo.save.b";
    }
}
