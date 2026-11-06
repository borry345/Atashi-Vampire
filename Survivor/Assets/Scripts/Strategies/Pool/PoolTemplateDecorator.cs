using System;
using Survivor.Core;
using UnityEngine;

namespace Survivor.Strategies.Pool
{
    /// Attaches PooledObject to every entity template, independent of the selected pool strategy (TAC-F9-7).
    public sealed class PoolTemplateDecorator : IEntityTemplateDecorator
    {
        /// Adds PooledObject to template unless it already has one; category does not change the result.
        public void Decorate(GameObject template, EntityCategory category)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (template.GetComponent<PooledObject>() == null)
            {
                template.AddComponent<PooledObject>();
            }
        }
    }
}
