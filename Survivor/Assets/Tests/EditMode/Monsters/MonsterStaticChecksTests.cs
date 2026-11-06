using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Survivor.Core;
using Survivor.Simulation;
using UnityEngine;

namespace Survivor.Tests.EditMode.Monsters
{
    /// Source-level checks on Scripts/Simulation/Monsters and Pickups (TAC-F3-11, TAC-0-2, 5c.3 #13) and the tick contract (TAC-F17-1/2).
    public sealed class MonsterStaticChecksTests
    {
        private static string[] SourcesOf(string folder)
        {
            string path = Path.Combine(Application.dataPath, "Scripts", "Simulation", folder);
            Assert.IsTrue(Directory.Exists(path), path);
            string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(files);
            return files;
        }

        [Test]
        public void MonstersFolder_NeverReferencesTheTargetProvider()
        {
            foreach (string file in SourcesOf("Monsters"))
            {
                string text = File.ReadAllText(file);
                StringAssert.DoesNotContain("ITargetProvider", text, "TAC-F3-11: " + Path.GetFileName(file));
                StringAssert.DoesNotContain(".Targets", text, "TAC-F3-11: " + Path.GetFileName(file));
            }
        }

        [Test]
        public void MonstersAndPickups_UseNoWallClockOrUnityRandom()
        {
            var banned = new Regex(@"\bTime\.|UnityEngine\.Random|System\.Random|\bRandom\.|DateTime");
            foreach (string folder in new[] { "Monsters", "Pickups" })
            {
                foreach (string file in SourcesOf(folder))
                {
                    Assert.IsFalse(banned.IsMatch(File.ReadAllText(file)), "TAC-0-2: " + Path.GetFileName(file));
                }
            }
        }

        [Test]
        public void MonstersAndPickups_DoNotReferencePlayerSideTypes()
        {
            var banned = new Regex(@"\b(ExperienceSystem|ChestEffectResolver|ChestEffect|PlayerEntity|PlayerDamageReceiver|WeaponInventory)\b");
            foreach (string folder in new[] { "Monsters", "Pickups" })
            {
                foreach (string file in SourcesOf(folder))
                {
                    Assert.IsFalse(banned.IsMatch(File.ReadAllText(file)), "5c.3 #13: " + Path.GetFileName(file));
                }
            }
        }

        [Test]
        public void OnlyMonstersAndEnemyProjectiles_AreUpdatable()
        {
            Assert.IsTrue(typeof(IUpdatable).IsAssignableFrom(typeof(Monster)), "TAC-F17-1/2");
            Assert.IsTrue(typeof(IUpdatable).IsAssignableFrom(typeof(EnemyProjectile)));
            Assert.IsFalse(typeof(IUpdatable).IsAssignableFrom(typeof(EnemyHazardZone)));
            Assert.IsFalse(typeof(IUpdatable).IsAssignableFrom(typeof(ExperienceGem)));
            Assert.IsFalse(typeof(IUpdatable).IsAssignableFrom(typeof(Chest)));
            Assert.IsTrue(typeof(IDamageable).IsAssignableFrom(typeof(Monster)));
            Assert.IsTrue(typeof(IContactDamageSource).IsAssignableFrom(typeof(Monster)));
            Assert.IsTrue(typeof(IContactDamageSource).IsAssignableFrom(typeof(EnemyProjectile)));
            Assert.IsTrue(typeof(IContactDamageSource).IsAssignableFrom(typeof(EnemyHazardZone)));
        }
    }
}
