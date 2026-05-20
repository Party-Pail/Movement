using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Sandbox;
using Sandbox.Movement;

namespace PartyPail.Movement;

/// <summary>
/// Source 1/Quake-based movement mode.
/// </summary>
public abstract class SourceMoveMode : MoveMode, Component.ICollisionListener
{
    /// <summary>
    /// Enable to use overrides specific to this component instance.
    /// </summary>
    [Property, ToggleGroup( "UseLocalCharacteristics", Label = "Use Local Characteristics" )]
    public bool UseLocalCharacteristics { get; set; } = false;

    /// <summary>
    /// Ground acceleration rate, in units/tick.
    /// </summary>
    [Property, Group( "UseLocalCharacteristics" )] public float Acceleration { get; set; } = 10;
    /// <summary>
    /// Air acceleration rate, in units/tick.
    /// </summary>
    [Property, Group( "UseLocalCharacteristics" )] public float AirAcceleration { get; set; } = 10;
    /// <summary>
    /// Slow walking speed, in units/tick.
    /// </summary>
    [Property, Group( "UseLocalCharacteristics" )] public float SlowWalkSpeed { get; set; } = 100;
    /// <summary>
    /// Absolute maximum player speed, in units/tick.
    /// </summary>
    [Property, Group( "UseLocalCharacteristics" )] public float MaxSpeed { get; set; } = 10000;
    /// <summary>
    /// Maximum air (wish) speed, in units/tick.
    /// </summary>
    [Property, Group( "UseLocalCharacteristics" )] public float MaxAirSpeed { get; set; } = 30;

    /// <summary>
    /// The button that the player will use to walk slowly.
    /// </summary>
    [Property, Feature( "Input" ), InputAction] public string SlowWalkButton { get; set; } = "walk";
    /// <summary>
    /// Set to true to linearly interpolate wish vector magnitude by the acceleration/deceleration times
    /// set in the PlayerController.
    /// </summary>
    [Property, Feature( "Input" )] public bool InterpolateWishSpeed { get; set; } = false;

    [ConVar( "sv_accelerate", ConVarFlags.Replicated, Help = "Ground acceleration rate, in units/tick." )]
    public static float GlobalAcceleration { get; set; } = 10;
    [ConVar( "sv_airaccelerate", ConVarFlags.Replicated, Help = "Air acceleration rate, in units/tick." )]
    public static float GlobalAirAcceleration { get; set; } = 10;
    [ConVar( "sv_duckedspeed", ConVarFlags.Replicated, Help = "Crouched walking speed, in units/tick." )]
    public static float GlobalDuckedSpeed { get; set; } = 60;
    [ConVar( "sv_slowwalkspeed", ConVarFlags.Replicated, Help = "Slow walking speed, in units/tick." )]
    public static float GlobalSlowWalkSpeed { get; set; } = 100;
    [ConVar( "sv_walkspeed", ConVarFlags.Replicated, Help = "Defult movement speed, in units/tick." )]
    public static float GlobalWalkSpeed { get; set; } = 200;
    [ConVar( "sv_runspeed", ConVarFlags.Replicated, Help = "Running movement speed, in units/tick." )]
    public static float GlobalRunSpeed { get; set; } = 400;
    [ConVar( "sv_maxspeed", ConVarFlags.Replicated, Help = "Absolute maximum player speed, in units/tick." )]
    public static float GlobalMaxSpeed { get; set; } = 10000;
    [ConVar( "sv_maxairspeed", ConVarFlags.Replicated, Help = "Maximum air (wish) speed, in units/tick." )]
    public static float GlobalMaxAirSpeed { get; set; } = 30;

    private List<Vector3> collisionPlanes = [];

    public void OnCollisionUpdate( Collision other )
    {
        if ( collisionPlanes.Count >= 3 ) return;
        collisionPlanes.Add( other.Contact.Normal );
    }

	public override void PrePhysicsStep()
	{
		base.PostPhysicsStep();

        collisionPlanes.Clear();
	}

