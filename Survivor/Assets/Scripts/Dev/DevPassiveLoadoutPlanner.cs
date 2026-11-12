using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;

namespace Survivor.Dev
{
    /// Owner of the dev passive loadout rules (5.15.5, SPEC F19 16/71): turns an overlay request (passives at levels 0..max)
    /// into the full slot list that PendingCommand.SetPassiveLoadout applies (PassiveId null = empty slot). Pure; the player
    /// side only applies the list (same slot + same id keeps the instance). Unlike weapons, an all-zero request is allowed.
    public static class DevPassiveLoadoutPlanner
    {
        /// slots = Session.Passives.Slots, data = Data.Current. On success slotsOut holds exactly GameRules.MaxPassiveSlots
        /// entries (slot order; PassiveId null = empty slot) and refusal is null; on refusal slotsOut is cleared and refusal
        /// holds the English reason. Null arguments throw.
        public static bool TryPlan(IReadOnlyList<PassiveInstance> slots, GameData data, IReadOnlyList<PassiveLoadoutEntry> request,
                                   List<PassiveLoadoutEntry> slotsOut, out string refusal)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            var current = new List<PlannerSlot>(GameRules.MaxPassiveSlots);
            for (int s = 0; s < GameRules.MaxPassiveSlots; s++)
            {
                PassiveInstance passive = s < slots.Count ? slots[s] : null;
                current.Add(passive == null ? default : new PlannerSlot(passive.Id, passive.Level, passive.Definition.Levels.Count));
            }

            return TryPlan(current, data, request, slotsOut, out refusal);
        }

        // Same rules over plain slot values (PassiveInstance cannot be built outside the simulation; tests use this overload).
        internal static bool TryPlan(IReadOnlyList<PlannerSlot> current, GameData data, IReadOnlyList<PassiveLoadoutEntry> request,
                                     List<PassiveLoadoutEntry> slotsOut, out string refusal)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (slotsOut == null)
            {
                throw new ArgumentNullException(nameof(slotsOut));
            }

            slotsOut.Clear();
            refusal = Validate(current, data, request) ?? Place(current, request, slotsOut);
            if (refusal != null)
            {
                slotsOut.Clear();
                return false;
            }

            return true;
        }

        // Rule 1: known passives, levels 0..levelCount (the owned instance's definition when held, else the data's), no repeats.
        private static string Validate(IReadOnlyList<PlannerSlot> current, GameData data, IReadOnlyList<PassiveLoadoutEntry> request)
        {
            for (int i = 0; i < request.Count; i++)
            {
                PassiveLoadoutEntry entry = request[i];
                if (string.IsNullOrEmpty(entry.PassiveId) || !data.TryGetPassive(entry.PassiveId, out PassiveDefinition definition))
                {
                    return "Unknown passive '" + entry.PassiveId + "'.";
                }

                int owned = IndexOf(current, entry.PassiveId);
                int levels = owned >= 0 ? current[owned].LevelCount : definition.Levels != null ? definition.Levels.Count : 0;
                if (entry.Level < 0 || entry.Level > levels)
                {
                    return entry.PassiveId + " has levels 0.." + levels.ToString(CultureInfo.InvariantCulture) + ".";
                }

                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(request[j].PassiveId, entry.PassiveId, StringComparison.Ordinal))
                    {
                        return "Passive '" + entry.PassiveId + "' appears twice.";
                    }
                }
            }

            return null;
        }

        // Rules 2 and 3: owned passives requested at 1+ stay in their slot at the requested level, owned passives at 0 or missing
        // from the request leave; the remaining requested passives fill the lowest empty slots in request order. No passive at
        // all is a valid result.
        private static string Place(IReadOnlyList<PlannerSlot> current, IReadOnlyList<PassiveLoadoutEntry> request, List<PassiveLoadoutEntry> slotsOut)
        {
            var placed = new bool[request.Count];
            for (int s = 0; s < GameRules.MaxPassiveSlots; s++)
            {
                PlannerSlot slot = s < current.Count ? current[s] : default;
                PassiveLoadoutEntry entry = default;
                if (slot.Id != null)
                {
                    int r = IndexOf(request, slot.Id);
                    if (r >= 0)
                    {
                        placed[r] = true;
                        if (request[r].Level >= 1)
                        {
                            entry = request[r];
                        }
                    }
                }

                slotsOut.Add(entry);
            }

            for (int r = 0; r < request.Count; r++)
            {
                if (placed[r] || request[r].Level < 1)
                {
                    continue;
                }

                int empty = slotsOut.FindIndex(e => e.PassiveId == null);
                if (empty < 0)
                {
                    return "Passive loadout needs more than " + GameRules.MaxPassiveSlots.ToString(CultureInfo.InvariantCulture) + " passive slots.";
                }

                slotsOut[empty] = request[r];
            }

            return null;
        }

        private static int IndexOf(IReadOnlyList<PassiveLoadoutEntry> request, string id)
        {
            for (int i = 0; i < request.Count; i++)
            {
                if (string.Equals(request[i].PassiveId, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOf(IReadOnlyList<PlannerSlot> current, string id)
        {
            for (int s = 0; s < current.Count && s < GameRules.MaxPassiveSlots; s++)
            {
                if (current[s].Id != null && string.Equals(current[s].Id, id, StringComparison.Ordinal))
                {
                    return s;
                }
            }

            return -1;
        }

        /// One current slot as plain values: passive id (null = empty), 1-based level, level count of the owned instance's
        /// definition (the level range PLAYER checks for a kept passive).
        internal readonly struct PlannerSlot
        {
            internal readonly string Id;
            internal readonly int Level;
            internal readonly int LevelCount;

            internal PlannerSlot(string id, int level, int levelCount)
            {
                Id = id;
                Level = level;
                LevelCount = levelCount;
            }
        }
    }
}
