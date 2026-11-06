using System;
using System.Collections.Generic;
using Survivor.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Survivor.Simulation
{
    /// Builds the inactive entity templates in code (one per category) and decorated clones of registry prefab overrides (5.6, F11).
    /// It attaches only SpriteRenderer and the category's SimEntity type; every injected decorator is applied to every template (TAC-F9-7).
    public sealed class PrefabCatalog
    {
        private readonly Dictionary<EntityCategory, Type> _componentTypes = new Dictionary<EntityCategory, Type>();
        private readonly List<IEntityTemplateDecorator> _decorators;
        private readonly Dictionary<EntityCategory, GameObject> _templates = new Dictionary<EntityCategory, GameObject>();
        private readonly Dictionary<GameObject, GameObject> _overrideTemplates = new Dictionary<GameObject, GameObject>();
        private GameObject _overrideRoot;

        /// Creates one template per category in entityComponentTypes (each type must be a non-abstract SimEntity subclass,
        /// else ArgumentException) and applies decorators to each in list order. Null arguments or decorators throw ArgumentNullException.
        public PrefabCatalog(IReadOnlyDictionary<EntityCategory, Type> entityComponentTypes, IReadOnlyList<IEntityTemplateDecorator> decorators)
        {
            if (entityComponentTypes == null)
            {
                throw new ArgumentNullException(nameof(entityComponentTypes));
            }

            if (decorators == null)
            {
                throw new ArgumentNullException(nameof(decorators));
            }

            _decorators = new List<IEntityTemplateDecorator>(decorators.Count);
            for (int i = 0; i < decorators.Count; i++)
            {
                if (decorators[i] == null)
                {
                    throw new ArgumentNullException(nameof(decorators), "PrefabCatalog: decorator " + i + " is null.");
                }

                _decorators.Add(decorators[i]);
            }

            foreach (EntityCategory category in Enum.GetValues(typeof(EntityCategory)))
            {
                if (entityComponentTypes.TryGetValue(category, out Type type))
                {
                    ValidateComponentType(category, type);
                    _componentTypes.Add(category, type);
                }
            }

            foreach (KeyValuePair<EntityCategory, Type> entry in _componentTypes)
            {
                _templates.Add(entry.Key, BuildTemplate(entry.Key, entry.Value));
            }
        }

        /// True when a template exists for category.
        public bool HasTemplate(EntityCategory category)
        {
            return _templates.ContainsKey(category);
        }

        /// Generated inactive template of category; throws InvalidOperationException when the catalog has no type for it.
        public GameObject GetTemplate(EntityCategory category)
        {
            if (!_templates.TryGetValue(category, out GameObject template))
            {
                throw new InvalidOperationException("PrefabCatalog: no entity component type was given for " + category + ".");
            }

            return template;
        }

        /// Template for a registry prefab override (VisualAsset.PrefabOverride): null returns GetTemplate(category); otherwise an
        /// inactive clone of prefabOverride, given the category's SimEntity type and SpriteRenderer when missing and decorated
        /// like the generated template. Clones are cached per prefab.
        public GameObject GetTemplate(EntityCategory category, GameObject prefabOverride)
        {
            if (prefabOverride == null)
            {
                return GetTemplate(category);
            }

            if (_overrideTemplates.TryGetValue(prefabOverride, out GameObject cached))
            {
                SimEntity cachedEntity = cached.GetComponent<SimEntity>();
                if (cachedEntity.Category != category)
                {
                    throw new InvalidOperationException("PrefabCatalog: prefab '" + prefabOverride.name + "' is already used as a "
                        + cachedEntity.Category + " template.");
                }

                return cached;
            }

            Type type = GetTemplate(category).GetComponent<SimEntity>().GetType();
            if (_overrideRoot == null)
            {
                // Inactive parent: prefab clones made under it never run Awake/OnEnable.
                _overrideRoot = new GameObject("PrefabCatalog.Overrides");
                _overrideRoot.SetActive(false);
            }

            GameObject clone = Object.Instantiate(prefabOverride, _overrideRoot.transform);
            clone.name = prefabOverride.name + ".Template";
            clone.SetActive(false);
            clone.transform.SetParent(null, false);
            SimEntity entity = clone.GetComponent<SimEntity>();
            if (entity != null && entity.GetType() != type)
            {
                Object.DestroyImmediate(clone);
                throw new InvalidOperationException("PrefabCatalog: prefab '" + prefabOverride.name + "' carries " + entity.GetType().Name
                    + " but " + category + " uses " + type.Name + ".");
            }

            Prepare(clone, category, type);
            _overrideTemplates.Add(prefabOverride, clone);
            return clone;
        }

        private GameObject BuildTemplate(EntityCategory category, Type type)
        {
            var template = new GameObject("Template." + category);
            template.SetActive(false);
            Prepare(template, category, type);
            return template;
        }

        private void Prepare(GameObject template, EntityCategory category, Type type)
        {
            if (template.GetComponent<SpriteRenderer>() == null)
            {
                template.AddComponent<SpriteRenderer>();
            }

            var entity = template.GetComponent(type) as SimEntity;
            if (entity == null)
            {
                entity = (SimEntity)template.AddComponent(type);
            }

            entity.SetCategory(category);
            for (int i = 0; i < _decorators.Count; i++)
            {
                _decorators[i].Decorate(template, category);
            }
        }

        private static void ValidateComponentType(EntityCategory category, Type type)
        {
            if (type == null || type.IsAbstract || !typeof(SimEntity).IsAssignableFrom(type))
            {
                throw new ArgumentException("PrefabCatalog: the component type for " + category
                    + " must be a non-abstract SimEntity subclass (got " + (type == null ? "null" : type.FullName) + ").");
            }
        }
    }
}
