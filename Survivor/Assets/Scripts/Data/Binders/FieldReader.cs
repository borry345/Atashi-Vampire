using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Survivor.Core;

namespace Survivor.Data
{
    /// Allowed range of a number field.
    internal enum NumberRange { Any, NonNegative, Positive }

    /// Reflection-free field binding over one JSON object: required / type / range checks, unknown keys, seconds -> steps.
    /// Errors use the reader's severity and also count toward every parent reader (a child error invalidates the definition).
    internal sealed class FieldReader
    {
        private static readonly Regex IdPattern = new Regex("^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant);

        private readonly JObject _obj;
        private readonly string _fileName;
        private readonly string _definitionId;
        private readonly string _prefix;
        private readonly DataValidationReport _report;
        private readonly IssueSeverity _severity;
        private readonly FieldReader _parent;
        private readonly HashSet<string> _known = new HashSet<string>(StringComparer.Ordinal);
        private bool _unknownReported;

        internal FieldReader(JObject obj, string fileName, string definitionId, string prefix, DataValidationReport report, IssueSeverity severity)
            : this(obj, fileName, definitionId, prefix, report, severity, null)
        {
        }

        private FieldReader(JObject obj, string fileName, string definitionId, string prefix, DataValidationReport report, IssueSeverity severity, FieldReader parent)
        {
            _obj = obj;
            _fileName = fileName;
            _definitionId = definitionId;
            _prefix = prefix;
            _report = report;
            _severity = severity;
            _parent = parent;
        }

        /// Errors recorded by this reader and its children.
        internal int ErrorCount { get; private set; }

        internal bool HasErrors => ErrorCount > 0;

        internal JObject Object => _obj;

        internal string DefinitionId => _definitionId;

        /// True when the identifier matches ^[A-Za-z0-9_]+$.
        internal static bool IsValidId(string id)
        {
            return id != null && IdPattern.IsMatch(id);
        }

        /// Korean name of a JSON token type for messages.
        internal static string TypeName(JToken token)
        {
            switch (token.Type)
            {
                case JTokenType.String: return "문자열";
                case JTokenType.Integer: return "정수";
                case JTokenType.Float: return "실수";
                case JTokenType.Boolean: return "참/거짓";
                case JTokenType.Object: return "객체";
                case JTokenType.Array: return "배열";
                case JTokenType.Null: return "null";
                default: return token.Type.ToString();
            }
        }

        /// Invariant-culture number text for messages.
        internal static string Num(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// Reader for a nested object; its errors also count here.
        internal FieldReader Child(JObject obj, string relativePath)
        {
            return new FieldReader(obj, _fileName, _definitionId, Combine(relativePath), _report, _severity, this);
        }

        /// Full field path of a key of this object.
        internal string Combine(string name)
        {
            return string.IsNullOrEmpty(_prefix) ? name : _prefix + "." + name;
        }

        /// Records an error at a field of this object (null field = the object itself).
        internal void Error(string field, string message, JToken at)
        {
            for (FieldReader r = this; r != null; r = r._parent)
            {
                r.ErrorCount++;
            }

            AddIssue(_severity, field, message, at);
        }

        /// Records a warning that does not invalidate the definition.
        internal void Warn(string field, string message, JToken at)
        {
            AddIssue(IssueSeverity.Warning, field, message, at);
        }

        /// Marks a key as part of the schema so it is not reported as unknown.
        internal void Known(string name)
        {
            _known.Add(name);
        }

        /// Warns about every key that was not read; keys listed in otherVariantKeys get the "not used by this type" message.
        internal void ReportUnknownKeys(ICollection<string> otherVariantKeys = null, string variantName = null)
        {
            _unknownReported = true;
            foreach (JProperty property in _obj.Properties())
            {
                if (_known.Contains(property.Name))
                {
                    continue;
                }

                string message = otherVariantKeys != null && otherVariantKeys.Contains(property.Name)
                    ? "이 타입('" + variantName + "')에는 쓰이지 않는 키라서 무시합니다."
                    : "알 수 없는 키라서 무시합니다. 철자를 확인하세요.";
                Warn(property.Name, message, property);
            }
        }

        /// Reports unknown keys unless the binder already did (with variant information).
        internal void ReportUnknownKeysIfNotDone()
        {
            if (!_unknownReported)
            {
                ReportUnknownKeys();
            }
        }

        /// Reads a string; null when missing (error if required) or not a string. requireId enforces ^[A-Za-z0-9_]+$.
        internal string String(string name, bool required, bool requireId = false, string fallback = null)
        {
            JToken token = Get(name, required);
            if (token == null)
            {
                return fallback;
            }

            if (token.Type != JTokenType.String)
            {
                Error(name, "문자열이어야 합니다(현재: " + TypeName(token) + ").", token);
                return fallback;
            }

            string value = (string)token;
            if (requireId && !IsValidId(value))
            {
                Error(name, "ID는 영문자·숫자·밑줄만 쓸 수 있습니다(현재 값: \"" + value + "\").", token);
                return fallback;
            }

            if (!requireId && value.Length == 0)
            {
                Error(name, "빈 문자열은 쓸 수 없습니다.", token);
                return fallback;
            }

            return value;
        }

        /// Reads a number (integer or real) and checks its range; returns fallback on any error.
        internal double Number(string name, NumberRange range, bool required, double fallback = 0d)
        {
            JToken token = Get(name, required);
            if (token == null)
            {
                return fallback;
            }

            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            {
                Error(name, "숫자여야 합니다(현재: " + TypeName(token) + ").", token);
                return fallback;
            }

            double value = ToDouble((JValue)token);
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > float.MaxValue)
            {
                Error(name, "유한한 숫자여야 합니다.", token);
                return fallback;
            }

            if (range == NumberRange.Positive && !(value > 0d))
            {
                Error(name, "0보다 커야 합니다(현재 값: " + Num(value) + ").", token);
                return fallback;
            }

            if (range == NumberRange.NonNegative && value < 0d)
            {
                Error(name, "0 이상이어야 합니다(현재 값: " + Num(value) + ").", token);
                return fallback;
            }

            return value;
        }

        /// Reads an integer (a real number such as 1.5 or 2.0 is a type error) that must be at least min.
        internal int Int(string name, int min, bool required, int fallback = 0)
        {
            JToken token = Get(name, required);
            if (token == null)
            {
                return fallback;
            }

            return ParseInt(token, name, min, fallback);
        }

        /// Validates an integer token at a field path (used for array elements too).
        internal int ParseInt(JToken token, string field, int min, int fallback)
        {
            if (token.Type != JTokenType.Integer)
            {
                Error(field, "정수여야 합니다(현재: " + TypeName(token) + ").", token);
                return fallback;
            }

            object raw = ((JValue)token).Value;
            if (!(raw is long value) || value > int.MaxValue || value < int.MinValue)
            {
                Error(field, "정수 범위를 벗어났습니다.", token);
                return fallback;
            }

            if (value < min)
            {
                Error(field, min.ToString(CultureInfo.InvariantCulture) + " 이상이어야 합니다(현재 값: " + value.ToString(CultureInfo.InvariantCulture) + ").", token);
                return fallback;
            }

            return (int)value;
        }

        /// Reads an optional boolean.
        internal bool Bool(string name, bool fallback)
        {
            JToken token = Get(name, false);
            if (token == null)
            {
                return fallback;
            }

            if (token.Type != JTokenType.Boolean)
            {
                Error(name, "true 또는 false여야 합니다(현재: " + TypeName(token) + ").", token);
                return fallback;
            }

            return (bool)token;
        }

        /// Reads a nested object; null when missing or of another type.
        internal JObject ObjectField(string name, bool required)
        {
            JToken token = Get(name, required);
            if (token == null)
            {
                return null;
            }

            var obj = token as JObject;
            if (obj == null)
            {
                Error(name, "객체여야 합니다(현재: " + TypeName(token) + ").", token);
            }

            return obj;
        }

        /// Reads an array; null when missing or of another type.
        internal JArray ArrayField(string name, bool required)
        {
            JToken token = Get(name, required);
            if (token == null)
            {
                return null;
            }

            var array = token as JArray;
            if (array == null)
            {
                Error(name, "배열이어야 합니다(현재: " + TypeName(token) + ").", token);
            }

            return array;
        }

        /// Reads a string enum; false when missing or not one of the names (the message lists the allowed names).
        internal bool TryEnum<T>(string name, string[] names, T[] values, bool required, T fallback, out T result)
        {
            result = fallback;
            JToken token = Get(name, required);
            if (token == null)
            {
                return !required;
            }

            if (token.Type == JTokenType.String)
            {
                string text = (string)token;
                for (int i = 0; i < names.Length; i++)
                {
                    if (string.Equals(names[i], text, StringComparison.Ordinal))
                    {
                        result = values[i];
                        return true;
                    }
                }
            }

            Error(name, "허용되지 않는 값입니다(현재: " + token.ToString(Newtonsoft.Json.Formatting.None) + ", 허용: " + string.Join(" / ", names) + ").", token);
            return false;
        }

        /// R1 interval conversion (at least 1 step) of an already validated seconds value; records an error when it does not fit an int.
        internal int IntervalSteps(string name, double seconds)
        {
            try
            {
                return StepMath.SecondsToIntervalSteps(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                Error(name, "스텝으로 환산할 수 없는 시간입니다(현재 값: " + Num(seconds) + "초).", _obj[name] ?? _obj);
                return 1;
            }
        }

        /// R1 conversion allowing 0 steps of an already validated seconds value; records an error when it does not fit an int.
        internal int Steps(string name, double seconds)
        {
            try
            {
                return StepMath.SecondsToSteps(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                Error(name, "스텝으로 환산할 수 없는 시간입니다(현재 값: " + Num(seconds) + "초).", _obj[name] ?? _obj);
                return 0;
            }
        }

        private JToken Get(string name, bool required)
        {
            _known.Add(name);
            JToken token;
            if (!_obj.TryGetValue(name, out token))
            {
                if (required)
                {
                    Error(name, "필수 필드가 없습니다. 이 필드를 추가하세요.", _obj);
                }

                return null;
            }

            if (token.Type == JTokenType.Null)
            {
                Error(name, "null은 쓸 수 없습니다. 값을 넣거나 필드를 지우세요.", token);
                return null;
            }

            return token;
        }

        private static double ToDouble(JValue value)
        {
            object raw = value.Value;
            if (raw is BigInteger big)
            {
                return (double)big;
            }

            return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
        }

        private void AddIssue(IssueSeverity severity, string field, string message, JToken at)
        {
            string path = field == null ? _prefix : Combine(field);
            _report.Add(new ValidationIssue(severity, _fileName, _definitionId, path, message, JsonTreeReader.LineOf(at), JsonTreeReader.ColumnOf(at)));
        }
    }
}
