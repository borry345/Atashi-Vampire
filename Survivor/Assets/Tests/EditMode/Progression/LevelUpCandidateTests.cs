using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Data;
using Survivor.Simulation;
using Survivor.Tests.EditMode.Monsters;

namespace Survivor.Tests.EditMode.Progression
{
    /// LevelUpCandidateBuilder (TAC-F5-1..F5-3, R8, R12, R22, TAC-F11-5) and LevelUpSelector (TAC-F5-1, TAC-F5-2, R9).
    public sealed class LevelUpCandidateTests
    {
        private FlowEmRig _rig;
        private readonly List<(UpgradeOption option, float weight)> _candidates = new List<(UpgradeOption option, float weight)>();

        [SetUp]
        public void SetUp()
        {
            _rig = new FlowEmRig(start: false);
            _rig.PolicyFactory = () => new MonEmNoSpawnPolicy();
            _rig.StartRun();
        }

        [TearDown]
        public void TearDown()
        {
            _rig.Dispose();
        }

        private void Build(GameData data = null)
        {
            LevelUpCandidateBuilder.Build(data ?? _rig.Data.Current, _rig.Weapons, _rig.Passives, _rig.Health.Hp, _rig.Health.MaxHp, _candidates);
        }

        private string Describe()
        {
            return string.Join(" ", _candidates.Select(c => c.option.Kind + ":" + c.option.TargetId + "=" + c.weight));
        }

        private void SetLoadout(params (string id, int level)[] entries)
        {
            var loadout = new List<WeaponLoadoutEntry>();
            foreach ((string id, int level) in entries)
            {
                loadout.Add(new WeaponLoadoutEntry(id, level));
            }

            _rig.Enqueue(PendingCommand.SetLoadout(loadout));
            _rig.Step();
        }

        // Closes the gate for a level-up so ApplyLevelUpOption is legal (the gate's own path, LevelUpNow).
        private void CloseForLevelUp()
        {
            _rig.Enqueue(PendingCommand.LevelUpNow());
            _rig.Step();
            Assert.AreEqual(GateCloseReason.LevelUp, _rig.Clock.CloseReason);
            Assert.IsFalse(_rig.Clock.IsRunning);
        }

        [Test]
        public void RunStart_CanonicalOrder_PoolWeights_NoHealAtFullHealth()
        {
            Build();
            Assert.AreEqual(
                "WeaponUpgrade:W1=12 WeaponNew:W2=8 WeaponNew:W3=8 WeaponNew:W4=8 PassiveNew:P1=10 PassiveNew:P2=10 PassiveNew:P3=10",
                Describe(), "R12 order, rule W1 upgrade 12, weaponNew * 8, default 10; E1/E2 evolved-only; heal weight 0 at full HP");
            Assert.IsTrue(_candidates.All(c => !c.option.IsFiller));
        }

        [Test]
        public void Heal_UsesR8Weight_WhenDamaged()
        {
            _rig.Enqueue(PendingCommand.SetHealth(50f));
            _rig.Step();
            Build();
            (UpgradeOption option, float weight) last = _candidates[_candidates.Count - 1];
            Assert.AreEqual(UpgradeKind.Heal, last.option.Kind);
            Assert.AreEqual(LevelUpCandidateBuilder.HealTargetId, last.option.TargetId);
            Assert.AreEqual("", last.option.TargetId);
            Assert.AreEqual(HealWeight.Compute(30f, 50f, 100f), last.weight);
            Assert.AreEqual(7.5f, last.weight, 1e-6f);
        }

        [Test]
        public void OwnedItems_OnlyAppearAsUpgrades()
        {
            SetLoadout(("W1", 1), ("W2", 2));
            CloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            Build();
            Assert.AreEqual(
                "WeaponUpgrade:W1=12 WeaponUpgrade:W2=10 WeaponNew:W3=8 WeaponNew:W4=8 PassiveUpgrade:P1=10 PassiveNew:P2=10 PassiveNew:P3=10",
                Describe(), "no New candidate for an owned weapon/passive (TAC-F5-3, 7th revision)");
        }

        [Test]
        public void FullSlots_NoNewCandidates_AndMaxLevel_NoUpgrade()
        {
            SetLoadout(("W1", 5), ("W2", 1), ("W3", 1), ("W4", 4));
            CloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P2"));
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P3"));
            for (int i = 0; i < 4; i++)
            {
                _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveUpgrade, "P3"));
            }

