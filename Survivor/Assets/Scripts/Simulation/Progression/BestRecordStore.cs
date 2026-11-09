using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Simulation
{
    /// Best survival time in steps, kept in PlayerPrefs (TAC-F8-3). Only the Death/Clear result path records; an error stop,
    /// a forced quit and "back to title" never do.
    public sealed class BestRecordStore
    {
        /// PlayerPrefs key of the shipping game.
        public const string DefaultKey = "BestSurvivalSteps";

        /// Store over the shipping key.
        public BestRecordStore() : this(DefaultKey)
        {
        }

        /// Store over key (tests use their own key); null or empty throws ArgumentException.
        public BestRecordStore(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("BestRecordStore needs a PlayerPrefs key.", nameof(key));
            }

            Key = key;
        }

        /// PlayerPrefs key in use.
        public string Key { get; }

        /// True with the stored best survival steps; false when nothing was recorded yet (the result screen shows "-").
        public bool TryGetBest(out int bestSteps)
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                bestSteps = 0;
                return false;
            }

            bestSteps = PlayerPrefs.GetInt(Key);
            return true;
        }

        /// Records outcome.SurvivalSteps when outcome is Death or Clear, rules.RecordBestTime is true and it beats the stored best
        /// (or none is stored): SetInt, then PlayerPrefs.Save(). Steps above int.MaxValue are stored as int.MaxValue.
        /// Returns true when it saved. Null rules throws ArgumentNullException.
        public bool TryRecord(in RunOutcome outcome, SimulationRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (outcome.Kind == RunOutcomeKind.None || !rules.RecordBestTime)
            {
                return false;
            }

            int survival = outcome.SurvivalSteps > int.MaxValue ? int.MaxValue : (int)Math.Max(0L, outcome.SurvivalSteps);
            if (TryGetBest(out int best) && survival <= best)
            {
                return false;
            }

            PlayerPrefs.SetInt(Key, survival);
            PlayerPrefs.Save();
            return true;
        }
    }
}
