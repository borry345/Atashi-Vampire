using System;
using System.Collections.Generic;
using Survivor.Data;
using Survivor.Simulation;

namespace Survivor.Dev
{
    /// JSON hot reload (SPEC F19 data, 5.11.1): loads every file from the data root with GameDataLoader.LoadAll and, when the
    /// report is playable, queues a SwapData command (applied at the next Begin). An unplayable report or a load failure queues
    /// nothing and returns the cause.
    public sealed class HotReloadService
    {
        private readonly string _root;
        private readonly PendingCommandQueue _commands;

        /// Service reading root (Application.streamingAssetsPath in the game) and queueing into commands. Null throws.
        public HotReloadService(string root, PendingCommandQueue commands)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        /// Data root read by TryReload.
        public string Root => _root;

        /// Loads and queues SwapData when playable (true, with a summary); false with the cause otherwise.
        public bool TryReload(out string message)
        {
            GameData data;
            DataValidationReport report;
            try
            {
                (data, report) = GameDataLoader.LoadAll(_root);
            }
            catch (Exception e)
            {
                message = "Hot reload failed: " + e.GetType().Name + ": " + e.Message;
                return false;
            }

            if (report == null || !report.IsPlayable || data == null)
            {
                message = "Hot reload refused: data not playable (" + Describe(report) + "); current data kept.";
                return false;
            }

            _commands.Enqueue(PendingCommand.SwapData(data, report));
            message = "Hot reload queued (" + report.WarningCount + " warning(s)); applies at the next step.";
            return true;
        }

        // "<n> blocking issue(s), first: <issue>" for the overlay line.
        private static string Describe(DataValidationReport report)
        {
            if (report == null)
            {
                return "no report";
            }

            using (IEnumerator<ValidationIssue> blocking = report.BlockingIssues.GetEnumerator())
            {
                string first = blocking.MoveNext() && blocking.Current != null ? ", first: " + blocking.Current : string.Empty;
                return report.BlockingCount + " blocking issue(s)" + first;
            }
        }
    }
}
