using System;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.UnitTesting.Server;

namespace Robust.UnitTesting.Shared.GameObjects
{
    [TestFixture]
    public sealed class TransformComponent_Tests
    {
        /// <summary>
        /// Verify that WorldPosition and WorldRotation return the same result as the faster helper method.
        /// </summary>
        [Test]
        public void TestGetWorldMatches()
        {
            var server = RobustServerSimulation.NewSimulation().InitializeInstance();

            var entManager = server.Resolve<IEntityManager>();
            var mapManager = server.Resolve<IMapManager>();

            var mapId = mapManager.CreateMap();

            var ent1 = entManager.SpawnEntity(null, new MapCoordinates(Vector2.Zero, mapId));
            var ent2 = entManager.SpawnEntity(null, new MapCoordinates(new Vector2(100f, 0f), mapId));

            var xform1 = entManager.GetComponent<TransformComponent>(ent1);
            var xform2 = entManager.GetComponent<TransformComponent>(ent2);

            xform2.AttachParent(xform1);

            xform1.LocalRotation = MathF.PI;

            var (worldPos, worldRot, worldMatrix) = xform2.GetWorldPositionRotationMatrix();

            Assert.That(worldPos, Is.EqualTo(xform2.WorldPosition));
            Assert.That(worldRot, Is.EqualTo(xform2.WorldRotation));
            Assert.That(worldMatrix, Is.EqualTo(xform2.WorldMatrix));

            var (_, _, invWorldMatrix) = xform2.GetWorldPositionRotationInvMatrix();

            Assert.That(invWorldMatrix, Is.EqualTo(xform2.InvWorldMatrix));
        }
    }
}
