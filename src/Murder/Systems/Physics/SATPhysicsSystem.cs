﻿using Bang.Components;
using Bang.Contexts;
using Bang.Entities;
using Bang.Systems;
using Murder.Components;
using Murder.Core;
using Murder.Core.Dialogs;
using Murder.Core.Geometry;
using Murder.Core.Physics;
using Murder.Diagnostics;
using Murder.Messages;
using Murder.Services;
using Murder.Utilities;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;

namespace Murder.Systems.Physics
{
    [Filter(typeof(ITransformComponent), typeof(VelocityComponent))]
    public class SATPhysicsSystem : IFixedUpdateSystem
    {
        public void FixedUpdate(Context context)
        {
            Map map = context.World.GetUnique<MapComponent>().Map;
            Quadtree qt = context.World.GetUnique<QuadtreeComponent>().Quadtree;
            List<(Entity entity, Rectangle boundingBox)> entityList = new();
            HashSet<int> ignore = new();

            foreach (Entity e in context.Entities)
            {
                bool ignoreCollisions = false;
                var collider = e.TryGetCollider();
                ignore.Clear();
                ignore.Add(e.EntityId);
                if (e.Parent is not null)
                    ignore.Add(e.Parent.Value);
                foreach (var child in e.Children)
                {
                    ignore.Add(child);
                }

                int mask = CollisionLayersBase.SOLID | CollisionLayersBase.HOLE;
                if (e.TryGetCustomCollisionMask() is CustomCollisionMask agent)
                    mask = agent.CollisionMask;

                // If the entity has a velocity, we'll move around by checking for collisions first.
                if (e.TryGetVelocity()?.Velocity is Vector2 currentVelocity)
                {
                    Vector2 startVelocity = currentVelocity;
                    
                    Vector2 velocity = currentVelocity * Game.FixedDeltaTime;
                    IMurderTransformComponent relativeStartPosition = e.GetTransform();
                    Vector2 startPosition = relativeStartPosition.GetGlobal().ToVector2();

                    if (MathF.Abs(currentVelocity.X) < 0.5f && MathF.Abs(currentVelocity.Y) < 0.5f)
                    {
                        ignoreCollisions = true;
                    }

                    if (collider is not null && collider.Value.Layer == CollisionLayersBase.TRIGGER)
                    {
                        ignoreCollisions = true;
                    }

                    if (collider is null)
                    {
                        ignoreCollisions = true;
                    }

                    if (ignoreCollisions)
                    {
                        e.SetGlobalPosition(startPosition + velocity);
                    }
                    else
                    {
                        entityList.Clear();
                        qt.GetEntitiesAt(collider!.Value.GetBoundingBox((startPosition + velocity).Point), ref entityList);
                        var collisionEntities = PhysicsServices.FilterPositionAndColliderEntities(entityList, CollisionLayersBase.SOLID | CollisionLayersBase.HOLE);
                        
                        int exhaustCounter = 10;
                        int potentialHit;
                        Vector2 potentialPushout;

                        Vector2 moveToPosition = startPosition + velocity;
                        
                        int hitId = -1;
                        Vector2 pushout = Vector2.Zero;
                        while (PhysicsServices.GetFirstMtvAt(map, ignore, collider.Value, moveToPosition, collisionEntities, mask, out potentialHit, out potentialPushout)
                            && exhaustCounter-- > 0)
                        {
                            moveToPosition -= potentialPushout;
                            if (potentialHit >= 0)
                            {
                                hitId = potentialHit;
                            }

                            if (potentialPushout.HasValue)
                            {
                                pushout = potentialPushout;
                            }
                        }
                        if (exhaustCounter == 0)
                            GameLogger.Warning("Exhausted collision checks! Maybe increase it?");

                        e.SetGlobalPosition(moveToPosition - pushout);

                        Vector2 translationVector = startPosition + velocity - moveToPosition;

                        // Some collision was found!
                        if (hitId>=0)
                        {
                            // Slide the speed accordingly
                            Vector2 edgePerpendicularToMTV = new Vector2(-translationVector.Y, translationVector.X);
                            Vector2 normalizedEdge = edgePerpendicularToMTV.Normalized();
                            float dotProduct = Vector2.Dot(currentVelocity, normalizedEdge);
                            currentVelocity = normalizedEdge * dotProduct;
                            e.SendMessage(new CollidedWithMessage(hitId, pushout));
                        }
                    }

                    // Don't change velocity if the velocity was already changed by the message
                    if (startVelocity == (e.TryGetVelocity()?.Velocity ?? Vector2.Zero))
                    {
                        // Makes sure that the velocity snaps to zero if it's too small.
                        if (currentVelocity.Manhattan() > 0.0001f)
                        {
                            e.SetVelocity(currentVelocity);
                        }
                        else
                        {
                            e.RemoveVelocity();
                        }
                    }
                }
            }
        }
    }
}
