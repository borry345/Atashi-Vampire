using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Survivor.App;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Flow
{
    /// Entity visuals (wave 4d): every live entity carries a child "Visual" with a sprite, the registry tint of its asset key,
    /// its category's sorting order and a scale of twice the body radius, while the host keeps scale 1 (and, under engine
    /// collision, a collider radius equal to the body radius). Without a registry every key falls back to magenta. Batch play
    /// mode has no graphics, so component state stands in for "visible".
    public sealed class FlowVisualTests : FlowPmFixture
    {
        private const ulong Seed = 77UL;
        private const long CheckStep = 240;

        // Crowd of chasers, chargers and ranged monsters, all four base weapons, then the run is paused for a stable check.
        private IEnumerator StartCrowdedRun(StrategySelection selection)
        {
            yield return LoadMain();
            UseScript(Seed, new FlowPmInput { Still = true }, new FlowPmCrowdPolicy(30, 3, "M1", "M2", "M3"));
            Host.NextRunSelection = selection;
            Host.StartNewRun();
            Host.Commands.Enqueue(PendingCommand.SetLoadout(new[]
            {
                new WeaponLoadoutEntry("W1", 1), new WeaponLoadoutEntry("W2", 1), new WeaponLoadoutEntry("W3", 1), new WeaponLoadoutEntry("W4", 1),
            }));
            yield return WaitFor(() => Clock.Step >= CheckStep || Flow.State != FlowState.Playing, "the crowded run reaches step " + CheckStep);
            Assert.AreNotEqual(FlowState.Result, Flow.State, "the run is still alive");
            yield return PauseAndSettle();
        }

        // No steps run while paused, so one more frame lets LateUpdate settle every visual against the final radii.
        private IEnumerator PauseAndSettle()
        {
            if (Flow.State == FlowState.Playing)
            {
                Flow.TogglePause();
            }

            Assert.IsFalse(Clock.IsRunning, "no step runs during the check");
            yield return Frames(2);
        }

        private Dictionary<EntityCategory, int> AssertVisuals(bool expectRegistry, bool engineCollision)
        {
            var seen = new Dictionary<EntityCategory, int>();
            foreach (SimEntity entity in Host.Lifecycle.ActiveEntities)
            {
                string what = entity.Category + " " + entity.Id;
                Transform visual = EntityPresenter.FindVisual(entity.transform);
                Assert.IsNotNull(visual, what + " has a Visual child");
                var spriteRenderer = visual.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(spriteRenderer, what + " Visual has a SpriteRenderer");
                Assert.IsNotNull(spriteRenderer.sprite, what + " sprite");
                Assert.AreEqual(Host.Presenter.ResolvedColorOf(entity), spriteRenderer.color, what + " tint = resolved registry color");
                if (expectRegistry)
                {
                    Assert.AreNotEqual(Color.magenta, spriteRenderer.color, what + " key is registered (not the fallback)");
                }
                else
                {
                    Assert.AreEqual(Color.magenta, spriteRenderer.color, what + " falls back to magenta without a registry");
                }

                Assert.AreEqual(EntityPresenter.SortingOrderOf(entity.Category), spriteRenderer.sortingOrder, what + " sorting order");
                float diameter = entity.Body.Radius * 2f;
                Assert.AreEqual(new Vector3(diameter, diameter, 1f), visual.localScale, what + " visual scale = 2r");
                Assert.AreEqual(Vector3.zero, visual.localPosition, what + " visual sits on the host");
                Assert.AreEqual(Vector3.one, entity.transform.localScale, what + " host scale stays 1");
                if (engineCollision)
                {
                    var circle = entity.GetComponent<CircleCollider2D>();
                    Assert.IsNotNull(circle, what + " collider");
                    Assert.AreEqual(entity.Body.Radius, circle.radius, what + " collider radius = body radius");
                }

                seen.TryGetValue(entity.Category, out int count);
                seen[entity.Category] = count + 1;
            }

            Assert.IsTrue(seen.ContainsKey(EntityCategory.Monster), "at least one monster spawned");
            foreach (KeyValuePair<EntityCategory, int> pair in seen)
            {
                TestContext.Out.WriteLine(pair.Key + " x" + pair.Value);
            }

            return seen;
        }

        [UnityTest]
        public IEnumerator Visuals_UseRegistryColors_AndBodyDiameter_SelfCollision()
        {
            yield return StartCrowdedRun(StrategySelection.ShippingDefault);
            Assert.AreEqual(CollisionStrategyKind.SelfSquaredDistance, Host.Lifecycle.Collisions.Kind);
            Dictionary<EntityCategory, int> seen = AssertVisuals(true, false);
            Assert.IsTrue(seen.ContainsKey(EntityCategory.PlayerOrb), "W2 orbs are drawn");
            Assert.IsTrue(seen.ContainsKey(EntityCategory.PlayerZone) || seen.ContainsKey(EntityCategory.PlayerProjectile),
                "player attacks are drawn");
        }

        [UnityTest]
        public IEnumerator Visuals_HoldUnderEngineCollision_HostScaleAndColliderUntouched()
        {
            yield return StartCrowdedRun(StrategySelection.Baseline);
            Assert.AreEqual(CollisionStrategyKind.EngineCollider2D, Host.Lifecycle.Collisions.Kind);
            AssertVisuals(true, true);
        }

        [UnityTest]
        public IEnumerator Visuals_WithoutRegistry_FallBackToMagenta()
        {
            yield return StartCrowdedRun(StrategySelection.ShippingDefault);
            Host.Presenter.UseResolverWithoutRegistry();
            AssertVisuals(false, false);

            // Entities spawned after the swap use the fallback as well.
            Flow.TogglePause();
            long resumedAt = Clock.Step;
            yield return WaitFor(() => Clock.Step >= resumedAt + 30 || Flow.State != FlowState.Playing, "the run resumes");
            yield return PauseAndSettle();
            AssertVisuals(false, false);
        }
    }
}
