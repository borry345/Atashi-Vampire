using System;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Dev;
using Survivor.Simulation;
using UnityEngine;
using Slot = Survivor.Dev.DevLoadoutPlanner.PlannerSlot;

namespace Survivor.Tests.Dev
{
    /// Plain C# checks of the wave 6b dev conveniences (5.15.5): DevLoadoutPlanner rules over the real data (TAC-F19-15),
    /// ClampZoom (TAC-F19-17), DevCommandLog ring and the command / chest line format (TAC-F19-19).
    public sealed class DevConveniencePureTests
    {
        // Hangul display names as escapes (file encoding independent): "투사체", "연사 투사체".
        private const string W1Name = "\uD22C\uC0AC\uCCB4";
        private const string E1Name = "\uC5F0\uC0AC \uD22C\uC0AC\uCCB4";

        private static readonly string[] NoneConsumed = new string[0];
        private GameData _data;

        [OneTimeSetUp]
        public void LoadData()
        {
            (GameData data, DataValidationReport report) = GameDataLoader.LoadAll(Application.streamingAssetsPath);
            Assert.IsNotNull(data, "StreamingAssets data loads");
            Assert.IsTrue(report.IsPlayable);
            _data = data;
        }

        [Test]
        public void Plan_RemovedFirstSlotIsReusedByTheNewWeapon()
        {
            // [W1] with W1:0, W3:2 -> [W3:2, -, -, -].
            AssertPlan(Slots(Base("W1", 1)), NoneConsumed, Request(("W1", 0), ("W3", 2)), ("W3", 2), null, null, null);
        }

        [Test]
        public void Plan_ZeroEmptiesItsSlot_OthersKeepTheirSlots()
        {
            // [W1, W2, W3] with W2:0 (rest kept) -> [W1, -, W3]; W3 stays in slot 2.
            Slot[] three = Slots(Base("W1", 2), Base("W2", 3), Base("W3", 4));
            AssertPlan(three, NoneConsumed, Request(("W1", 2), ("W2", 0), ("W3", 4)), ("W1", 2), null, ("W3", 4), null);

            // Then W4:1 -> the empty slot 1 (lowest empty), W3 still in slot 2.
            Slot[] gap = Slots(Base("W1", 2), default, Base("W3", 4));
            AssertPlan(gap, NoneConsumed, Request(("W1", 2), ("W3", 4), ("W4", 1)), ("W1", 2), ("W4", 1), ("W3", 4), null);
        }

        [Test]
        public void Plan_LevelChangeKeepsTheSlot_AndMissingBaseWeaponIsRemoved()
        {
            Slot[] two = Slots(Base("W1", 5), Base("W2", 3));
            AssertPlan(two, NoneConsumed, Request(("W2", 1)), null, ("W2", 1), null, null);
            AssertPlan(two, NoneConsumed, Request(("W2", 5), ("W1", 1)), ("W1", 1), ("W2", 5), null, null);
        }

        [Test]
        public void Plan_AllZeroIsRefused()
        {
            Slot[] four = Slots(Base("W1", 1), Base("W2", 1), Base("W3", 1), Base("W4", 1));
            AssertRefused(four, NoneConsumed, Request(("W1", 0), ("W2", 0), ("W3", 0), ("W4", 0)), "Loadout would leave no weapon.");
            AssertRefused(four, NoneConsumed, Request(), "Loadout would leave no weapon.");
        }

        [Test]
        public void Plan_EvolvedWeaponStays_OriginalAndEvolvedIdsRefused()
        {
            // [E1 (slot 0), W2] (W1 consumed by the evolution).
            Slot[] evolved = Slots(Evolved("E1"), Base("W2", 2));
            string[] consumed = { "W1" };
            AssertPlan(evolved, consumed, Request(("W2", 0)), ("E1", 1), null, null, null);
            AssertPlan(evolved, consumed, Request(("W1", 0), ("W2", 2), ("W4", 3)), ("E1", 1), ("W2", 2), ("W4", 3), null);
            AssertRefused(evolved, consumed, Request(("W1", 1), ("W2", 2)), "W1 was evolved; set it to 0.");
            AssertRefused(evolved, consumed, Request(("E1", 1)), "E1 is an evolved weapon; evolution is the only way to get it.");
            AssertRefused(evolved, consumed, Request(("E1", 0)), "E1 is an evolved weapon; evolution is the only way to get it.");
        }

