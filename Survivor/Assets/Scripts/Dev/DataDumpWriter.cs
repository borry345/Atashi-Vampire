using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Survivor.Data;

namespace Survivor.Dev
{
    /// Dump of the loaded data and its validation report (SPEC F19 data, 5.15): every public property of every definition
    /// (seconds and the derived step values alike, since the definitions expose both) and every report issue, written to
    /// <root>/dump_<yyyyMMddTHHmmssZ>.txt as UTF-8 with "\n" lines. Numbers in InvariantCulture. Reflection keeps the dump
    /// complete when Data adds a field, at the cost of a fixed order only by declaration.
    public sealed class DataDumpWriter
    {
        private const int MaxDepth = 8;
        private readonly string _root;

        /// Writer under root (default Application.persistentDataPath, injected by tests). Empty root throws.
        public DataDumpWriter(string root)
        {
            if (string.IsNullOrEmpty(root))
            {
                throw new ArgumentException("Dump output root is empty.", nameof(root));
            }

            _root = root;
        }

        /// "dump_<yyyyMMddTHHmmssZ>.txt" of utc.
        public static string FileName(DateTime utc)
        {
            return "dump_" + MeasurementCsvExporter.FileStamp(utc) + ".txt";
        }

        /// Dump text: a "# data" section (null data says so) and a "# report" section (null report says so).
        public static string BuildText(GameData data, DataValidationReport report, DateTime utc)
        {
            var builder = new StringBuilder(16384);
            builder.Append("# Survivor data dump ").Append(utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("# data").Append('\n');
            if (data == null)
            {
                builder.Append("(no data loaded)").Append('\n');
            }
            else
            {
                AppendObject(builder, "data", data, 0);
            }

            builder.Append("# report").Append('\n');
            if (report == null)
            {
                builder.Append("(no report)").Append('\n');
                return builder.ToString();
            }

            builder.Append("playable: ").Append(report.IsPlayable ? "true" : "false").Append('\n');
            builder.Append("blocking: ").Append(report.BlockingCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append("warnings: ").Append(report.WarningCount.ToString(CultureInfo.InvariantCulture)).Append('\n');
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ValidationIssue issue = report.Issues[i];
                builder.Append(issue.Severity).Append(' ').Append(issue).Append('\n');
            }

            return builder.ToString();
        }

        /// Writes BuildText to <root>/FileName(utc) and returns the path. IO errors propagate.
        public string Write(GameData data, DataValidationReport report, DateTime utc)
        {
            Directory.CreateDirectory(_root);
            string path = Path.Combine(_root, FileName(utc));
            File.WriteAllText(path, BuildText(data, report, utc), new UTF8Encoding(false));
            return path;
        }

        private static void AppendObject(StringBuilder builder, string path, object value, int depth)
        {
            if (value == null)
            {
                builder.Append(path).Append(" = null").Append('\n');
                return;
            }

            if (TryFormatScalar(value, out string text))
            {
                builder.Append(path).Append(" = ").Append(text).Append('\n');
                return;
            }

            if (depth >= MaxDepth)
            {
                builder.Append(path).Append(" = (depth limit)").Append('\n');
                return;
            }

            if (value is IEnumerable sequence)
            {
                int index = 0;
                foreach (object item in sequence)
                {
                    AppendObject(builder, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]", item, depth + 1);
                    index++;
                }

                if (index == 0)
                {
                    builder.Append(path).Append(" = []").Append('\n');
                }

                return;
            }

            PropertyInfo[] properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (PropertyInfo property in properties)
            {
                if (property.GetIndexParameters().Length > 0 || !property.CanRead)
                {
                    continue;
                }

                object child;
                try
                {
                    child = property.GetValue(value);
                }
                catch (TargetInvocationException e)
                {
                    child = "(error: " + (e.InnerException ?? e).Message + ")";
                }

                AppendObject(builder, path + "." + property.Name, child, depth + 1);
            }

            foreach (FieldInfo field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                AppendObject(builder, path + "." + field.Name, field.GetValue(value), depth + 1);
            }
        }

        private static bool TryFormatScalar(object value, out string text)
        {
            switch (value)
            {
                case string s:
                    text = "\"" + s + "\"";
                    return true;
                case float f:
                    text = f.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case double d:
                    text = d.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case bool b:
                    text = b ? "true" : "false";
                    return true;
                case IFormattable formattable when value.GetType().IsPrimitive || value is Enum || value is decimal:
                    text = formattable.ToString(null, CultureInfo.InvariantCulture);
                    return true;
                case UnityEngine.Vector2 v:
                    text = "(" + v.x.ToString("R", CultureInfo.InvariantCulture) + ", " + v.y.ToString("R", CultureInfo.InvariantCulture) + ")";
                    return true;
                case UnityEngine.Object unityObject:
                    text = unityObject.GetType().Name + " " + unityObject.name;
                    return true;
                case object engineValue when engineValue.GetType().IsValueType && engineValue.GetType().Namespace == "UnityEngine":
                    // Engine structs (Color, Rect, ...) expose properties returning their own type; walking them would repeat.
                    text = engineValue.ToString();
                    return true;
                default:
                    text = null;
                    return false;
            }
        }
    }
}
