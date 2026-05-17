using Sandbox;

namespace PartyPail.Movement;

/// <summary>
/// The player is walking.
/// </summary>
[Icon( "transfer_within_a_station" ), Group( "Movement" ), Title( "Source MoveMode - Walk" )]
public class SourceMoveModeWalk : SourceMoveMode
{
    [Property] public int Priority { get; set; } = 1;

    /// <summary>
    /// Maximum angle the ground can be before the character starts sliding down it.
    /// </summary>
	[Property, Group( "UseLocalCharacteristics" )] public float GroundAngle { get; set; } = 45.0f;
    /// <summary>
    /// Maximum height the character can automatically ascend, as a "step."
    /// </summary>
	[Property, Group( "UseLocalCharacteristics" )] public float StepUpHeight { get; set; } = 18.0f;
    /// <summary>
    /// Maximum height the character can automatically descend, as a "step."
    /// </summary>
	[Property, Group( "UseLocalCharacteristics" )] public float StepDownHeight { get; set; } = 18.0f;

    [ConVar( "sv_groundangle", ConVarFlags.Replicated, Help = "Maximum angle the ground can be before the character starts sliding down it." )]
    public static float GlobalGroundAngle { get; set; } = 45.0f;
    [ConVar( "sv_stepsize", ConVarFlags.Replicated, Help = "Maximum height the character can automatically traverse, as a \"step.\"" )]
    public static float GlobalStepHeight { get; set; } = 45.0f;

	public override bool AllowGrounding => true;
	public override bool AllowFalling => true;

    public override int Score ( PlayerController controller ) => Priority;

	public override void AddVelocity()
	{
        Controller.WishVelocity = Controller.WishVelocity.WithZ( 0 );
		base.AddVelocity();
	}

	public override void PrePhysicsStep()
	{
		base.PrePhysicsStep();

        var stepHeight = GetStepUpHeight();
        if ( stepHeight > 0 )
        {
            TrySteppingUp( stepHeight );
        } 
	}

	public override void PostPhysicsStep()
	{
		base.PostPhysicsStep();

        StickToGround( GetStepDownHeight() );
	}

	public override bool IsStandableSurace( in SceneTraceResult result )
	{
        return Vector3.GetAngle( Vector3.Up, result.Normal ) <= GroundAngle;
	}

	public override Vector3 UpdateMove( Rotation eyes, Vector3 input )
	{
        eyes = eyes.Angles() with { pitch = 0 };

		return base.UpdateMove( eyes, input );
	}

    private float GetStepUpHeight()
    {
        return UseLocalCharacteristics ? StepUpHeight : GlobalStepHeight;
    }

    private float GetStepDownHeight()
    {
        return UseLocalCharacteristics ? StepDownHeight : GlobalStepHeight;
    }
}