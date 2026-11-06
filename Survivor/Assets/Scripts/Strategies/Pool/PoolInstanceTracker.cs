using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Survivor.Strategies.Pool
{
    // Bookkeeping shared by the three strategies: template checks, acquired set, release validation.
    // Composition instead of a base class keeps the three strategies the only public pool strategy types.
    internal sealed class PoolInstanceTracker
    {
        private readonly object _owner;
        private readonly HashSet<PooledObject> _active = new HashSet<PooledObject>();
        private long _nextSerial;

        internal PoolInstanceTracker(object owner)
        {
            _owner = owner;
        }

        internal int ActiveCount => _active.Count;

        internal static void ValidateTemplate(GameObject template)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (template.activeSelf)
            {
                throw new InvalidOperationException($"Pool template '{template.name}' must be inactive.");
            }

            if (template.GetComponent<PooledObject>() == null)
            {
                throw new InvalidOperationException(
                    $"Pool template '{template.name}' has no PooledObject; apply PoolTemplateDecorator first.");
            }
        }

        internal static void ValidatePrewarm(GameObject template, int count)
        {
            ValidateTemplate(template);
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Prewarm count must not be negative.");
            }
        }

        // Clones a validated template; the clone is inactive because the template is.
        internal static GameObject CreateInstance(GameObject template)
        {
            return UnityEngine.Object.Instantiate(template);
        }

        // Marks an inactive instance as acquired, moves it to position and activates it.
        // Position is set before activation so OnEnable already sees it. No gameplay state is touched (TAC-F9-6).
        internal GameObject MarkAcquired(GameObject template, GameObject instance, Vector2 position)
        {
            PooledObject pooled = instance.GetComponent<PooledObject>();
            pooled.Template = template;
            pooled.Owner = _owner;
            pooled.IsActive = true;
            pooled.AcquireSerial = _nextSerial++;
            _active.Add(pooled);

            Transform transform = instance.transform;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            instance.SetActive(true);
            return instance;
        }

        // Validates a release, removes the instance from the acquired set and deactivates it.
        // Returns the instance's template. Logs and throws InvalidOperationException on double or foreign release (TAC-F9-5).
        internal GameObject MarkReleased(GameObject instance)
        {
            if (ReferenceEquals(instance, null))
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (instance == null)
            {
                // Destroyed object: its name is no longer readable, its instance id still is.
                throw Fail($"Release of a destroyed instance (id {instance.GetInstanceID()}): double release.");
            }

            PooledObject pooled = instance.GetComponent<PooledObject>();
            if (pooled == null || !ReferenceEquals(pooled.Owner, _owner))
            {
                throw Fail(
                    $"Release of foreign object '{instance.name}' (id {instance.GetInstanceID()}): not acquired from this pool.");
            }

            if (!pooled.IsActive)
            {
                throw Fail($"Double release of '{instance.name}' (id {instance.GetInstanceID()}).");
            }

            _active.Remove(pooled);
            pooled.IsActive = false;
            instance.SetActive(false);
            return pooled.Template;
        }

        // Clear is only legal once every instance is back; logs and throws otherwise, before anything is destroyed.
        internal void EnsureNoneActive(string strategyName)
        {
            if (_active.Count > 0)
            {
                throw Fail($"{strategyName}.Clear called with {_active.Count} active instance(s); call ReleaseAll first.");
            }
        }

        // Acquired instances in acquire order, so ReleaseAll does not depend on HashSet order.
        internal List<GameObject> SnapshotActiveInAcquireOrder()
        {
            var pooled = new List<PooledObject>(_active);
            pooled.Sort((a, b) => a.AcquireSerial.CompareTo(b.AcquireSerial));
            var instances = new List<GameObject>(pooled.Count);
            foreach (PooledObject item in pooled)
            {
                instances.Add(item.gameObject);
            }

            return instances;
        }

        // Logs in the shared format ([HH:mm:ss.fff] [ERROR] [Pool] message) and returns the exception to throw.
        internal static InvalidOperationException Fail(string message)
        {
            string stamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
            Debug.LogError($"[{stamp}] [ERROR] [Pool] {message}");
            return new InvalidOperationException(message);
        }
    }
}