        [Test]
        public void Plan_OnlyAnEvolvedWeaponLeft_AllZeroPasses()
        {
            AssertPlan(Slots(Evolved("E1")), new[] { "W1" }, Request(("W1", 0), ("W2", 0), ("W3", 0), ("W4", 0)), ("E1", 1), null, null, null);
        }

        [Test]
        public void Plan_RefusesUnknownIdsLevelsAndRepeats()
        {
            Slot[] one = Slots(Base("W1", 1));
            AssertRefused(one, NoneConsumed, Request(("NOPE", 1)), "Unknown weapon 'NOPE'.");
            AssertRefused(one, NoneConsumed, Request(("", 1)), "Unknown weapon ''.");
            AssertRefused(one, NoneConsumed, Request((null, 1)), "Unknown weapon ''.");
            AssertRefused(one, NoneConsumed, Request(("W1", 6)), "W1 has levels 0..5.");
            AssertRefused(one, NoneConsumed, Request(("W1", -1)), "W1 has levels 0..5.");
            AssertRefused(one, NoneConsumed, Request(("W1", 1), ("W1", 2)), "Weapon 'W1' appears twice.");
        }

        [Test]
        public void Plan_EvolvedWeaponMissingFromTheDataIsRefused()
        {
            AssertRefused(Slots(Evolved("E9"), Base("W2", 1)), new[] { "W9" }, Request(("W2", 1)), "E9 is not in the current data.");
        }

        [Test]
        public void Plan_NullArgumentsThrow()
        {
            var output = new List<WeaponLoadoutEntry>();
            IReadOnlyList<WeaponLoadoutEntry> request = Request(("W1", 1));
            Slot[] one = Slots(Base("W1", 1));
            Assert.Throws<ArgumentNullException>(() => DevLoadoutPlanner.TryPlan((IReadOnlyList<WeaponInstance>)null, NoneConsumed, _data, request, output, out _));
            Assert.Throws<ArgumentNullException>(() => DevLoadoutPlanner.TryPlan(one, null, _data, request, output, out _));
            Assert.Throws<ArgumentNullException>(() => DevLoadoutPlanner.TryPlan(one, NoneConsumed, null, request, output, out _));
            Assert.Throws<ArgumentNullException>(() => DevLoadoutPlanner.TryPlan(one, NoneConsumed, _data, null, output, out _));
            Assert.Throws<ArgumentNullException>(() => DevLoadoutPlanner.TryPlan(one, NoneConsumed, _data, request, null, out _));
        }

        [TestCase(1f, 4.5f)]
        [TestCase(4.4f, 4.5f)]
        [TestCase(4.5f, 4.5f)]
        [TestCase(9f, 9f)]
        [TestCase(12f, 12f)]
        [TestCase(27f, 27f)]
        [TestCase(30f, 27f)]
        [TestCase(float.PositiveInfinity, 27f)]
        [TestCase(float.NegativeInfinity, 4.5f)]
        [TestCase(float.NaN, 9f)]
        public void ClampZoom_Table(float requested, float expected)
        {
            Assert.AreEqual(expected, DevController.ClampZoom(requested));
        }

        [Test]
        public void ZoomRange_IsTheContractRange()
        {
            Assert.AreEqual(4.5f, DevController.MinZoom);
            Assert.AreEqual(27f, DevController.MaxZoom);
            Assert.AreEqual(9f, GameRules.CameraOrthographicSize);
        }

        [Test]
        public void CommandLog_KeepsTheLatestTenNewestFirst()
        {
            var log = new DevCommandLog();
            Assert.AreEqual(0, log.Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => log.NewestFirst(0));
            for (int i = 1; i <= 12; i++)
            {
                log.Add("line " + i);
                Assert.AreEqual(Math.Min(i, DevCommandLog.Capacity), log.Count);
                Assert.AreEqual("line " + i, log.NewestFirst(0));
            }

            Assert.AreEqual(10, DevCommandLog.Capacity);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual("line " + (12 - i), log.NewestFirst(i));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => log.NewestFirst(10));
            Assert.Throws<ArgumentOutOfRangeException>(() => log.NewestFirst(-1));
            Assert.Throws<ArgumentNullException>(() => log.Add(null));
            Assert.AreEqual("line 12", log.NewestFirst(0), "a refused Add changes nothing");
        }

