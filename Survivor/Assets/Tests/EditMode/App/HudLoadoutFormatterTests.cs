using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.UI;
using UnityEngine;

namespace Survivor.Tests.EditMode.App
{
    /// HUD loadout panel text (TAC-F8-14, 5.17.1): weapon lines "<name> Lv n" or "<name> (진화)", passive lines below, the
    /// multiplier line last with one decimal and a '.' in every culture. ASCII 'x': the font atlas has no U+00D7 (PM decision).
    public sealed class HudLoadoutFormatterTests
    {
        private static GameData LoadShippedData()
        {
            (GameData data, DataValidationReport report) = GameDataLoader.LoadAll(Application.streamingAssetsPath);
            Assert.IsTrue(report.IsPlayable, "the shipped data is playable");
            return data;
        }

        private static WeaponInstance Weapon(GameData data, string id, int level, int slot)
        {
            Assert.IsTrue(data.TryGetWeapon(id, out WeaponDefinition definition), id);
            return WeaponFactory.Create(definition, level, slot, 0, 0f);
        }

        private static PassiveInstance Passive(GameData data, string id, int level)
        {
            Assert.IsTrue(data.TryGetPassive(id, out PassiveDefinition definition), id);
            return new PassiveInstance(definition, level);
        }

        [Test]
        public void WeaponLine_LevelOrEvolved()
        {
            Assert.AreEqual("투사체 Lv 3", HudLoadoutFormatter.WeaponLine("투사체", 3, false));
            Assert.AreEqual("강화 투사체 (진화)", HudLoadoutFormatter.WeaponLine("강화 투사체", 1, true));
        }

        [Test]
        public void PassiveLine_HasLevel()
        {
            Assert.AreEqual("공격 속도 Lv 2", HudLoadoutFormatter.PassiveLine("공격 속도", 2));
        }

        [TestCase(1f, "몬스터 체력 x1.0")]
        [TestCase(1.5f, "몬스터 체력 x1.5")]
        [TestCase(2.25f, "몬스터 체력 x2.3")]
        [TestCase(3f, "몬스터 체력 x3.0")]
        public void HpMultiplierLine_OneDecimal(float value, string expected)
        {
            Assert.AreEqual(expected, HudLoadoutFormatter.HpMultiplierLine(value));
        }

        [Test]
        public void HpMultiplierLine_UsesADotUnderACommaCulture()
        {
            WeaponInstance w1 = Weapon(LoadShippedData(), "W1", 1, 0);
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("몬스터 체력 x1.5", HudLoadoutFormatter.HpMultiplierLine(1.5f));
                Assert.AreEqual(w1.Definition.DisplayName + " Lv 1\n몬스터 체력 x2.2",
                    HudLoadoutFormatter.Format(new[] { w1 }, new PassiveInstance[0], 2.2f));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Test]
        public void Format_WeaponsThenPassivesThenMultiplier_SkippingEmptySlots()
        {
            GameData data = LoadShippedData();
            WeaponInstance w1 = Weapon(data, "W1", 2, 0);
            WeaponInstance e2 = Weapon(data, "E2", 1, 2);
            PassiveInstance p3 = Passive(data, "P3", 3);
            string text = HudLoadoutFormatter.Format(new[] { w1, null, e2, null }, new[] { null, p3, null }, 1.5f);
            string expected = w1.Definition.DisplayName + " Lv 2\n"
                + e2.Definition.DisplayName + " (진화)\n"
                + p3.Definition.DisplayName + " Lv 3\n"
                + "몬스터 체력 x1.5";
            Assert.AreEqual(expected, text);
        }

        [Test]
        public void Format_NothingOwned_OnlyTheMultiplierLine()
        {
            Assert.AreEqual("몬스터 체력 x1.0", HudLoadoutFormatter.Format(new WeaponInstance[4], new PassiveInstance[3], 1f));
            Assert.AreEqual("몬스터 체력 x1.0", HudLoadoutFormatter.Format(new WeaponInstance[0], new PassiveInstance[0], 1f));
        }

        [Test]
        public void Format_NullLists_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => HudLoadoutFormatter.Format(null, new PassiveInstance[0], 1f));
            Assert.Throws<ArgumentNullException>(() => HudLoadoutFormatter.Format(new WeaponInstance[0], null, 1f));
        }
    }
}
