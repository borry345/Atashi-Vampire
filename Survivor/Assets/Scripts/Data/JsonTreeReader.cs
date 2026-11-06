using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Survivor.Data
{
    /// Step 2 of the pipeline: parses one file into a JSON tree (Newtonsoft) and decides file-level failure. No binding here.
    internal static class JsonTreeReader
    {
        internal const int SupportedSchemaVersion = 1;
        internal const string SchemaVersionKey = "schemaVersion";

        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            LineInfoHandling = LineInfoHandling.Load,
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
            CommentHandling = CommentHandling.Ignore,
        };

        /// Returns true for the four files whose file-level failure blocks the run.
        internal static bool IsRequired(DataFileKind kind)
        {
            return kind == DataFileKind.Weapons || kind == DataFileKind.Monsters
                || kind == DataFileKind.SpawnTable || kind == DataFileKind.PlayerStats;
        }

        /// Severity of a file-level failure: Blocking for required files, Warning (with fallback) for optional ones.
        internal static IssueSeverity FileFailureSeverity(DataFileKind kind)
        {
            return IsRequired(kind) ? IssueSeverity.Blocking : IssueSeverity.Warning;
        }

        /// Records a file-level failure with the policy suffix (blocked / fallback).
        internal static void AddFileFailure(DataFileKind kind, DataValidationReport report, string message, int line = 0, int column = 0)
        {
            string suffix = IsRequired(kind)
                ? " 필수 파일이라 게임을 시작할 수 없습니다."
                : " 선택 파일이라 기본값으로 대체하고 계속합니다.";
            report.Add(new ValidationIssue(FileFailureSeverity(kind), GameDataLoader.FileNameOf(kind), null, null, message + suffix, line, column));
        }

        /// Parses text into the root object; returns null (after recording a file-level failure) when the text is missing, malformed, not an object or has an unsupported schemaVersion.
        internal static JObject Read(DataFileKind kind, string text, DataValidationReport report)
        {
            if (text == null)
            {
                AddFileFailure(kind, report, "파일이 없습니다(StreamingAssets/Data/" + GameDataLoader.FileNameOf(kind) + ").");
                return null;
            }

            if (text.Length > 0 && text[0] == '﻿')
            {
                text = text.Substring(1);
            }

            JToken root;
            try
            {
                using (var stringReader = new StringReader(text))
                using (var reader = new JsonTextReader(stringReader))
                {
                    // Keep strings as strings (no date sniffing) and numbers as double; the reader is culture-invariant.
                    reader.DateParseHandling = DateParseHandling.None;
                    reader.FloatParseHandling = FloatParseHandling.Double;
                    root = JToken.ReadFrom(reader, LoadSettings);
                    while (reader.Read())
                    {
                        if (reader.TokenType != JsonToken.Comment)
                        {
                            throw new JsonReaderException("Additional text found after the root value.", reader.Path, reader.LineNumber, reader.LinePosition, null);
                        }
                    }
                }
            }
            catch (JsonReaderException e)
            {
                AddFileFailure(kind, report, "JSON 문법 오류로 파일 전체를 읽지 못했습니다(" + e.Message + "). 표시된 위치 근처의 쉼표·괄호·따옴표를 확인하세요.", e.LineNumber, e.LinePosition);
                return null;
            }
            catch (JsonException e)
            {
                AddFileFailure(kind, report, "JSON 문법 오류로 파일 전체를 읽지 못했습니다(" + e.Message + ").");
                return null;
            }

            var rootObject = root as JObject;
            if (rootObject == null)
            {
                AddFileFailure(kind, report, "최상위 값은 객체({ ... })여야 합니다.", LineOf(root), ColumnOf(root));
                return null;
            }

            JToken version;
            if (!rootObject.TryGetValue(SchemaVersionKey, out version))
            {
                AddFileFailure(kind, report, "필수 필드 schemaVersion이 없습니다. 첫 필드로 \"schemaVersion\": 1을 추가하세요.", LineOf(rootObject), ColumnOf(rootObject));
                return null;
            }

            if (version.Type != JTokenType.Integer || !IsSupportedVersion(version))
            {
                AddFileFailure(kind, report, "지원하지 않는 schemaVersion입니다(현재 값: " + version.ToString(Formatting.None) + ", 지원: "
                    + SupportedSchemaVersion.ToString(CultureInfo.InvariantCulture) + ").", LineOf(version), ColumnOf(version));
                return null;
            }

            return rootObject;
        }

        /// 1-based line of a token, 0 when unknown.
        internal static int LineOf(JToken token)
        {
            IJsonLineInfo info = token;
            return info != null && info.HasLineInfo() ? info.LineNumber : 0;
        }

        /// 1-based column of a token, 0 when unknown.
        internal static int ColumnOf(JToken token)
        {
            IJsonLineInfo info = token;
            return info != null && info.HasLineInfo() ? info.LinePosition : 0;
        }

        private static bool IsSupportedVersion(JToken version)
        {
            object value = ((JValue)version).Value;
            return value is long l && l == SupportedSchemaVersion;
        }
    }
}
