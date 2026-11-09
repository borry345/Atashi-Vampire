using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Survivor.Tests.EditMode.Architecture
{
    /// Reads every .asmdef under Assets/Scripts, Assets/Tests and Assets/Editor and compares its name references with the 5c.1
    /// reference direction table (rows of later waves included). A reference outside the row is forbidden; the six runtime
    /// assemblies must also reference exactly their row's project assemblies.
    public sealed class AsmdefReferenceTests
    {
        private const string Core = "Survivor.Core";
        private const string Data = "Survivor.Data";
        private const string Simulation = "Survivor.Simulation";
        private const string Strategies = "Survivor.Strategies";
        private const string UI = "Survivor.UI";
        private const string App = "Survivor.App";
        private const string Dev = "Survivor.Dev";
        private const string InputSystem = "Unity.InputSystem";
        private const string TextMeshPro = "Unity.TextMeshPro";
        private const string UnityUi = "UnityEngine.UI";
        // 5c.1 names Unity.RenderPipelines.Universal.Runtime for Light2D; in URP 17 Light2D lives in the 2D runtime assembly.
        private const string Urp = "Unity.RenderPipelines.Universal.Runtime";
        private const string Urp2D = "Unity.RenderPipelines.Universal.2D.Runtime";
        private const string RenderCore = "Unity.RenderPipelines.Core.Runtime";
        private const string TestRunner = "UnityEngine.TestRunner";
        private const string EditorTestRunner = "UnityEditor.TestRunner";

        // 5c.1 table: assembly -> (project assemblies it may reference, external packages it may reference).
        private static readonly Dictionary<string, (string[] project, string[] external)> Table =
            new Dictionary<string, (string[] project, string[] external)>
            {
                { Core, (new string[0], new string[0]) },
                { Data, (new[] { Core }, new string[0]) },
                { Simulation, (new[] { Core, Data }, new[] { InputSystem }) },
                { Strategies, (new[] { Core }, new string[0]) },
                { UI, (new[] { Core, Data, Simulation }, new[] { UnityUi, TextMeshPro, InputSystem }) },
                { App, (new[] { Core, Data, Simulation, Strategies, UI }, new[] { UnityUi, InputSystem, Urp, Urp2D }) },
                { Dev, (new[] { Core, Data, Simulation, Strategies, App }, new[] { InputSystem, RenderCore }) },
                { "Survivor.Editor", (new[] { Core, Data }, new string[0]) },
                { "Survivor.Tests.EditMode", (new[] { Core, Data, Simulation, Strategies, UI, App }, new[] { TestRunner, EditorTestRunner }) },
                { "Survivor.Tests.PlayMode", (new[] { Core, Data, Simulation, Strategies, UI, App }, new[] { TestRunner, EditorTestRunner }) },
                { "Survivor.Tests.Dev", (new[] { Core, Data, Simulation, Strategies, UI, App, Dev }, new[] { TestRunner, EditorTestRunner }) },
            };

        private static readonly string[] RuntimeAssemblies = { Core, Data, Simulation, Strategies, UI, App };

        [Serializable]
        private sealed class AsmdefJson
        {
            public string name;
            public string[] references;
        }

        private static List<(string path, AsmdefJson asmdef)> LoadAll()
        {
            var result = new List<(string path, AsmdefJson asmdef)>();
            foreach (string folder in new[] { "Scripts", "Tests", "Editor" })
            {
                string root = Path.Combine(Application.dataPath, folder);
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string path in Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories))
                {
                    AsmdefJson asmdef = JsonUtility.FromJson<AsmdefJson>(File.ReadAllText(path));
                    Assert.IsFalse(string.IsNullOrEmpty(asmdef.name), path + " has no name");
                    result.Add((path, asmdef));
                }
            }

            return result;
        }

        [Test]
        public void EveryAsmdef_IsInTheTable_AndReferencesOnlyItsRow()
        {
            List<(string path, AsmdefJson asmdef)> all = LoadAll();
            Assert.GreaterOrEqual(all.Count, 8, "the six runtime assemblies and two test assemblies");
            foreach ((string path, AsmdefJson asmdef) in all)
            {
                Assert.IsTrue(Table.ContainsKey(asmdef.name), "assembly " + asmdef.name + " (" + path + ") is not in the 5c.1 table");
                (string[] project, string[] external) row = Table[asmdef.name];
                foreach (string reference in asmdef.references ?? new string[0])
                {
                    StringAssert.DoesNotStartWith("GUID:", reference, asmdef.name + " uses a GUID reference; use the assembly name");
                    bool allowed = row.project.Contains(reference) || row.external.Contains(reference);
                    Assert.IsTrue(allowed, asmdef.name + " -> " + reference + " is not allowed by 5c.1");
                }
            }
        }

        [Test]
        public void RuntimeAssemblies_ReferenceExactlyTheirProjectRow()
        {
            List<(string path, AsmdefJson asmdef)> all = LoadAll();
            foreach (string name in RuntimeAssemblies)
            {
                List<(string path, AsmdefJson asmdef)> matches = all.Where(a => a.asmdef.name == name).ToList();
                Assert.AreEqual(1, matches.Count, name + " asmdef count");
                string[] project = (matches[0].asmdef.references ?? new string[0]).Where(r => r.StartsWith("Survivor.")).OrderBy(r => r).ToArray();
                CollectionAssert.AreEqual(Table[name].project.OrderBy(r => r).ToArray(), project, name + " project references");
            }
        }

        [Test]
        public void UiNeverReferencesApp_AndNothingButTestsAndDevReferencesApp()
        {
            foreach ((string path, AsmdefJson asmdef) in LoadAll())
            {
                bool referencesApp = (asmdef.references ?? new string[0]).Contains(App);
                bool mayReferenceApp = asmdef.name == Dev || asmdef.name.StartsWith("Survivor.Tests.");
                Assert.IsTrue(!referencesApp || mayReferenceApp, asmdef.name + " must not reference " + App + " (5c.1, 5.17)");
            }
        }
    }
}