    public override void AddVelocity()
    {
        var body = Controller.Body;
        var wish = Controller.WishVelocity;
        if ( wish.IsNearZeroLength ) return;

        var wishDir = wish.Normal;
        var wishSpeed = wish.Length;

        var groundVelocity = Controller.GroundVelocity;
        var currentZ = body.Velocity.z;

        var velocity = body.Velocity;
        if ( Controller.IsOnGround )
        {
            velocity = velocity.WithFriction( 8.0f * Time.Delta * Controller.GroundFriction, 100 );
        }
        velocity -= groundVelocity;

        if ( !Controller.IsOnGround )
        {
            wishSpeed = MathF.Min( wishSpeed, GetMaxAirSpeed() );
        }

        var maxAddedSpeed = wishSpeed - velocity.Dot( wishDir );
        if ( maxAddedSpeed > 0 )
        {
            // We are intentionally using the original wish vector here
            // and not scaling it to `wishSpeed`. This is how Quake did it;
            // take it up with id. Because every game descended from it
            // did the same thing, doing it "right" feels wrong.
            var accel = wish * Time.Delta * GetAcceleration() * GetFriction();
            accel = accel.ClampLength( maxAddedSpeed );

            velocity += accel + groundVelocity;
        }

        if ( Controller.IsOnGround )
        {
            velocity.z = currentZ;
        }

        Log.Info( $"original vel: {velocity}" );
        foreach ( Vector3 normal in collisionPlanes )
        {
            velocity = ClipVelocity( velocity, normal );
        }
        Log.Info( $"adjusted vel: {velocity}" );
        body.Velocity = velocity;
    }

    private Vector3.SmoothDamped smoothedWish;

	public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
	{
		input = input.ClampLength( 1 );
        var direction = eyes * input;
        var velocity = GetSpeed();

        if ( direction.IsNearlyZero( 0.1f ) )
        {
            return 0;
        }

        var wish = (direction * velocity).ClampLength( GetMaxSpeed() );

        if ( InterpolateWishSpeed )
        {
            smoothedWish.Current = direction.Normal * smoothedWish.Current.Length;
            smoothedWish.Target = wish;
            smoothedWish.SmoothTime = smoothedWish.Target.Length < smoothedWish.Current.Length ? Controller.DeaccelerationTime : Controller.AccelerationTime;
            smoothedWish.Update( Time.Delta );

            wish = smoothedWish.Current;
        }

        if ( wish.IsNearlyZero( 0.01f ) )
        {
            if ( InterpolateWishSpeed ) smoothedWish.Current = 0;
            return 0;
        }

        return wish;
	}

    private float GetFriction()
    {
        return Controller.IsOnGround ? MathF.Max( Controller.GroundFriction * 1.25f, 1.0f ) : Controller.AirFriction;
    }

    private float GetAcceleration()
    {
        if ( UseLocalCharacteristics )
        {
            return Controller.IsOnGround ? Acceleration : AirAcceleration;
        }
        else
        {
            return Controller.IsOnGround ? GlobalAcceleration : GlobalAirAcceleration;
        }
    }

    private float GetSpeed()
    {
        bool run = Input.Down( Controller.AltMoveButton );
        if ( Controller.RunByDefault ) run = !run;
        bool walk = Input.Down( SlowWalkButton );


        if ( Controller.IsDucking )
            return UseLocalCharacteristics ? Controller.DuckedSpeed : GlobalDuckedSpeed;
        if ( walk )
            return UseLocalCharacteristics ? SlowWalkSpeed : GlobalSlowWalkSpeed;
        if ( run )
            return UseLocalCharacteristics ? Controller.RunSpeed : GlobalRunSpeed;
        return UseLocalCharacteristics ? Controller.WalkSpeed : GlobalWalkSpeed;
    }

    private float GetMaxSpeed()
    {
        return UseLocalCharacteristics ? MaxSpeed : GlobalMaxSpeed;
    }

    private float GetMaxAirSpeed()
    {
        return UseLocalCharacteristics ? MaxAirSpeed : GlobalMaxAirSpeed;
    }

    private static Vector3 ClipVelocity( Vector3 velocity, Vector3 normal, float bounce = 1.0f )
    {
        var clipped = velocity - velocity.Dot( normal ) * bounce;

        // We shouldn't need to do this twice, but everyone else does.
        var adjust = clipped.Dot( normal );
        if ( adjust > 0.0f )
        {
            clipped -= normal * adjust;
        }

        return clipped;
    }
}
