using System;
using System.Collections.Generic;

namespace Survivor.Data
{
    /// Collects every validation issue of one load; each added issue is logged once.
    public sealed class DataValidationReport
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
        private int _blockingCount;

        /// All issues in the order they were added.
        public IReadOnlyList<ValidationIssue> Issues => _issues;

        /// Blocking issues in the order they were added.
        public IEnumerable<ValidationIssue> BlockingIssues
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == IssueSeverity.Blocking)
                    {
                        yield return _issues[i];
                    }
                }
            }
        }

        /// True when there is no Blocking issue. PlayabilityChecker records each failed playability criterion as a Blocking issue, so this covers all four criteria.
        public bool IsPlayable => _blockingCount == 0;

        /// Number of Warning issues.
        public int WarningCount => _issues.Count - _blockingCount;

        /// Number of Blocking issues.
        public int BlockingCount => _blockingCount;

        /// Adds an issue and logs it (Warning -> LogWarning, Blocking -> LogError).
        public void Add(ValidationIssue issue)
        {
            if (issue == null)
            {
                throw new ArgumentNullException(nameof(issue));
            }

            _issues.Add(issue);
            if (issue.Severity == IssueSeverity.Blocking)
            {
                _blockingCount++;
                DataLog.Error(issue.ToString());
            }
            else
            {
                DataLog.Warning(issue.ToString());
            }
        }
    }
}