            Build();
            Assert.AreEqual(
                "WeaponUpgrade:W2=10 WeaponUpgrade:W3=10 WeaponUpgrade:W4=10 PassiveUpgrade:P1=10 PassiveUpgrade:P2=10",
                Describe(), "4 weapons / 3 passives: no New; W1 at 5/5 and P3 at 5/5 are not upgradable (R22)");
        }

        [Test]
        public void EvolvedOnlyWeapon_NeverOffered_AndConsumedOriginal_NotOfferedAsNew()
        {
            SetLoadout(("W1", 5), ("W2", 1));
            CloseForLevelUp();
            _rig.Applier.ApplyLevelUpOption(new UpgradeOption(UpgradeKind.PassiveNew, "P1"));
            _rig.Clock.Open();
            CombinationRule rule = _rig.Data.Current.Combinations.First(r => r.WeaponId == "W1");
            _rig.ResolveHook.Pending.Add(context => _rig.Applier.ApplyEvolution(0, rule, context));
            _rig.Step();
            Assert.AreEqual("E1", _rig.Weapons.Slots[0].Id);
            CollectionAssert.AreEqual(new[] { "W1" }, _rig.Weapons.ConsumedByEvolution);

            Build();
            Assert.AreEqual(
                "WeaponUpgrade:W2=10 WeaponNew:W3=8 WeaponNew:W4=8 PassiveUpgrade:P1=10 PassiveNew:P2=10 PassiveNew:P3=10",
                Describe(), "E1 owned: no upgrade (evolved-only); W1 consumed: no WeaponNew W1 (D5); E2 never offered");
        }

        [Test]
        public void HotReload_UpgradeReadsTheInstanceDefinition_NewReadsCurrent()
        {
            SetLoadout(("W1", 1), ("W2", 2));
            (GameData renamed, DataValidationReport report) = MonEmData.Load(
                (DataFileKind.Weapons, "\"id\": \"W2\"", "\"id\": \"W9\""),
                (DataFileKind.Combinations, "\"weaponId\": \"W2\"", "\"weaponId\": \"W9\""));
            Assert.IsTrue(_rig.Data.TrySwap(renamed, report));
            Assert.IsFalse(renamed.TryGetWeapon("W2", out _));

            Build();
            Assert.AreEqual(
                "WeaponUpgrade:W1=12 WeaponUpgrade:W2=10 WeaponNew:W3=8 WeaponNew:W4=8 WeaponNew:W9=8 PassiveNew:P1=10 PassiveNew:P2=10 PassiveNew:P3=10",
                Describe(), "the W2 instance keeps its captured definition (TAC-F11-5); new candidates come from Current");
        }

        [Test]
        public void Build_ClearsOutput_AndRejectsNulls()
        {
            _candidates.Add((new UpgradeOption(UpgradeKind.Heal, "x"), 99f));
            Build();
            Assert.IsFalse(_candidates.Any(c => c.option.TargetId == "x"));
            GameData data = _rig.Data.Current;
            Assert.Throws<ArgumentNullException>(() => LevelUpCandidateBuilder.Build(null, _rig.Weapons, _rig.Passives, 1f, 1f, _candidates));
            Assert.Throws<ArgumentNullException>(() => LevelUpCandidateBuilder.Build(data, null, _rig.Passives, 1f, 1f, _candidates));
            Assert.Throws<ArgumentNullException>(() => LevelUpCandidateBuilder.Build(data, _rig.Weapons, null, 1f, 1f, _candidates));
            Assert.Throws<ArgumentNullException>(() => LevelUpCandidateBuilder.Build(data, _rig.Weapons, _rig.Passives, 1f, 1f, null));
        }

        [Test]
        public void HealTargetId_IsSafeForTheCanonicalComparer()
        {
            var heal = new UpgradeOption(UpgradeKind.Heal, "");
            Assert.AreEqual(0, UpgradeOrdering.Compare(heal, new UpgradeOption(UpgradeKind.Heal, "")));
            Assert.Less(UpgradeOrdering.Compare(new UpgradeOption(UpgradeKind.PassiveNew, "P3"), heal), 0);
            Assert.DoesNotThrow(() => UpgradeOrdering.Compare(new UpgradeOption(UpgradeKind.Heal, null), heal), "null is also tolerated");
        }

        [Test]
        public void Draw_ThreeDistinct_InWeightedSamplerOrder()
        {
            Build();
            var rngs = new RngStreams(777UL);
            var mirror = new RngStreams(777UL);
            var output = new List<UpgradeOption>();
            LevelUpSelector.Draw(_candidates, rngs.Get(RngStream.LevelUp), output);

            var indices = new List<int>();
            WeightedSampler.SampleWithoutReplacement(_candidates.Select(c => c.weight).ToList(), 3, mirror.Get(RngStream.LevelUp), indices);
            Assert.AreEqual(3, output.Count);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(_candidates[indices[i]].option.Kind, output[i].Kind);
                Assert.AreEqual(_candidates[indices[i]].option.TargetId, output[i].TargetId);
                Assert.IsFalse(output[i].IsFiller);
            }

            Assert.AreEqual(3, output.Select(o => o.Kind + o.TargetId).Distinct().Count(), "without replacement");
        }

        [Test]
        public void Draw_FewerThanThree_FillsWithHeal_EvenAtFullHealth()
        {
            var rng = new RngStreams(5UL).Get(RngStream.LevelUp);
            var output = new List<UpgradeOption> { default, default, default, default };
            var two = new List<(UpgradeOption option, float weight)>
            {
                (new UpgradeOption(UpgradeKind.WeaponUpgrade, "W1"), 1f), (new UpgradeOption(UpgradeKind.PassiveNew, "P2"), 3f),
            };
            LevelUpSelector.Draw(two, rng, output);
            Assert.AreEqual(3, output.Count, "output cleared first");
            Assert.AreEqual(2, output.Count(o => !o.IsFiller));
            Assert.AreEqual(UpgradeKind.Heal, output[2].Kind);
            Assert.IsTrue(output[2].IsFiller);
            Assert.AreEqual("", output[2].TargetId);

            LevelUpSelector.Draw(new List<(UpgradeOption option, float weight)>(), rng, output);
            Assert.AreEqual(3, output.Count);
            Assert.IsTrue(output.All(o => o.Kind == UpgradeKind.Heal && o.IsFiller), "no candidates: three heal fillers");

            var zero = new List<(UpgradeOption option, float weight)>
            {
                (new UpgradeOption(UpgradeKind.WeaponNew, "W2"), 0f), (new UpgradeOption(UpgradeKind.WeaponNew, "W3"), 2f),
            };
            for (int i = 0; i < 20; i++)
            {
                LevelUpSelector.Draw(zero, rng, output);
                Assert.IsFalse(output.Any(o => o.TargetId == "W2"), "zero weight is never drawn");
                Assert.AreEqual("W3", output[0].TargetId);
                Assert.IsTrue(output[1].IsFiller && output[2].IsFiller);
            }
        }

        [Test]
        public void Draw_ConsumesOnlyTheLevelUpStream()
        {
            Build();
            var drawn = new RngStreams(42UL);
            var untouched = new RngStreams(42UL);
            var output = new List<UpgradeOption>();
            for (int i = 0; i < 5; i++)
            {
                LevelUpSelector.Draw(_candidates, drawn.Get(RngStream.LevelUp), output);
            }

            foreach (RngStream stream in new[] { RngStream.Spawn, RngStream.Summon, RngStream.Drop, RngStream.Chest })
            {
                Assert.AreEqual(untouched.Get(stream).NextUInt(), drawn.Get(stream).NextUInt(), stream + " is not consumed");
            }

            Assert.AreNotEqual(untouched.Get(RngStream.LevelUp).NextUInt(), drawn.Get(RngStream.LevelUp).NextUInt(), "sanity: LevelUp advanced");
        }

        [Test]
        public void Draw_RejectsNulls()
        {
            var rng = new RngStreams(1UL).Get(RngStream.LevelUp);
            var output = new List<UpgradeOption>();
            Assert.Throws<ArgumentNullException>(() => LevelUpSelector.Draw(null, rng, output));
            Assert.Throws<ArgumentNullException>(() => LevelUpSelector.Draw(_candidates, null, output));
            Assert.Throws<ArgumentNullException>(() => LevelUpSelector.Draw(_candidates, rng, null));
        }
    }
}
