using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Survivor.Data
{
    /// Shared loop for files of the form { "schemaVersion": 1, "<array>": [ { "id": ... }, ... ] }.
    internal static class BinderSupport
    {
        /// Binds every element of root[arrayName]; invalid or duplicate definitions are skipped with warnings. Returns false when the array itself is missing (file-level failure already recorded).
        internal static bool BindList<T>(JObject root, DataFileKind kind, string arrayName, DataValidationReport report, List<T> output, Func<FieldReader, T> bindOne)
            where T : class
        {
            string fileName = GameDataLoader.FileNameOf(kind);
            var top = new FieldReader(root, fileName, null, null, report, JsonTreeReader.FileFailureSeverity(kind));
            top.Known(JsonTreeReader.SchemaVersionKey);
            JArray array = top.ArrayField(arrayName, true);
            top.ReportUnknownKeys();
            if (array == null)
            {
                JsonTreeReader.AddFileFailure(kind, report, "정의 배열 '" + arrayName + "'을(를) 읽지 못했습니다.", JsonTreeReader.LineOf(root), JsonTreeReader.ColumnOf(root));
                return false;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < array.Count; i++)
            {
                string elementPath = arrayName + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                var obj = array[i] as JObject;
                if (obj == null)
                {
                    report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, null, elementPath,
                        "정의는 객체여야 합니다(현재: " + FieldReader.TypeName(array[i]) + "). 이 항목을 건너뜁니다.",
                        JsonTreeReader.LineOf(array[i]), JsonTreeReader.ColumnOf(array[i])));
                    continue;
                }

                string peekId = PeekId(obj);
                var reader = new FieldReader(obj, fileName, peekId, peekId == null ? elementPath : null, report, IssueSeverity.Warning);
                string id = reader.String("id", true, true);
                if (id != null && !seen.Add(id))
                {
                    reader.Error("id", "ID가 중복됩니다. 앞서 나온 같은 ID의 정의만 쓰고 이 정의는 건너뜁니다. 다른 ID로 바꾸세요.", obj["id"]);
                    continue;
                }

                T definition = bindOne(reader);
                reader.ReportUnknownKeysIfNotDone();
                if (reader.HasErrors || definition == null)
                {
                    AddSkipped(report, fileName, peekId, peekId == null ? elementPath : null, reader.ErrorCount, obj);
                    continue;
                }

                output.Add(definition);
            }

            return true;
        }

        /// Records the "definition skipped" summary warning.
        internal static void AddSkipped(DataValidationReport report, string fileName, string definitionId, string path, int errorCount, JToken at)
        {
            report.Add(new ValidationIssue(IssueSeverity.Warning, fileName, definitionId, path,
                "오류 " + errorCount.ToString(CultureInfo.InvariantCulture) + "건이 있어 이 정의를 건너뜁니다.",
                JsonTreeReader.LineOf(at), JsonTreeReader.ColumnOf(at)));
        }

        /// Returns a valid string id of the object, or null.
        internal static string PeekId(JObject obj)
        {
            JToken token;
            if (obj.TryGetValue("id", out token) && token.Type == JTokenType.String)
            {
                string id = (string)token;
                return FieldReader.IsValidId(id) ? id : null;
            }

            return null;
        }
    }
}
