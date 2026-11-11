using System;
using System.Collections.Generic;
using System.Globalization;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;

namespace Survivor.Dev
{
    /// Owner of the dev loadout rules (5.15.5, SPEC F19 70, Q50 9/17/18): turns an overlay request (base weapons at levels
    /// 0..n) into the full slot list that PendingCommand.SetLoadout applies (WeaponId null = empty slot). Pure; the player
    /// side only applies the list (same slot + same id keeps the instance).
    public static class DevLoadoutPlanner
    {
        /// slots = Session.Weapons.Slots, consumedByEvolution = Session.Weapons.ConsumedByEvolution, data = Data.Current.
        /// On success slotsOut holds exactly GameRules.MaxWeaponSlots entries (slot order; WeaponId null = empty slot) and
        /// refusal is null; on refusal slotsOut is cleared and refusal holds the English reason. Null arguments throw.
        public static bool TryPlan(IReadOnlyList<WeaponInstance> slots, IReadOnlyList<string> consumedByEvolution, GameData data,
                                   IReadOnlyList<WeaponLoadoutEntry> request, List<WeaponLoadoutEntry> slotsOut, out string refusal)
        {
            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            var current = new List<PlannerSlot>(GameRules.MaxWeaponSlots);
            for (int s = 0; s < GameRules.MaxWeaponSlots; s++)
            {
                WeaponInstance weapon = s < slots.Count ? slots[s] : null;
                current.Add(weapon == null ? default : new PlannerSlot(weapon.Id, weapon.Level, weapon.Definition.EvolvedOnly));
            }

            return TryPlan(current, consumedByEvolution, data, request, slotsOut, out refusal);
        }

        // Same rules over plain slot values (WeaponInstance cannot be built outside the simulation; tests use this overload).
        internal static bool TryPlan(IReadOnlyList<PlannerSlot> current, IReadOnlyList<string> consumedByEvolution, GameData data,
                                     IReadOnlyList<WeaponLoadoutEntry> request, List<WeaponLoadoutEntry> slotsOut, out string refusal)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (consumedByEvolution == null)
            {
                throw new ArgumentNullException(nameof(consumedByEvolution));
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
            refusal = Validate(consumedByEvolution, data, request) ?? Place(current, request, slotsOut) ?? CheckEvolvedInData(current, data);
            if (refusal != null)
            {
                slotsOut.Clear();
                return false;
            }

            return true;
        }

        // Rules 1 and 2: known base weapons, levels 0..n, no repeats, no consumed original above 0.
        private static string Validate(IReadOnlyList<string> consumedByEvolution, GameData data, IReadOnlyList<WeaponLoadoutEntry> request)
        {
            for (int i = 0; i < request.Count; i++)
            {
                WeaponLoadoutEntry entry = request[i];
                if (string.IsNullOrEmpty(entry.WeaponId) || !data.TryGetWeapon(entry.WeaponId, out WeaponDefinition definition))
                {
                    return "Unknown weapon '" + entry.WeaponId + "'.";
                }

                if (definition.EvolvedOnly)
                {
                    return entry.WeaponId + " is an evolved weapon; evolution is the only way to get it.";
                }

                int levels = definition.Levels != null ? definition.Levels.Count : 0;
                if (entry.Level < 0 || entry.Level > levels)
                {
                    return entry.WeaponId + " has levels 0.." + levels.ToString(CultureInfo.InvariantCulture) + ".";
                }

                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(request[j].WeaponId, entry.WeaponId, StringComparison.Ordinal))
                    {
                        return "Weapon '" + entry.WeaponId + "' appears twice.";
                    }
                }
            }

            for (int i = 0; i < request.Count; i++)
            {
                if (request[i].Level >= 1 && Contains(consumedByEvolution, request[i].WeaponId))
                {
                    return request[i].WeaponId + " was evolved; set it to 0.";
                }
            }

            return null;
        }

        // Rules 3 and 4: evolved weapons stay in their slot at their level, kept base weapons stay in their slot at the requested
        // level, base weapons at 0 or missing from the request leave; the remaining requested weapons fill the lowest empty slots
        // in request order.
        private static string Place(IReadOnlyList<PlannerSlot> current, IReadOnlyList<WeaponLoadoutEntry> request, List<WeaponLoadoutEntry> slotsOut)
        {
            var placed = new bool[request.Count];
            for (int s = 0; s < GameRules.MaxWeaponSlots; s++)
            {
                PlannerSlot slot = s < current.Count ? current[s] : default;
                WeaponLoadoutEntry entry = default;
                if (slot.Id != null)
                {
                    if (slot.EvolvedOnly)
                    {
                        entry = new WeaponLoadoutEntry(slot.Id, slot.Level);
                    }
                    else
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
                }

                slotsOut.Add(entry);
            }

            for (int r = 0; r < request.Count; r++)
            {
                if (placed[r] || request[r].Level < 1)
                {
                    continue;
                }

                int empty = slotsOut.FindIndex(e => e.WeaponId == null);
                if (empty < 0)
                {
                    return "Loadout needs more than " + GameRules.MaxWeaponSlots.ToString(CultureInfo.InvariantCulture) + " weapon slots.";
                }

                slotsOut[empty] = request[r];
            }

            for (int s = 0; s < slotsOut.Count; s++)
            {
                if (slotsOut[s].WeaponId != null)
                {
                    return null;
                }
            }

            return "Loadout would leave no weapon.";
        }

        // Rule 5: an evolved weapon kept by the plan must still be defined, or SetLoadout would throw looking it up.
        private static string CheckEvolvedInData(IReadOnlyList<PlannerSlot> current, GameData data)
        {
            for (int s = 0; s < current.Count && s < GameRules.MaxWeaponSlots; s++)
            {
                PlannerSlot slot = current[s];
                if (slot.Id != null && slot.EvolvedOnly && !data.TryGetWeapon(slot.Id, out _))
                {
                    return slot.Id + " is not in the current data.";
                }
            }

            return null;
        }

        private static int IndexOf(IReadOnlyList<WeaponLoadoutEntry> request, string id)
        {
            for (int i = 0; i < request.Count; i++)
            {
                if (string.Equals(request[i].WeaponId, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool Contains(IReadOnlyList<string> ids, string id)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// One current slot as plain values: weapon id (null = empty), 1-based level, evolved-only flag.
        internal readonly struct PlannerSlot
        {
            internal readonly string Id;
            internal readonly int Level;
            internal readonly bool EvolvedOnly;

            internal PlannerSlot(string id, int level, bool evolvedOnly)
            {
                Id = id;
                Level = level;
                EvolvedOnly = evolvedOnly;
            }
        }
    }
}
