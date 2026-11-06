using System.Globalization;
using System.Text;

namespace Survivor.Data
{
    /// One validation finding: file, definition, field path, Korean message and (when known) the JSON position.
    public sealed class ValidationIssue
    {
        /// Warning (run continues) or Blocking (run cannot start).
        public IssueSeverity Severity;

        /// Data file name (e.g. "weapons.json"); null for findings that are not tied to one file.
        public string FileName;

        /// Definition id (e.g. "W3"); null when unknown or not applicable.
        public string DefinitionId;

        /// Field path relative to the definition (e.g. "levels[2].zoneRadius"); null when not applicable.
        public string FieldPath;

        /// User-facing Korean message (cause and, when possible, how to fix it).
        public string Message;

        /// 1-based line in the file; 0 when unknown.
        public int Line;

        /// 1-based column in the file; 0 when unknown.
        public int Column;

        /// Creates an empty issue (fields are set by the caller).
        public ValidationIssue()
        {
        }

        /// Creates an issue with every field.
        public ValidationIssue(IssueSeverity severity, string fileName, string definitionId, string fieldPath, string message, int line = 0, int column = 0)
        {
            Severity = severity;
            FileName = fileName;
            DefinitionId = definitionId;
            FieldPath = fieldPath;
            Message = message;
            Line = line;
            Column = column;
        }

        /// Formats as "[weapons.json:12:5] W3.levels[2].zoneRadius: message" (position, id and path omitted when unknown).
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(string.IsNullOrEmpty(FileName) ? "data" : FileName);
            if (Line > 0)
            {
                sb.Append(':').Append(Line.ToString(CultureInfo.InvariantCulture));
                if (Column > 0)
                {
                    sb.Append(':').Append(Column.ToString(CultureInfo.InvariantCulture));
                }
            }

            sb.Append("] ");
            bool hasId = !string.IsNullOrEmpty(DefinitionId);
            bool hasPath = !string.IsNullOrEmpty(FieldPath);
            if (hasId)
            {
                sb.Append(DefinitionId);
            }

            if (hasPath)
            {
                if (hasId)
                {
                    sb.Append('.');
                }

                sb.Append(FieldPath);
            }

            if (hasId || hasPath)
            {
                sb.Append(": ");
            }

            sb.Append(Message);
            return sb.ToString();
        }
    }
}
