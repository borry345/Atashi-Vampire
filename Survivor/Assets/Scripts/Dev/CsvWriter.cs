using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Survivor.Dev
{
    /// R28 CSV format, the single owner for every dev CSV (measurement, summary, frames, collision log): UTF-8 with a BOM on
    /// new files only, "\r\n" line ends, numbers in InvariantCulture ("R" or a given format), booleans true/false, and values
    /// holding a comma, quote or line break wrapped in double quotes with inner quotes doubled.
    public static class CsvWriter
    {
        /// Line end of every CSV row.
        public const string NewLine = "\r\n";

        /// Value written when a number cannot be measured (5.15.3, R27).
        public const string NotAvailable = "NA";

        private static readonly UTF8Encoding WithBom = new UTF8Encoding(true);
        private static readonly UTF8Encoding WithoutBom = new UTF8Encoding(false);

        /// value quoted when it holds ',', '"', '\r' or '\n' (inner '"' doubled); null gives "".
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// Round-trip ("R") InvariantCulture text of value.
        public static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// Round-trip ("R") InvariantCulture text of value.
        public static string Number(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        /// value in InvariantCulture with the given format (R28 "given format").
        public static string Number(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        /// "R" text of value, or NA when value is null (not measurable).
        public static string Number(double? value)
        {
            return value.HasValue ? Number(value.Value) : NotAvailable;
        }

        /// InvariantCulture text of value.
        public static string Number(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// "true" or "false".
        public static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        /// Appends fields (each escaped) joined by ',' and one "\r\n" to builder. Null arguments throw.
        public static void AppendRow(StringBuilder builder, IReadOnlyList<string> fields)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(Escape(fields[i]));
            }

            builder.Append(NewLine);
        }

        /// Writes content to a new file at path (BOM first), replacing any file there; creates the directory.
        public static void WriteNewFile(string path, string content)
        {
            EnsureDirectory(path);
            File.WriteAllText(path, content ?? string.Empty, WithBom);
        }

        /// Appends content to path. A missing file is created with the BOM and headerRow (already "\r\n"-terminated, may be
        /// null) first; an existing file gets content only, without a BOM.
        public static void AppendToFile(string path, string headerRow, string content)
        {
            EnsureDirectory(path);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, (headerRow ?? string.Empty) + (content ?? string.Empty), WithBom);
                return;
            }

            File.AppendAllText(path, content ?? string.Empty, WithoutBom);
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("CSV path is empty.", nameof(path));
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
    }
}
