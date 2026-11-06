using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies.Tick
{
    /// Adds a disabled self ticker to templates (5.6): Monster -> MonsterSelfTicker, PlayerProjectile/EnemyProjectile -> ProjectileSelfTicker.
    /// Applied regardless of the tick strategy (TAC-F9-7); SelfTickStrategy enables the ticker on Register. Idempotent.
    public sealed class SelfTickTemplateDecorator : IEntityTemplateDecorator
    {
        /// Adds the category's disabled ticker once; other categories are left untouched. Null template throws ArgumentNullException.
        public void Decorate(GameObject template, EntityCategory category)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            switch (category)
            {
                case EntityCategory.Monster:
                    if (template.GetComponent<MonsterSelfTicker>() == null)
                    {
                        template.AddComponent<MonsterSelfTicker>().enabled = false;
                    }

                    break;
                case EntityCategory.PlayerProjectile:
                case EntityCategory.EnemyProjectile:
                    if (template.GetComponent<ProjectileSelfTicker>() == null)
                    {
                        template.AddComponent<ProjectileSelfTicker>().enabled = false;
                    }

                    break;
            }
        }
    }
}
