using Sandbox;

namespace PartyPail.Movement;

partial class MoveMode
{
	/// <summary>
	/// Get the position of the player's eye
	/// </summary>
	/// <returns></returns>
	public virtual Transform CalculateEyeTransform()
	{
		return new Transform
		{
			Position = Controller.WorldPosition + Vector3.Up * (Controller.CurrentHeight - Controller.EyeDistanceFromTop),
			Rotation = Controller.EyeAngles.ToRotation()
		};
	}

	/// <summary>
	/// Called to update the camera each frame
	/// </summary>
	public void UpdateCamera( CameraComponent cam )
	{

	}
}
