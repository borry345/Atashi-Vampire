using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Survivor.Data;

namespace Survivor.Dev
{
    /// Measurement files (5.15.3, R28 through CsvWriter) under <root>/measurements: measurement_<yyyyMMddTHHmmssZ>.csv (header
    /// block of key,value rows, one empty row, body column row + value row), frames_<same>.csv (raw frames) and, for valid
    /// sessions only, one row appended to summary.csv (header keys then body columns; BOM and column row only for a new file).
    /// Header key and body column order is fixed by HeaderKeys / BodyColumns.
    public sealed class MeasurementCsvExporter
    {
        /// Value of the schema header key.
        public const string Schema = "survivor-measurement-1";

        /// Sub-folder of the output root.
        public const string FolderName = "measurements";

        /// Summary file name.
        public const string SummaryFileName = "summary.csv";

        /// monsterCountLimit outside A0 (not measured by definition, D28).
        public const string NotApplicable = "해당 없음";

        /// monsterCountLimit in A0 when the average FPS stayed at or above 55 (D28).
        public const string NotReached = "미도달";

        /// Header keys in file order (5.15.3).
        public static readonly IReadOnlyList<string> HeaderKeys = new[]
        {
            "schema", "timestampUtc", "label", "group", "buildType", "unityVersion", "cpu", "gpu", "ramMB", "os",
            "vSyncCount", "targetFrameRate", "scriptingBackend", "incrementalGC", "pool", "target", "tick", "collision", "ccdMode",
            "monsterCountN", "monsterCapDisabled", "despawnDistance", "despawnFormula", "seed", "hpMultipliers", "cellSize",
            "stepSeconds", "durationSteps", "warmupSteps", "nReachedStep", "playerInvulnerable", "weaponLoadout", "chestDropChance",
            "spawnStopHoldN", "levelUpEnabled", "autoPathRadius", "autoPathSpeed", "timeScale", "overlayHidden", "visualizersOff",
            "collisionLogOff", "valid", "invalidReason",
        };

        /// Body columns in file order (5.15.3).
        public static readonly IReadOnlyList<string> BodyColumns = BuildBodyColumns();

        /// Frames file columns in file order (5.15.3; markers in R16 order).
        public static readonly IReadOnlyList<string> FrameColumns = BuildFrameColumns();

        private const double BytesPerMB = 1024d * 1024d;
        private readonly string _root;

        /// Exporter writing under root (default Application.persistentDataPath, injected by tests). Empty root throws.
        public MeasurementCsvExporter(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                throw new ArgumentException("Measurement output root is empty.", nameof(root));
            }

            _root = root;
        }

        /// <root>/measurements.
        public string OutputDirectory => Path.Combine(_root, FolderName);

        /// "yyyyMMddTHHmmssZ" of utc (file name part).
        public static string FileStamp(DateTime utc)
        {
            return utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }

