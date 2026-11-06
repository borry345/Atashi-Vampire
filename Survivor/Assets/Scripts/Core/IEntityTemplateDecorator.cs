using UnityEngine;

namespace Survivor.Core
{
    /// Hook that lets Strategies attach their components to entity templates without Simulation referencing Strategies.
    public interface IEntityTemplateDecorator
    {
        /// Called once per template at bootstrap.
        void Decorate(GameObject template, EntityCategory category);
    }
}