        [Test]
        public void LineFormat_RunStepCommandMessage()
        {
            Assert.AreEqual("#2 3900 Loadout: Loadout of 2 weapon(s) queued.",
                DevController.FormatLine(2, 3900, "Loadout", "Loadout of 2 weapon(s) queued."));
            Assert.AreEqual("#0 0 FixedSeed: Seed for the next run: 4242.", DevController.FormatLine(0, 0, "FixedSeed", "Seed for the next run: 4242."));
        }

        [Test]
        public void ChestText_UpgradeEvolutionAndHeal()
        {
            Assert.AreEqual(W1Name + " Lv 3\u21924", DevController.ChestText(new ChestResult(1, 50, ChestEffect.WeaponUpgraded, "W1", 3, 4, 0f), _data));
            Assert.AreEqual(E1Name + " (evolved)", DevController.ChestText(new ChestResult(2, 60, ChestEffect.Evolved, "E1", 0, 1, 0f), _data));
            Assert.AreEqual("Heal +30.0", DevController.ChestText(new ChestResult(3, 70, ChestEffect.Healed, null, 0, 0, 30f), _data));
            Assert.AreEqual("Heal +0.0", DevController.ChestText(new ChestResult(4, 70, ChestEffect.Healed, null, 0, 0, 0f), _data));
            Assert.AreEqual("W9 Lv 1\u21922", DevController.ChestText(new ChestResult(5, 80, ChestEffect.WeaponUpgraded, "W9", 1, 2, 0f), _data),
                "id when the weapon is not in the data");
        }

        private void AssertPlan(Slot[] current, IReadOnlyList<string> consumed, IReadOnlyList<WeaponLoadoutEntry> request,
                                params (string id, int level)?[] expected)
        {
            var output = new List<WeaponLoadoutEntry> { new WeaponLoadoutEntry("stale", 1) };
            Assert.IsTrue(DevLoadoutPlanner.TryPlan(current, consumed, _data, request, output, out string refusal), refusal);
            Assert.IsNull(refusal);
            Assert.AreEqual(GameRules.MaxWeaponSlots, output.Count, "one entry per slot");
            for (int s = 0; s < GameRules.MaxWeaponSlots; s++)
            {
                if (expected[s] == null)
                {
                    Assert.IsNull(output[s].WeaponId, "slot " + s + " empty");
                    continue;
                }

                Assert.AreEqual(expected[s].Value.id, output[s].WeaponId, "slot " + s + " id");
                Assert.AreEqual(expected[s].Value.level, output[s].Level, "slot " + s + " level");
            }
        }

        private void AssertRefused(Slot[] current, IReadOnlyList<string> consumed, IReadOnlyList<WeaponLoadoutEntry> request, string reason)
        {
            var output = new List<WeaponLoadoutEntry> { new WeaponLoadoutEntry("stale", 1) };
            Assert.IsFalse(DevLoadoutPlanner.TryPlan(current, consumed, _data, request, output, out string refusal));
            Assert.AreEqual(reason, refusal);
            Assert.AreEqual(0, output.Count, "no plan on refusal");
        }

        private static Slot[] Slots(params Slot[] used)
        {
            var slots = new Slot[GameRules.MaxWeaponSlots];
            Array.Copy(used, slots, used.Length);
            return slots;
        }

        private static Slot Base(string id, int level)
        {
            return new Slot(id, level, false);
        }

        private static Slot Evolved(string id)
        {
            return new Slot(id, 1, true);
        }

        private static IReadOnlyList<WeaponLoadoutEntry> Request(params (string id, int level)[] entries)
        {
            var request = new List<WeaponLoadoutEntry>(entries.Length);
            foreach ((string id, int level) in entries)
            {
                request.Add(new WeaponLoadoutEntry(id, level));
            }

            return request;
        }
    }
}