        /// R31 values joined by ';' in segment order, each "0.0#####" InvariantCulture ("1.0;1.5;2.25"); "" for no segment.
        /// Null throws.
        public static string FormatHpMultipliers(IEnumerable<float> multipliers)
        {
            if (multipliers == null)
            {
                throw new ArgumentNullException(nameof(multipliers));
            }

            var builder = new StringBuilder();
            foreach (float value in multipliers)
            {
                if (builder.Length > 0)
                {
                    builder.Append(';');
                }

                builder.Append(value.ToString("0.0#####", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        /// FormatHpMultipliers of table.Segments' HpMultiplier; NA without a table.
        public static string FormatHpMultipliers(SpawnTableDefinition table)
        {
            if (table == null || table.Segments == null)
            {
                return CsvWriter.NotAvailable;
            }

            var values = new List<float>(table.Segments.Count);
            for (int i = 0; i < table.Segments.Count; i++)
            {
                values.Add(table.Segments[i].HpMultiplier);
            }

            return FormatHpMultipliers(values);
        }

        /// Header values of record in HeaderKeys order. Null throws.
        public static IReadOnlyList<string> BuildHeaderValues(MeasurementRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            return new[]
            {
                Schema,
                record.TimestampUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                record.Label,
                record.Group,
                record.BuildType,
                record.UnityVersion,
                record.Cpu,
                record.Gpu,
                CsvWriter.Number(record.RamMB),
                record.Os,
                CsvWriter.Number(record.VSyncCount),
                CsvWriter.Number(record.TargetFrameRate),
                record.ScriptingBackend,
                CsvWriter.Bool(record.IncrementalGC),
                record.Selection.Pool.ToString(),
                record.Selection.Target.ToString(),
                record.Selection.Tick.ToString(),
                record.Selection.Collision.ToString(),
                record.Selection.EngineDetectionMode.ToString(),
                CsvWriter.Number(record.MonsterCountN),
                CsvWriter.Bool(record.MonsterCapDisabled),
                CsvWriter.Number(record.DespawnDistance),
                record.DespawnFormula,
                record.Seed.ToString(CultureInfo.InvariantCulture),
                record.HpMultipliers,
                CsvWriter.Number(record.Selection.SpatialHashCellSize),
                CsvWriter.Number(1d / Survivor.Core.StepMath.StepsPerSecond, "0.#########"),
                CsvWriter.Number(record.DurationSteps),
                CsvWriter.Number(Survivor.Core.GameRules.MeasurementWarmupSteps),
                record.NReachedStep >= 0 ? CsvWriter.Number(record.NReachedStep) : CsvWriter.NotAvailable,
                CsvWriter.Bool(record.PlayerInvulnerable),
                record.WeaponLoadout,
                CsvWriter.Number(record.ChestDropChance),
                CsvWriter.Bool(record.SpawnStopHoldN),
                CsvWriter.Bool(record.LevelUpEnabled),
                CsvWriter.Number(record.AutoPathRadius),
                CsvWriter.Number(record.AutoPathSpeed),
                CsvWriter.Number(record.TimeScale),
                CsvWriter.Bool(record.OverlayHidden),
                CsvWriter.Bool(record.VisualizersOff),
                CsvWriter.Bool(record.CollisionLogOff),
                CsvWriter.Bool(record.Valid),
                record.Valid ? string.Empty : record.InvalidReason ?? string.Empty,
            };
        }

        /// Body values of record in BodyColumns order. Null throws.
        public static IReadOnlyList<string> BuildBodyValues(MeasurementRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            MeasurementStats s = record.Stats ?? new MeasurementStats();
            var values = new List<string>(BodyColumns.Count)
            {
                CsvWriter.Number(s.Frames),
                CsvWriter.Number(s.Steps),
                CsvWriter.Number(s.SlowmoFrames),
                CsvWriter.Number(s.AvgFps),
                CsvWriter.Number(s.OnePercentLowFps),
                CsvWriter.Number(s.WorstFrameMs),
                CsvWriter.Number(s.GcAllocPerFrameBytes),
                MonsterCountLimit(record.Group, record.MonsterCountN, s.AvgFps),
                CsvWriter.Number(s.MonsterCountAvg),
                CsvWriter.Number(s.MonsterCountMax),
            };

            for (int m = 0; m < StatsCollector.MarkerCount; m++)
            {
                values.Add(CsvWriter.Number(At(s.MarkerPerStepMs, m)));
                values.Add(CsvWriter.Number(At(s.MarkerPerFrameMs, m)));
            }

            values.Add(CsvWriter.Number(s.MemUsedAvgMB));
            values.Add(CsvWriter.Number(s.MemUsedMaxMB));
            values.Add(CsvWriter.Number(s.MemReservedAvgMB));
            values.Add(CsvWriter.Number(s.MemReservedMaxMB));
            values.Add(CsvWriter.Number(s.ActiveGemsAvg));
            values.Add(CsvWriter.Number(s.ActiveGemsMax));
            return values;
        }

        /// SPEC F10 item 4 / D28: "해당 없음" outside A0; in A0 N when avgFps < 55, "미도달" otherwise, NA without avgFps.
        public static string MonsterCountLimit(string group, int n, double? avgFps)
        {
            if (!string.Equals(group, "A0", StringComparison.Ordinal))
            {
                return NotApplicable;
            }

            if (!avgFps.HasValue)
            {
                return CsvWriter.NotAvailable;
            }

            return avgFps.Value < MeasurementMath.MonsterLimitFps ? CsvWriter.Number(n) : NotReached;
        }

        /// Full text of the measurement file (no BOM; CsvWriter adds it). Null throws.
        public static string BuildMeasurementText(MeasurementRecord record)
        {
            IReadOnlyList<string> header = BuildHeaderValues(record);
            var builder = new StringBuilder(4096);
            for (int i = 0; i < HeaderKeys.Count; i++)
            {
                CsvWriter.AppendRow(builder, new[] { HeaderKeys[i], header[i] });
            }

            builder.Append(CsvWriter.NewLine);
            CsvWriter.AppendRow(builder, BodyColumns);
            CsvWriter.AppendRow(builder, BuildBodyValues(record));
            return builder.ToString();
        }

        /// Column row of summary.csv (header keys then body columns, "\r\n"-terminated).
        public static string BuildSummaryColumnRow()
        {
            var builder = new StringBuilder(2048);
            CsvWriter.AppendRow(builder, Concat(HeaderKeys, BodyColumns));
            return builder.ToString();
        }

        /// One summary.csv row of record ("\r\n"-terminated). Null throws.
        public static string BuildSummaryRow(MeasurementRecord record)
        {
            var builder = new StringBuilder(2048);
            CsvWriter.AppendRow(builder, Concat(BuildHeaderValues(record), BuildBodyValues(record)));
            return builder.ToString();
        }

        /// Full text of the frames file (column row + one row per frame). Null throws.
        public static string BuildFramesText(IReadOnlyList<MeasurementFrame> frames)
        {
            if (frames == null)
            {
                throw new ArgumentNullException(nameof(frames));
            }

            var builder = new StringBuilder(64 + frames.Count * 96);
            CsvWriter.AppendRow(builder, FrameColumns);
            var row = new string[FrameColumns.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                MeasurementFrame f = frames[i];
                row[0] = CsvWriter.Number(f.Frame);
                row[1] = NumberOrNa(f.UnscaledDtMs);
                row[2] = CsvWriter.Number(f.Steps);
                row[3] = NumberOrNa(f.GcBytes);
                row[4] = NumberOrNa(f.MemUsedBytes / BytesPerMB);
                row[5] = CsvWriter.Number(f.LiveMonsters);
                row[6] = CsvWriter.Number(f.ActiveGems);
                for (int m = 0; m < StatsCollector.MarkerCount; m++)
                {
                    row[7 + m] = NumberOrNa(f.MarkerMs != null && m < f.MarkerMs.Length ? f.MarkerMs[m] : double.NaN);
                }

                CsvWriter.AppendRow(builder, row);
            }

            return builder.ToString();
        }

        /// Writes measurement_<stamp>.csv and frames_<stamp>.csv, and appends to summary.csv when record.Valid. Returns the
        /// measurement file path. IO errors propagate to the caller (the session reports them). Null record throws.
        public string Export(MeasurementRecord record, IReadOnlyList<MeasurementFrame> frames)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            string stamp = FileStamp(record.TimestampUtc);
            string measurementPath = Path.Combine(OutputDirectory, "measurement_" + stamp + ".csv");
            CsvWriter.WriteNewFile(measurementPath, BuildMeasurementText(record));
            CsvWriter.WriteNewFile(Path.Combine(OutputDirectory, "frames_" + stamp + ".csv"), BuildFramesText(frames ?? Array.Empty<MeasurementFrame>()));
            if (record.Valid)
            {
                CsvWriter.AppendToFile(Path.Combine(OutputDirectory, SummaryFileName), BuildSummaryColumnRow(), BuildSummaryRow(record));
            }

            return measurementPath;
        }

        private static string NumberOrNa(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value) ? CsvWriter.NotAvailable : CsvWriter.Number(value);
        }

        private static double? At(double?[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : null;
        }

        private static string[] Concat(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            var all = new string[a.Count + b.Count];
            for (int i = 0; i < a.Count; i++)
            {
                all[i] = a[i];
            }

            for (int i = 0; i < b.Count; i++)
            {
                all[a.Count + i] = b[i];
            }

            return all;
        }

        private static IReadOnlyList<string> BuildBodyColumns()
        {
            var columns = new List<string>
            {
                "frames", "steps", "slowmoFrames", "avgFps", "onePercentLowFps", "worstFrameMs", "gcAllocPerFrameBytes",
                "monsterCountLimit", "monsterCountAvg", "monsterCountMax",
            };

            for (int m = 0; m < StatsCollector.MarkerCount; m++)
            {
                columns.Add(StatsCollector.MarkerColumns[m] + "_msPerStep");
                columns.Add(StatsCollector.MarkerColumns[m] + "_msPerFrame");
            }

            columns.AddRange(new[]
            {
                "memTotalUsedMB_avg", "memTotalUsedMB_max", "memTotalReservedMB_avg", "memTotalReservedMB_max", "activeGems_avg",
                "activeGems_max",
            });
            return columns;
        }

        private static IReadOnlyList<string> BuildFrameColumns()
        {
            var columns = new List<string> { "frame", "unscaledDtMs", "steps", "gcBytes", "memUsedMB", "liveMonsters", "activeGems" };
            for (int m = 0; m < StatsCollector.MarkerCount; m++)
            {
                columns.Add(StatsCollector.MarkerColumns[m] + "_ms");
            }

            return columns;
        }
    }
}
