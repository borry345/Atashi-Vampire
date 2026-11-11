using System.Collections;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using Survivor.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Flow
{
    /// Wave 6b display (5.17.1, 5.17.2): the HUD loadout panel (TAC-F8-15) follows level-ups, dev loadouts and spawn segment
    /// changes on the next frame and sits at the bottom left clear of the four top-left lines; the orbit rings (TAC-F6-10) show
    /// one ring per Orbit weapon slot at its current orbitRadius, also while attacks are off, and vanish with the weapon.
    public sealed class FlowHudOrbitTests : FlowPmFixture
    {
        private const ulong Seed = 20261111UL;

        private IEnumerator StartQuietRun()
        {
            yield return LoadMain();
            UseScript(Seed, new FlowPmInput { Still = true }, new FlowPmNoSpawnPolicy());
            Flow.Title.RequestStart();
            Assert.AreEqual(FlowState.Playing, Flow.State);
        }

        // Dev SetLoadout runs at the next step's Begin; waits for it, then one more frame for the presentation.
        private IEnumerator ApplyLoadout(params WeaponLoadoutEntry[] loadout)
        {
            int before = Host.Applier.ChangeCount;
            Host.Commands.Enqueue(PendingCommand.SetLoadout(loadout));
            yield return WaitFor(() => Host.Applier.ChangeCount != before, "the loadout is applied");
            yield return null;
        }

        private IEnumerator JumpTo(long targetStep)
        {
            Clock.RequestJump(targetStep);
            yield return WaitFor(() => Clock.Step > targetStep, "the jump to step " + targetStep);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HudLoadout_FirstFrame_LevelUp_DevLoadout_SpawnSegments()
        {
            yield return StartQuietRun();
            WeaponInstance start = Host.Session.Weapons.Slots[0];
            Assert.IsNotNull(start, "start weapon");

            // First frame of the run: start weapon at Lv 1 and the first segment's multiplier (StartRun calls RefreshHud).
            // This assembly does not reference Survivor.Data, so the display name is read back from the panel's first line.
            string[] lines = Flow.Hud.LoadoutText.Split('\n');
            Assert.AreEqual(2, lines.Length, Flow.Hud.LoadoutText);
            StringAssert.EndsWith(" Lv 1", lines[0], "start weapon at Lv 1");
            Assert.AreEqual("몬스터 체력 x1.0", lines[1]);
            Assert.AreEqual(HudLoadoutFormatter.Format(Host.Session.Weapons.Slots, Host.Session.Passives.Slots, 1f), Flow.Hud.LoadoutText);
            string startName = lines[0].Substring(0, lines[0].Length - " Lv 1".Length);

            // Level-ups one at a time (dev LevelUpNow): the first round that offers the start weapon's upgrade takes it, earlier
            // rounds take option 0 (the draw is random, so how many rounds that needs depends on the seed).
            const int MaxRounds = 12;
            bool upgraded = false;
            for (int roundIndex = 0; roundIndex < MaxRounds && !upgraded; roundIndex++)
            {
                Host.Commands.Enqueue(PendingCommand.LevelUpNow());
                yield return WaitFor(() => Flow.State == FlowState.LevelUp, "level-up round " + (roundIndex + 1));
                yield return null;
                LevelUpSession round = Host.Session.LevelUpSession;
                int choice = 0;
                for (int i = 0; i < round.Options.Count; i++)
                {
                    if (round.Options[i].Kind == UpgradeKind.WeaponUpgrade && round.Options[i].TargetId == start.Id)
                    {
                        choice = i;
                        upgraded = true;
                    }
                }

                StringAssert.StartsWith(startName + " Lv 1\n", Flow.Hud.LoadoutText, "not upgraded yet");
                Flow.LevelUp.Choose(choice);
                Assert.AreEqual(FlowState.Playing, Flow.State, "one level per round");
                yield return null;
            }

            Assert.IsTrue(upgraded, "a round within " + MaxRounds + " offered the start weapon's upgrade (seed " + Seed + ")");
            StringAssert.StartsWith(startName + " Lv 2\n", Flow.Hud.LoadoutText, "the frame after the choice shows Lv 2");

            // Dev loadout (ChangeCount path): new weapon lines on the next frame.
            yield return ApplyLoadout(new WeaponLoadoutEntry(start.Id, 3), new WeaponLoadoutEntry("W2", 1));
            StringAssert.StartsWith(startName + " Lv 3\n", Flow.Hud.LoadoutText);
            StringAssert.EndsWith(" Lv 1", Flow.Hud.LoadoutText.Split('\n')[1], "W2 line");
            Assert.AreEqual(HudLoadoutFormatter.Format(Host.Session.Weapons.Slots, Host.Session.Passives.Slots, 1f), Flow.Hud.LoadoutText,
                "the panel is exactly the formatter over the session slots");

            // Spawn segments (shipped table: 0-120 s x1.0, 120-300 s x1.5, ..., 480-600 s x3.0) through time jumps.
            yield return JumpTo(7100);
            StringAssert.EndsWith("\n몬스터 체력 x1.0", Flow.Hud.LoadoutText, "before 120 s");
            yield return JumpTo(7300);
            StringAssert.EndsWith("\n몬스터 체력 x1.5", Flow.Hud.LoadoutText, "the frame after crossing 120 s");
            yield return JumpTo(40000);
            StringAssert.EndsWith("\n몬스터 체력 x3.0", Flow.Hud.LoadoutText, "after the last segment the line keeps its value");
        }

        [UnityTest]
        public IEnumerator HudLoadout_IsBottomLeft_AndClearOfTheTopLeftLines()
        {
            yield return StartQuietRun();
            var panel = (RectTransform)Flow.Hud.transform.Find("Loadout");
            Assert.IsNotNull(panel, "Loadout text under the HUD root (shown and hidden with it)");
            Assert.AreEqual(Vector2.zero, panel.anchorMin);
            Assert.AreEqual(Vector2.zero, panel.anchorMax);
            Assert.AreEqual(Vector2.zero, panel.pivot);
            float top = panel.anchoredPosition.y + panel.sizeDelta.y;
            Assert.LessOrEqual(top, UiFactory.ReferenceHeight - 244f, "panel top stays below the four top-left lines (y 24..244)");
            Assert.Greater(panel.anchoredPosition.y, 0f, "inside the screen");
        }

        [UnityTest]
        public IEnumerator OrbitRings_FollowOrbitSlots_WhileAttacksOff_AndVanishWithTheWeapon()
        {
            yield return StartQuietRun();
            var rings = Host.PlayerHost.GetComponent<OrbitRingPresenter>();
            Assert.IsNotNull(rings, "OrbitRingPresenter on the player host");
            Assert.IsTrue(rings.HasLineMaterial, "Sprites/Default is found in the editor");
            yield return null;
            Assert.AreEqual(0, rings.VisibleRingCount, "the start weapon is not an orbit weapon");

            yield return ApplyLoadout(new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W2", 1));
            Assert.AreEqual(1, rings.VisibleRingCount);
            Assert.AreEqual(2f, rings.RingRadiusOf(1), "W2 Lv 1 orbitRadius (weapons.json)");
            foreach (Transform child in Host.PlayerHost.transform)
            {
                if (child.name.StartsWith("OrbitRing."))
                {
                    Assert.IsNull(child.GetComponent<SimEntity>(), child.name + " is not a SimEntity");
                    Assert.IsNull(child.GetComponent<Collider2D>(), child.name + " has no collider");
                }
            }

            // Attacks off (dev F19): the ring stays.
            Host.Rules.WeaponsEnabled = false;
            try
            {
                yield return Frames(3);
                Assert.AreEqual(1, rings.VisibleRingCount, "shown while WeaponsEnabled is false");

                // Upgrade in place (same instance, level 3) and evolution (slot replaced by E2): the next frame's radius.
                yield return ApplyLoadout(new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W2", 3));
                Assert.AreEqual(2.2f, rings.RingRadiusOf(1), "W2 Lv 3 orbitRadius");
                yield return ApplyLoadout(new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("E2", 1));
                Assert.AreEqual(1, rings.VisibleRingCount);
                Assert.AreEqual(3f, rings.RingRadiusOf(1), "E2 orbitRadius (weapons.json)");

                // Orbit weapon removed (a null entry leaves the slot empty): the ring goes off on the next frame.
                yield return ApplyLoadout(new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry(null, 0));
                Assert.IsNull(Host.Session.Weapons.Slots[1]);
                Assert.AreEqual(0, rings.VisibleRingCount);
                Assert.AreEqual(0f, rings.RingRadiusOf(1));
            }
            finally
            {
                Host.Rules.WeaponsEnabled = true;
            }
        }
    }
}
