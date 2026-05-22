using Sandbox;
using System;
using System.Linq;

namespace PartyPail.Movement;

public sealed partial class PlayerController : Component
{
	/// <summary>
	/// Make sure the body and our components are created
	/// </summary>
	void EnsureComponentsCreated()
	{
		if ( !ColliderObject.IsValid() )
		{
			ColliderObject = GameObject.Children.FirstOrDefault( x => x.Name == "Collider" );
			if ( !ColliderObject.IsValid() )
			{
				ColliderObject = new GameObject( GameObject, true, "Collider" );
			}
		}

		ColliderObject.LocalTransform = global::Transform.Zero;
		ColliderObject.Tags.SetFrom( BodyCollisionTags );

		Body.CollisionEventsEnabled = true;
		Body.CollisionUpdateEventsEnabled = true;
		Body.LinearDamping = 0.0f;
		Body.AngularDamping = 0.0f;
		Body.Gravity = false;
		Body.Locking = new PhysicsLock{ Pitch = true, Roll = true, Yaw = true };
		Body.RigidbodyFlags = RigidbodyFlags.DisableCollisionSounds;

		BodyCollider = ColliderObject.GetOrAddComponent<BoxCollider>();
		BodyCollider.Friction = 0;

		Body.Flags = Body.Flags.WithFlag( ComponentFlags.Hidden, !_showRigidBodyComponent );

		ColliderObject.Flags = ColliderObject.Flags.WithFlag( GameObjectFlags.Hidden, !ShowColliderComponent );
		BodyCollider.Flags = BodyCollider.Flags.WithFlag( ComponentFlags.Hidden, !ShowColliderComponent );

		if ( Renderer is null && UseAnimatorControls )
		{
			Renderer = GetComponentInChildren<SkinnedModelRenderer>();
		}
	}

	/// <summary>
	/// Update the body dimensions, and change the physical properties based on the current state
	/// </summary>
	void UpdateBody()
	{
		BodyCollider.Scale = new Vector3( BodyRadius, BodyRadius, CurrentHeight );
		BodyCollider.Center = new Vector3( 0, 0, BodyCollider.Scale.z * 0.5f );
		BodyCollider.Enabled = true;

		//
		// When trying to move, we move the mass center up to the waist so the player can "step" over smaller shit
		// When not moving we drop it to the foot position.
		//
		float massCenter = IsOnGround ? WishVelocity.Length.Clamp( 0, CurrentHeight * 0.5f ) : CurrentHeight * 0.5f;
		Body.MassCenterOverride = new Vector3( 0, 0, massCenter );
		Body.OverrideMassCenter = true;
		Body.MassOverride = BodyMass;

		Mode?.UpdateBody( Body );
	}
}
