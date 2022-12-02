// Copyright (c) 2017 Kastellanos Nikolaos

/* Original source Farseer Physics Engine:
 * Copyright (c) 2014 Ian Qvist, http://farseerphysics.codeplex.com
 * Microsoft Permissive License (Ms-PL) v1.1
 */

/*
* Farseer Physics Engine:
* Copyright (c) 2012 Ian Qvist
*
* Original source Box2D:
* Copyright (c) 2006-2011 Erin Catto http://www.box2d.org
*
* This software is provided 'as-is', without any express or implied
* warranty.  In no event will the authors be held liable for any damages
* arising from the use of this software.
* Permission is granted to anyone to use this software for any purpose,
* including commercial applications, and to alter it and redistribute it
* freely, subject to the following restrictions:
* 1. The origin of this software must not be misrepresented; you must not
* claim that you wrote the original software. If you use this software
* in a product, an acknowledgment in the product documentation would be
* appreciated but is not required.
* 2. Altered source versions must be plainly marked as such, and must not be
* misrepresented as being the original software.
* 3. This notice may not be removed or altered from any source distribution.
*/

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Collision;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Contacts;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Robust.Shared.Physics.Dynamics
{
    internal sealed class ContactManager
    {
        private readonly IEntityManager _entityManager;
        private readonly IPhysicsManager _physicsManager;
        private readonly IConfigurationManager _cfg;

        private EntityLookupSystem _lookup = default!;
        private SharedPhysicsSystem _physics = default!;
        private SharedTransformSystem _transform = default!;

        internal MapId MapId { get; set; }

        // TODO: Jesus we should really have a test for this
        /// <summary>
        ///     Ordering is under <see cref="ShapeType"/>
        ///     uses enum to work out which collision evaluation to use.
        /// </summary>
        private static Contact.ContactType[,] _registers = {
                                                           {
                                                               // Circle register
                                                               Contact.ContactType.Circle,
                                                               Contact.ContactType.EdgeAndCircle,
                                                               Contact.ContactType.PolygonAndCircle,
                                                               Contact.ContactType.ChainAndCircle,
                                                           },
                                                           {
                                                               // Edge register
                                                               Contact.ContactType.EdgeAndCircle,
                                                               Contact.ContactType.NotSupported, // Edge
                                                               Contact.ContactType.EdgeAndPolygon,
                                                               Contact.ContactType.NotSupported, // Chain
                                                           },
                                                           {
                                                               // Polygon register
                                                               Contact.ContactType.PolygonAndCircle,
                                                               Contact.ContactType.EdgeAndPolygon,
                                                               Contact.ContactType.Polygon,
                                                               Contact.ContactType.ChainAndPolygon,
                                                           },
                                                           {
                                                               // Chain register
                                                               Contact.ContactType.ChainAndCircle,
                                                               Contact.ContactType.NotSupported, // Edge
                                                               Contact.ContactType.ChainAndPolygon,
                                                               Contact.ContactType.NotSupported, // Chain
                                                           }
                                                       };

        public int ContactCount => _activeContacts.Count;

        private int ContactPoolInitialSize = 64;

        private readonly ObjectPool<Contact> _contactPool;

        internal LinkedList<Contact> _activeContacts = new();

        // Didn't use the eventbus because muh allocs on something being run for every collision every frame.
        /// <summary>
        ///     Invoked whenever a KinematicController body collides. The first body is always guaranteed to be a KinematicController
        /// </summary>
        internal event Action<Fixture, Fixture, float, Vector2>? KinematicControllerCollision;

        private int _contactMultithreadThreshold;
        private int _contactMinimumThreads;

        // TODO: Also need to clean the station up to not have 160 contacts on roundstart

        private sealed class ContactPoolPolicy : IPooledObjectPolicy<Contact>
        {
            private readonly SharedDebugPhysicsSystem _debugPhysicsSystem;
            private readonly IManifoldManager _manifoldManager;

            public ContactPoolPolicy(SharedDebugPhysicsSystem debugPhysicsSystem, IManifoldManager manifoldManager)
            {
                _debugPhysicsSystem = debugPhysicsSystem;
                _manifoldManager = manifoldManager;
            }

            public Contact Create()
            {
                var contact = new Contact(_manifoldManager);
#if DEBUG
                contact._debugPhysics = _debugPhysicsSystem;
#endif
                contact.Manifold = new Manifold
                {
                    Points = new ManifoldPoint[2]
                };

                return contact;
            }

            public bool Return(Contact obj)
            {
                SetContact(obj, null, 0, null, 0);
                return true;
            }
        }

        public ContactManager(
            SharedDebugPhysicsSystem debugPhysicsSystem,
            IManifoldManager manifoldManager,
            IEntityManager entityManager,
            IPhysicsManager physicsManager,
            IConfigurationManager cfg)
        {
            _entityManager = entityManager;
            _physicsManager = physicsManager;
            _cfg = cfg;

            _contactPool = new DefaultObjectPool<Contact>(
                new ContactPoolPolicy(debugPhysicsSystem, manifoldManager),
                1024);
        }

        private static void SetContact(Contact contact, Fixture? fixtureA, int indexA, Fixture? fixtureB, int indexB)
        {
            contact.Enabled = true;
            contact.IsTouching = false;
            contact.Flags = ContactFlags.None;
            // TOIFlag = false;

            contact.FixtureA = fixtureA;
            contact.FixtureB = fixtureB;

            contact.ChildIndexA = indexA;
            contact.ChildIndexB = indexB;

            contact.Manifold.PointCount = 0;

            //FPE: We only set the friction and restitution if we are not destroying the contact
            if (fixtureA != null && fixtureB != null)
            {
                contact.Friction = MathF.Sqrt(fixtureA.Friction * fixtureB.Friction);
                contact.Restitution = MathF.Max(fixtureA.Restitution, fixtureB.Restitution);
            }

            contact.TangentSpeed = 0;
        }

        public void Initialize()
        {
            _lookup = _entityManager.EntitySysManager.GetEntitySystem<EntityLookupSystem>();
            _physics = _entityManager.EntitySysManager.GetEntitySystem<SharedPhysicsSystem>();
            _transform = _entityManager.EntitySysManager.GetEntitySystem<SharedTransformSystem>();

            _cfg.OnValueChanged(CVars.ContactMultithreadThreshold, OnContactMultithreadThreshold, true);
            _cfg.OnValueChanged(CVars.ContactMinimumThreads, OnContactMinimumThreads, true);

            InitializePool();
        }

        public void Shutdown()
        {
            _cfg.UnsubValueChanged(CVars.ContactMultithreadThreshold, OnContactMultithreadThreshold);
            _cfg.UnsubValueChanged(CVars.ContactMinimumThreads, OnContactMinimumThreads);
        }

        private void OnContactMultithreadThreshold(int value)
        {
            _contactMultithreadThreshold = value;
        }

        private void OnContactMinimumThreads(int value)
        {
            _contactMinimumThreads = value;
        }

        private void InitializePool()
        {
            var dummy = new Contact[ContactPoolInitialSize];

            for (var i = 0; i < ContactPoolInitialSize; i++)
            {
                dummy[i] = _contactPool.Get();
            }

            for (var i = 0; i < ContactPoolInitialSize; i++)
            {
                _contactPool.Return(dummy[i]);
            }
        }

        private Contact CreateContact(Fixture fixtureA, int indexA, Fixture fixtureB, int indexB)
        {
            var type1 = fixtureA.Shape.ShapeType;
            var type2 = fixtureB.Shape.ShapeType;

            DebugTools.Assert(ShapeType.Unknown < type1 && type1 < ShapeType.TypeCount);
            DebugTools.Assert(ShapeType.Unknown < type2 && type2 < ShapeType.TypeCount);

            // Pull out a spare contact object
            var contact = _contactPool.Get();

            // Edge+Polygon is non-symmetrical due to the way Erin handles collision type registration.
            if ((type1 >= type2 || (type1 == ShapeType.Edge && type2 == ShapeType.Polygon)) && !(type2 == ShapeType.Edge && type1 == ShapeType.Polygon))
            {
                SetContact(contact, fixtureA, indexA, fixtureB, indexB);
            }
            else
            {
                SetContact(contact, fixtureB, indexB, fixtureA, indexA);
            }

            contact.Type = _registers[(int)type1, (int)type2];

            return contact;
        }

        /// <summary>
        /// Try to create a contact between these 2 fixtures.
        /// </summary>
        internal void AddPair(Fixture fixtureA, int indexA, Fixture fixtureB, int indexB, ContactFlags flags = ContactFlags.None)
        {
            PhysicsComponent bodyA = fixtureA.Body;
            PhysicsComponent bodyB = fixtureB.Body;

            // Broadphase has already done the faster check for collision mask / layers
            // so no point duplicating

            // Does a contact already exist?
            if (fixtureA.Contacts.ContainsKey(fixtureB))
                return;

            DebugTools.Assert(!fixtureB.Contacts.ContainsKey(fixtureA));

            // Does a joint override collision? Is at least one body dynamic?
            if (!_physics.ShouldCollide(bodyB, bodyA))
                return;

            // Call the factory.
            var contact = CreateContact(fixtureA, indexA, fixtureB, indexB);
            contact.Flags = flags;

            // Contact creation may swap fixtures.
            fixtureA = contact.FixtureA!;
            fixtureB = contact.FixtureB!;
            bodyA = fixtureA.Body;
            bodyB = fixtureB.Body;

            // Insert into world
            _activeContacts.AddLast(contact.MapNode);

            // Connect to body A
            DebugTools.Assert(!fixtureA.Contacts.ContainsKey(fixtureB));
            fixtureA.Contacts.Add(fixtureB, contact);
            bodyA.Contacts.AddLast(contact.BodyANode);

            // Connect to body B
            DebugTools.Assert(!fixtureB.Contacts.ContainsKey(fixtureA));
            fixtureB.Contacts.Add(fixtureA, contact);
            bodyB.Contacts.AddLast(contact.BodyBNode);
        }

        /// <summary>
        ///     Go through the cached broadphase movement and update contacts.
        /// </summary>
        internal void AddPair(in FixtureProxy proxyA, in FixtureProxy proxyB)
        {
            AddPair(proxyA.Fixture, proxyA.ChildIndex, proxyB.Fixture, proxyB.ChildIndex);
        }

        internal static bool ShouldCollide(Fixture fixtureA, Fixture fixtureB)
        {
            return !((fixtureA.CollisionMask & fixtureB.CollisionLayer) == 0x0 &&
                     (fixtureB.CollisionMask & fixtureA.CollisionLayer) == 0x0);
        }

        public void Destroy(Contact contact)
        {
            Fixture fixtureA = contact.FixtureA!;
            Fixture fixtureB = contact.FixtureB!;
            PhysicsComponent bodyA = fixtureA.Body;
            PhysicsComponent bodyB = fixtureB.Body;

            if (contact.IsTouching)
            {
                var ev1 = new EndCollideEvent(fixtureA, fixtureB);
                var ev2 = new EndCollideEvent(fixtureB, fixtureA);
                _entityManager.EventBus.RaiseLocalEvent(bodyA.Owner, ref ev1);
                _entityManager.EventBus.RaiseLocalEvent(bodyB.Owner, ref ev2);
            }

            if (contact.Manifold.PointCount > 0 && contact.FixtureA?.Hard == true && contact.FixtureB?.Hard == true)
            {
                if (bodyA.CanCollide)
                    _physics.SetAwake(contact.FixtureA.Body, true);

                if (bodyB.CanCollide)
                    _physics.SetAwake(contact.FixtureB.Body, true);
            }

            // Remove from the world
            _activeContacts.Remove(contact.MapNode);

            // Remove from body 1
            DebugTools.Assert(fixtureA.Contacts.ContainsKey(fixtureB));
            fixtureA.Contacts.Remove(fixtureB);
            DebugTools.Assert(bodyA.Contacts.Contains(contact.BodyANode!.Value));
            bodyA.Contacts.Remove(contact.BodyANode);

            // Remove from body 2
            DebugTools.Assert(fixtureB.Contacts.ContainsKey(fixtureA));
            fixtureB.Contacts.Remove(fixtureA);
            bodyB.Contacts.Remove(contact.BodyBNode);

            // Insert into the pool.
            _contactPool.Return(contact);
        }

        internal void Collide()
        {
            // Due to the fact some contacts may be removed (and we need to update this array as we iterate).
            // the length may not match the actual contact count, hence we track the index.
            var contacts = ArrayPool<Contact>.Shared.Rent(ContactCount);
            var index = 0;

            // Can be changed while enumerating
            // TODO: check for null instead?
            // Work out which contacts are still valid before we decide to update manifolds.
            var node = _activeContacts.First;
            var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();

            while (node != null)
            {
                var contact = node.Value;
                node = node.Next;

                Fixture fixtureA = contact.FixtureA!;
                Fixture fixtureB = contact.FixtureB!;
                int indexA = contact.ChildIndexA;
                int indexB = contact.ChildIndexB;

                PhysicsComponent bodyA = fixtureA.Body;
                PhysicsComponent bodyB = fixtureB.Body;

                // Do not try to collide disabled bodies
                if (!bodyA.CanCollide || !bodyB.CanCollide)
                {
                    Destroy(contact);
                    continue;
                }

                // Is this contact flagged for filtering?
                if ((contact.Flags & ContactFlags.Filter) != 0x0)
                {
                    // Should these bodies collide?
                    if (_physics.ShouldCollide(bodyB, bodyA) == false)
                    {
                        Destroy(contact);
                        continue;
                    }

                    // Check default filtering
                    if (ShouldCollide(fixtureA, fixtureB) == false)
                    {
                        Destroy(contact);
                        continue;
                    }

                    // Clear the filtering flag.
                    contact.Flags &= ~ContactFlags.Filter;
                }

                bool activeA = bodyA.Awake && bodyA.BodyType != BodyType.Static;
                bool activeB = bodyB.Awake && bodyB.BodyType != BodyType.Static;

                // At least one body must be awake and it must be dynamic or kinematic.
                if (activeA == false && activeB == false)
                {
                    continue;
                }

                // Special-case grid contacts.
                if ((contact.Flags & ContactFlags.Grid) != 0x0)
                {
                    var xformA = xformQuery.GetComponent(bodyA.Owner);
                    var xformB = xformQuery.GetComponent(bodyB.Owner);

                    var gridABounds = fixtureA.Shape.ComputeAABB(_physics.GetPhysicsTransform(bodyA.Owner, xformA, xformQuery), 0);
                    var gridBBounds = fixtureB.Shape.ComputeAABB(_physics.GetPhysicsTransform(bodyB.Owner, xformB, xformQuery), 0);

                    if (!gridABounds.Intersects(gridBBounds))
                    {
                        Destroy(contact);
                    }
                    else
                    {
                        contacts[index++] = contact;
                    }

                    continue;
                }

                var proxyA = fixtureA.Proxies[indexA];
                var proxyB = fixtureB.Proxies[indexB];
                var broadphaseA = _lookup.GetCurrentBroadphase(xformQuery.GetComponent(bodyA.Owner));
                var broadphaseB = _lookup.GetCurrentBroadphase(xformQuery.GetComponent(bodyB.Owner));
                var overlap = false;

                // We can have cross-broadphase proxies hence need to change them to worldspace
                if (broadphaseA != null && broadphaseB != null)
                {
                    if (broadphaseA == broadphaseB)
                    {
                        overlap = proxyA.AABB.Intersects(proxyB.AABB);
                    }
                    else
                    {
                        // These should really be destroyed before map changes.
                        DebugTools.Assert(xformQuery.GetComponent(broadphaseA.Owner).MapID == xformQuery.GetComponent(broadphaseB.Owner).MapID);

                        var proxyAWorldAABB = _transform.GetWorldMatrix(broadphaseA.Owner, xformQuery).TransformBox(proxyA.AABB);
                        var proxyBWorldAABB = _transform.GetWorldMatrix(broadphaseB.Owner, xformQuery).TransformBox(proxyB.AABB);
                        overlap = proxyAWorldAABB.Intersects(proxyBWorldAABB);
                    }
                }

                // Here we destroy contacts that cease to overlap in the broad-phase.
                if (!overlap)
                {
                    Destroy(contact);
                    continue;
                }

                contacts[index++] = contact;
            }

            var status = ArrayPool<ContactStatus>.Shared.Rent(index);

            // To avoid race conditions with the dictionary we'll cache all of the transforms up front.
            // Caching should provide better perf than multi-threading the GetTransform() as we can also re-use
            // these in PhysicsIsland as well.
            for (var i = 0; i < index; i++)
            {
                var contact = contacts[i];
                var bodyA = contact.FixtureA!.Body;
                var bodyB = contact.FixtureB!.Body;

                _physicsManager.EnsureTransform(bodyA.Owner);
                _physicsManager.EnsureTransform(bodyB.Owner);
            }

            // Update contacts all at once.
            BuildManifolds(contacts, index, status);

            // Single-threaded so content doesn't need to worry about race conditions.
            for (var i = 0; i < index; i++)
            {
                var contact = contacts[i];

                switch (status[i])
                {
                    case ContactStatus.StartTouching:
                    {
                        if (!contact.IsTouching) continue;

                        var fixtureA = contact.FixtureA!;
                        var fixtureB = contact.FixtureB!;
                        var bodyA = fixtureA.Body;
                        var bodyB = fixtureB.Body;
                        var worldPoint = Transform.Mul(_physicsManager.EnsureTransform(bodyA), contact.Manifold.LocalPoint);

                        var ev1 = new StartCollideEvent(fixtureA, fixtureB, worldPoint);
                        var ev2 = new StartCollideEvent(fixtureB, fixtureA, worldPoint);

                        _entityManager.EventBus.RaiseLocalEvent(bodyA.Owner, ref ev1, true);
                        _entityManager.EventBus.RaiseLocalEvent(bodyB.Owner, ref ev2, true);
                        break;
                    }
                    case ContactStatus.Touching:
                        break;
                    case ContactStatus.EndTouching:
                    {
                        var fixtureA = contact.FixtureA;
                        var fixtureB = contact.FixtureB;

                        // If something under StartCollideEvent potentially nukes other contacts (e.g. if the entity is deleted)
                        // then we'll just skip the EndCollide.
                        if (fixtureA == null || fixtureB == null) continue;

                        var bodyA = fixtureA.Body;
                        var bodyB = fixtureB.Body;

                        var ev1 = new EndCollideEvent(fixtureA, fixtureB);
                        var ev2 = new EndCollideEvent(fixtureB, fixtureA);

                        _entityManager.EventBus.RaiseLocalEvent(bodyA.Owner, ref ev1);
                        _entityManager.EventBus.RaiseLocalEvent(bodyB.Owner, ref ev2);
                        break;
                    }
                    case ContactStatus.NoContact:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            ArrayPool<Contact>.Shared.Return(contacts);
            ArrayPool<ContactStatus>.Shared.Return(status);
        }

        private void BuildManifolds(Contact[] contacts, int count, ContactStatus[] status)
        {
            var wake = ArrayPool<bool>.Shared.Rent(count);

            if (count > _contactMultithreadThreshold * _contactMinimumThreads)
            {
                var (batches, batchSize) = SharedPhysicsSystem.GetBatch(count, _contactMultithreadThreshold);

                Parallel.For(0, batches, i =>
                {
                    var start = i * batchSize;
                    var end = Math.Min(start + batchSize, count);
                    UpdateContacts(contacts, start, end, status, wake);
                });

            }
            else
            {
                UpdateContacts(contacts, 0, count, status, wake);
            }

            // Can't do this during UpdateContacts due to IoC threading issues.
            for (var i = 0; i < count; i++)
            {
                var shouldWake = wake[i];
                if (!shouldWake) continue;

                var contact = contacts[i];
                var bodyA = contact.FixtureA!.Body;
                var bodyB = contact.FixtureB!.Body;

                _physics.SetAwake(bodyA, true);
                _physics.SetAwake(bodyB, true);
            }

            ArrayPool<bool>.Shared.Return(wake);
        }

        private void UpdateContacts(Contact[] contacts, int start, int end, ContactStatus[] status, bool[] wake)
        {
            for (var i = start; i < end; i++)
            {
                status[i] = contacts[i].Update(_physicsManager, out wake[i]);
            }
        }

        public void PreSolve(float frameTime)
        {
            Span<Vector2> points = stackalloc Vector2[2];

            // We'll do pre and post-solve around all islands rather than each specific island as it seems cleaner with race conditions.
            var node = _activeContacts.First;

            while (node != null)
            {
                var contact = node.Value;
                node = node.Next;

                if (contact is not {IsTouching: true, Enabled: true}) continue;

                var bodyA = contact.FixtureA!.Body;
                var bodyB = contact.FixtureB!.Body;
                contact.GetWorldManifold(_physicsManager, out var worldNormal, points);

                // Didn't use an EntitySystemMessage as this is called FOR EVERY COLLISION AND IS REALLY EXPENSIVE
                // so we just use the Action. Also we'll sort out BodyA / BodyB for anyone listening first.
                if (bodyA.BodyType == BodyType.KinematicController)
                {
                    KinematicControllerCollision?.Invoke(contact.FixtureA!, contact.FixtureB!, frameTime, -worldNormal);
                }
                else if (bodyB.BodyType == BodyType.KinematicController)
                {
                    KinematicControllerCollision?.Invoke(contact.FixtureB!, contact.FixtureA!, frameTime, worldNormal);
                }
            }
        }
    }

    internal enum ContactStatus : byte
    {
        NoContact = 0,
        StartTouching = 1,
        Touching = 2,
        EndTouching = 3,
    }
}
