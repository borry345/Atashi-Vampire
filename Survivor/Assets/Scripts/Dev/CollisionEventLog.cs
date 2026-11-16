using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Dev
{
    /// Memory buffer of every collision event of one run (E1/E2 equivalence input, SPEC F19), exported to
    /// collision_<seed>_<strategy>_<mode>.csv (R28): column row "step,channel,a,b,kind", then one CollisionEvent.ToString() row
    /// per event in the order the strategy emitted them (sorted per step by CompareTo).
    public sealed class CollisionEventLog
    {
        /// Column row of the file (CollisionEvent.CompareTo / ToString order).
        public const string ColumnRow = "step,channel,a,b,kind";

        private readonly List<CollisionEvent> _events = new List<CollisionEvent>(4096);

        /// Events recorded so far, oldest first.
        public IReadOnlyList<CollisionEvent> Events => _events;

        /// Copies events (one step's CollectEvents result) to the end of the buffer. Null throws.
        public void Append(IReadOnlyList<CollisionEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            for (int i = 0; i < events.Count; i++)
            {
                _events.Add(events[i]);
            }
        }

        /// "collision_<seed>_<strategy>_<mode>.csv" (seed in decimal, enum names).
        public static string FileName(ulong seed, CollisionStrategyKind strategy, CollisionDetectionMode2D mode)
        {
            return "collision_" + seed.ToString(CultureInfo.InvariantCulture) + "_" + strategy + "_" + mode + ".csv";
        }

        /// File text without the BOM: the column row, then one row per event, each ending in "\r\n".
        public string BuildText()
        {
            var builder = new StringBuilder(32 + _events.Count * 40);
            builder.Append(ColumnRow).Append(CsvWriter.NewLine);
            for (int i = 0; i < _events.Count; i++)
            {
                builder.Append(_events[i].ToString()).Append(CsvWriter.NewLine);
            }

            return builder.ToString();
        }

        /// Writes the log to directory/FileName(...) as a new file (BOM, R28) and returns the path. IO errors propagate.
        public string Export(string directory, ulong seed, CollisionStrategyKind strategy, CollisionDetectionMode2D mode)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Collision log directory is empty.", nameof(directory));
            }

            string path = Path.Combine(directory, FileName(seed, strategy, mode));
            CsvWriter.WriteNewFile(path, BuildText());
            return path;
        }
    }
}
