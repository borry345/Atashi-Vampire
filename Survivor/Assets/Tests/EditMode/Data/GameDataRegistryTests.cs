using System;
using NUnit.Framework;
using Survivor.Data;

namespace Survivor.Tests.EditMode.Data
{
    /// Boot initialization and hot reload swap (TAC-F11-5): unplayable reloads keep the existing data.
    public sealed class GameDataRegistryTests
    {
        [Test]
        public void Initialize_Playable_SetsCurrent()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            var registry = new GameDataRegistry();
            Assert.IsNull(registry.Current);
            registry.Initialize(data, report);
            Assert.AreSame(data, registry.Current);
            Assert.AreSame(report, registry.LastReport);
        }

        [Test]
        public void Initialize_Unplayable_KeepsCurrentNull_ButStoresReport()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.PlayerStats, null));
            var registry = new GameDataRegistry();
            registry.Initialize(data, report);
            Assert.IsNull(registry.Current);
            Assert.AreSame(report, registry.LastReport);
        }

        [Test]
        public void Initialize_Twice_Throws()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            var registry = new GameDataRegistry();
            registry.Initialize(data, report);
            Assert.Throws<InvalidOperationException>(() => registry.Initialize(data, report));
        }

        [Test]
        public void TrySwap_BeforeInitialize_Throws()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            Assert.Throws<InvalidOperationException>(() => new GameDataRegistry().TrySwap(data, report));
        }

        [Test]
        public void TrySwap_Unplayable_KeepsExistingData()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            var registry = new GameDataRegistry();
            registry.Initialize(data, report);
            int swaps = 0;
            registry.Swapped += _ => swaps++;

            var (broken, brokenReport) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.Weapons, "{ broken"));
            Assert.IsFalse(registry.TrySwap(broken, brokenReport));
            Assert.AreSame(data, registry.Current, "existing data kept");
            Assert.AreSame(brokenReport, registry.LastReport, "errors stay visible");
            Assert.AreEqual(0, swaps);
        }

        [Test]
        public void TrySwap_Playable_ReplacesAndRaises_OldReferencesStayValid()
        {
            var (data, report) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            var registry = new GameDataRegistry();
            registry.Initialize(data, report);
            Assert.IsTrue(data.TryGetWeapon("W1", out WeaponDefinition oldW1));

            GameData received = null;
            registry.Swapped += d => received = d;
            var (next, nextReport) = DataTestFixtures.Load(DataTestFixtures.With(DataFileKind.Weapons, "\"damage\": 10, \"cooldownSeconds\": 1.0", "\"damage\": 99, \"cooldownSeconds\": 1.0"));
            Assert.IsTrue(registry.TrySwap(next, nextReport));
            Assert.AreSame(next, registry.Current);
            Assert.AreSame(next, received);
            Assert.IsTrue(registry.Current.TryGetWeapon("W1", out WeaponDefinition newW1));
            Assert.AreEqual(99f, newW1.Levels[0].Damage);
            Assert.AreEqual(10f, oldW1.Levels[0].Damage, "instances holding the old definition keep it (5.11.1)");
        }

        [Test]
        public void TrySwap_RecoversFromDataError()
        {
            var (bad, badReport) = DataTestFixtures.Load(DataTestFixtures.WithText(DataFileKind.PlayerStats, null));
            var registry = new GameDataRegistry();
            registry.Initialize(bad, badReport);
            var (good, goodReport) = DataTestFixtures.Load(DataTestFixtures.ValidTexts());
            Assert.IsTrue(registry.TrySwap(good, goodReport));
            Assert.AreSame(good, registry.Current);
        }
    }
}
