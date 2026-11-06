using System.Text.RegularExpressions;
using Survivor.Core;
using Survivor.Strategies.Pool;
using UnityEngine;
using UnityEngine.TestTools;

namespace Survivor.Tests.PlayMode.Pool
{
    // Test fixtures for pool tests: decorated inactive templates and scene cleanup.
    internal static class PoolTestObjects
    {
        internal const string NamePrefix = "PoolTest";

        // Inactive template with a SpriteRenderer, decorated the way the bootstrap decorates entity templates.
        internal static GameObject CreateTemplate(string suffix)
        {
            GameObject template = CreateUndecoratedTemplate(suffix);
            new PoolTemplateDecorator().Decorate(template, EntityCategory.Monster);
            return template;
        }

        internal static GameObject CreateUndecoratedTemplate(string suffix)
        {
            var template = new GameObject(NamePrefix + suffix);
            template.SetActive(false);
            template.AddComponent<SpriteRenderer>();
            return template;
        }

        // Live clones of template in the scene, active or not (objects pending Destroy still count).
        internal static int CountClones(GameObject template)
        {
            string cloneName = template.name + "(Clone)";
            int count = 0;
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gameObject.name == cloneName)
                {
                    count++;
                }
            }

            return count;
        }

        internal static void DestroyAll()
        {
            foreach (GameObject gameObject in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gameObject != null && gameObject.name.StartsWith(NamePrefix))
                {
                    Object.DestroyImmediate(gameObject);
                }
            }
        }

        // Expects one pool error log line in the shared format whose message matches messagePattern.
        internal static void ExpectPoolError(string messagePattern)
        {
            LogAssert.Expect(LogType.Error, new Regex(@"^\[\d{2}:\d{2}:\d{2}\.\d{3}\] \[ERROR\] \[Pool\] " + messagePattern));
        }
    }
}
