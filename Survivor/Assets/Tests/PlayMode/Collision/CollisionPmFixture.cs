using NUnit.Framework;
using Survivor.Strategies;

namespace Survivor.Tests.PlayMode.Collision
{
    /// Base fixture: applies the project's Physics2D settings for each test and restores the previous globals afterwards.
    public abstract class CollisionPmFixture
    {
        private Physics2DGlobalsScope _globals;

        [SetUp]
        public void ApplyPhysicsSettings()
        {
            _globals = new Physics2DGlobalsScope();
            PhysicsSettingsApplier.Apply();
        }

        [TearDown]
        public void RestorePhysicsSettings()
        {
            CollisionPmUtil.DestroyAll();
            _globals.Restore();
        }
    }
}
