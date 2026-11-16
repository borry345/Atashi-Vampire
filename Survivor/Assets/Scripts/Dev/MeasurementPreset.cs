using System;
using System.Collections.Generic;
using Survivor.Core;
using Survivor.Simulation;

namespace Survivor.Dev
{
    /// One measurement configuration (TAC-F10-4): label and group, strategy selection, monster count N (fixed for B, a
    /// default the user may change for A0 / A / C), the fixed weapon loadout and the chest drop chance (0 for every preset).
    public sealed class MeasurementPreset
    {
        /// Creates a preset. label, group and loadout are required (ArgumentNullException); defaultN must be positive.
        public MeasurementPreset(string label, string group, StrategySelection selection, int defaultN, bool nEditable,
                                 IReadOnlyList<WeaponLoadoutEntry> weaponLoadout, string weaponLoadoutText, float chestDropChance)
        {
            if (defaultN <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defaultN), defaultN, "Monster count N must be positive.");
            }

            Label = label ?? throw new ArgumentNullException(nameof(label));
            Group = group ?? throw new ArgumentNullException(nameof(group));
            Selection = selection;
            DefaultN = defaultN;
            NEditable = nEditable;
            WeaponLoadout = weaponLoadout;
            WeaponLoadoutText = weaponLoadoutText ?? string.Empty;
            ChestDropChance = chestDropChance;
        }

        /// Preset name written as the CSV label (e.g. "A3").
        public string Label { get; }

        /// "A0", "A", "B" or "C".
        public string Group { get; }

        /// Strategies of the measured run.
        public StrategySelection Selection { get; }

        /// N used when the user does not change it (the table value for B).
        public int DefaultN { get; }

        /// True when the overlay lets the user set N (A0, A, C); B keeps its table N.
        public bool NEditable { get; }

        /// Fixed weapon loadout applied at step 0 (null = none, which the guard refuses).
        public IReadOnlyList<WeaponLoadoutEntry> WeaponLoadout { get; }

        /// Loadout as written in the CSV header ("W1:5;W2:5;W3:5;W4:5").
        public string WeaponLoadoutText { get; }

        /// Chest drop chance during the run (0; anything else is refused by the guard).
        public float ChestDropChance { get; }

        /// N for a request: requested when the preset is editable and requested > 0, else DefaultN.
        public int ResolveN(int requested)
        {
            return NEditable && requested > 0 ? requested : DefaultN;
        }
    }
}
