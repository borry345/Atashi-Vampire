using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Survivor.Core;
using Survivor.Strategies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.EditMode.Collision
{
    /// Self collision test world: registered bodies, the snapshot MonsterRegistry would build from them, the shared index
    /// (Required = true, rebuilt per step like BuildIndex) and a brute-force reference of the expected events.
    internal sealed class CollisionEmWorld
    {
        internal const float CellSize = 2f;

        private readonly List<CollisionBody> _bodies = new List<CollisionBody>();
        private readonly HashSet<string> _referencePrevious = new HashSet<string>();

        internal CollisionEmWorld()
        {
            Snapshot = new MonsterSnapshotBuffer();
            Index = new MonsterSpatialIndex(CellSize, Snapshot) { Required = true };
            Strategy = new SelfCollisionStrategy(Index, Snapshot);
        }

        internal MonsterSnapshotBuffer Snapshot { get; }

        internal MonsterSpatialIndex Index { get; }

        internal SelfCollisionStrategy Strategy { get; }

        internal IReadOnlyList<CollisionBody> Bodies => _bodies;

        /// Monsters excluded from the next snapshot (registered in the spawn step, not yet in the registry).
        internal HashSet<CollisionBody> HiddenFromSnapshot { get; } = new HashSet<CollisionBody>();

        internal static CollisionBody Body(int index, CollisionLayer layer, float x, float y, float radius, bool isLarge = false, int generation = 1)
        {
            return new CollisionBody
            {
                Id = new EntityHandle(index, generation),
                Layer = layer,
                Position = new Vector2(x, y),
                Radius = radius,
                IsLarge = isLarge,
            };
        }

        internal CollisionBody Add(CollisionBody body)
        {
            Strategy.Register(body);
            _bodies.Add(body);
            return body;
        }

        internal void Remove(CollisionBody body)
        {
            Strategy.Unregister(body);
            _bodies.Remove(body);
            ForgetReference(body);
        }

        /// BuildIndex (snapshot from registered monsters in ascending Id, index rebuild) then Collision; returns a copy.
        internal List<CollisionEvent> Step(long step)
        {
            BuildSnapshot();
            Index.OnPhase(SimulationPhase.BuildIndex, default);
            return new List<CollisionEvent>(Strategy.CollectEvents(step));
        }

        /// Brute-force expectation for the current positions: every body pair with a channel, R5 test, diffed against the
        /// previous call. Bodies hidden from the snapshot are skipped like the strategy skips them.
        internal List<CollisionEvent> Reference(long step)
        {
            var current = new Dictionary<string, CollisionEvent>();
            for (int i = 0; i < _bodies.Count; i++)
            {
                for (int j = 0; j < _bodies.Count; j++)
                {
                    CollisionBody x = _bodies[i];
                    CollisionBody y = _bodies[j];
                    if (i == j || HiddenFromSnapshot.Contains(x) || HiddenFromSnapshot.Contains(y))
                    {
                        continue;
                    }

                    if (!CollisionChannelTable.TryGetChannel(x.Layer, y.Layer, out CollisionChannel ch, out bool xIsA) || !xIsA)
                    {
                        continue;
                    }

                    if (CircleMath.Overlaps(x.Position, x.Radius, y.Position, y.Radius))
                    {
                        var e = new CollisionEvent(step, CollisionEventKind.Enter, ch, x.Id, y.Id);
                        current[Key(e)] = e;
                    }
                }
            }

            var result = new List<CollisionEvent>();
            foreach (KeyValuePair<string, CollisionEvent> pair in current)
            {
                if (!_referencePrevious.Contains(pair.Key))
                {
                    result.Add(pair.Value);
                }
            }

            foreach (string key in _referencePrevious)
            {
                if (!current.ContainsKey(key))
                {
                    string[] parts = key.Split('|');
                    result.Add(new CollisionEvent(step, CollisionEventKind.Exit, (CollisionChannel)int.Parse(parts[0]),
                        ParseHandle(parts[1]), ParseHandle(parts[2])));
                }
            }

            _referencePrevious.Clear();
            foreach (string key in current.Keys)
            {
                _referencePrevious.Add(key);
            }

            result.Sort((a, b) => a.CompareTo(b));
            return result;
        }

        /// Forgets reference pairs that contain body (mirrors the silent unregister).
        internal void ForgetReference(CollisionBody body)
        {
            _referencePrevious.RemoveWhere(k =>
            {
                string[] parts = k.Split('|');
                string id = body.Id.ToString();
                return parts[1] == id || parts[2] == id;
            });
        }

        private void BuildSnapshot()
        {
            Snapshot.Clear();
            var monsters = new List<CollisionBody>();
            foreach (CollisionBody body in _bodies)
            {
                if (body.Layer == CollisionLayer.Monster && !HiddenFromSnapshot.Contains(body))
                {
                    monsters.Add(body);
                }
            }

            monsters.Sort((a, b) => a.Id.CompareTo(b.Id));
            foreach (CollisionBody monster in monsters)
            {
                Snapshot.Add(new MonsterSnapshot { Id = monster.Id, Position = monster.Position, Radius = monster.Radius, IsLarge = monster.IsLarge });
            }
        }

        private static string Key(in CollisionEvent e)
        {
            return (int)e.Channel + "|" + e.A + "|" + e.B;
        }

        private static EntityHandle ParseHandle(string text)
        {
            string[] parts = text.Split(':');
            return new EntityHandle(int.Parse(parts[0]), int.Parse(parts[1]));
        }
    }

    /// Formatting and log expectations shared by the collision EditMode tests.
    internal static class CollisionEmUtil
    {
        /// One CSV row per event (CollisionEvent.ToString), newline separated; "(none)" when empty.
        internal static string Format(IReadOnlyList<CollisionEvent> events)
        {
            if (events.Count == 0)
            {
                return "(none)";
            }

            var builder = new StringBuilder();
            for (int i = 0; i < events.Count; i++)
            {
                builder.Append(events[i]).Append('\n');
            }

            return builder.ToString();
        }

        /// Expects one collision error log line in the shared format whose message matches messagePattern.
        internal static void ExpectCollisionError(string messagePattern)
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[ERROR\] \[Collision\] " + messagePattern));
        }
    }
}
