using System.Collections.Generic;
using NUnit.Framework;
using Survivor.Core;

namespace Survivor.Tests.EditMode.Core
{
    public sealed class R12_UpgradeOrderingTests
    {
        [Test]
        public void EnumValueOrder_IsDocumented()
        {
            Assert.AreEqual(0, (int)UpgradeKind.WeaponUpgrade);
            Assert.AreEqual(1, (int)UpgradeKind.WeaponNew);
            Assert.AreEqual(2, (int)UpgradeKind.PassiveUpgrade);
            Assert.AreEqual(3, (int)UpgradeKind.PassiveNew);
            Assert.AreEqual(4, (int)UpgradeKind.Heal);
        }

        [Test]
        public void Sort_ByKindThenOrdinalTargetId()
        {
            var options = new List<UpgradeOption>
            {
                new UpgradeOption(UpgradeKind.Heal, "heal"),
                new UpgradeOption(UpgradeKind.PassiveNew, "P2"),
                new UpgradeOption(UpgradeKind.WeaponNew, "W3"),
                new UpgradeOption(UpgradeKind.WeaponUpgrade, "W2"),
                new UpgradeOption(UpgradeKind.WeaponUpgrade, "W10"),
                new UpgradeOption(UpgradeKind.PassiveUpgrade, "a1"),
                new UpgradeOption(UpgradeKind.PassiveUpgrade, "Z1"),
            };
            options.Sort(UpgradeOrdering.Compare);

            string[] expected = { "W10", "W2", "W3", "Z1", "a1", "P2", "heal" };
            UpgradeKind[] expectedKinds =
            {
                UpgradeKind.WeaponUpgrade, UpgradeKind.WeaponUpgrade, UpgradeKind.WeaponNew,
                UpgradeKind.PassiveUpgrade, UpgradeKind.PassiveUpgrade, UpgradeKind.PassiveNew, UpgradeKind.Heal,
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], options[i].TargetId, $"index {i}");
                Assert.AreEqual(expectedKinds[i], options[i].Kind, $"index {i}");
            }
        }

        [Test]
        public void Compare_IsZeroForSameKindAndId()
        {
            Assert.AreEqual(0, UpgradeOrdering.Compare(
                new UpgradeOption(UpgradeKind.WeaponNew, "W1"), new UpgradeOption(UpgradeKind.WeaponNew, "W1", true)));
        }
    }
}
