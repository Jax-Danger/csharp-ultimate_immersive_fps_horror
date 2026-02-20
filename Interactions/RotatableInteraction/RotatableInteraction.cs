using Godot;

namespace Game.Interactions;

[GlobalClass]
public partial class RotatableInteraction : AbstractInteraction
{
	[Export]
	public AudioStream MovementSound { get; set; }

	protected AudioStreamPlayer3D MovementAudioPlayer;

	[Export]
	public float MaximumRotation { get; set; }

	protected float StartingRotation = 0.0f;
	protected float CurrentAngle = 0.0f;
	protected float PreviousAngle = 0.0f;
	protected float AngularVelocity = 0.0f;
	protected float CreakVelocityThreshold = 0.0f;
	protected float FadeSpeed = 0.0f;
	protected float VolumeScale = 0.0f;
	protected float SmoothingCoefficient = 0.0f;
	protected bool AllowMovementSound = false;
	protected bool InputActive = false;

	public override void _Ready()
	{
		base._Ready();
		MaximumRotation = Mathf.DegToRad(Mathf.RadToDeg(StartingRotation) + MaximumRotation);

		MovementAudioPlayer = new AudioStreamPlayer3D
		{
			Stream = MovementSound
		};
		AddChild(MovementAudioPlayer);
	}

	public override void PreInteract()
	{
		base.PreInteract();
		LockCamera = true;
		PreviousAngle = CurrentAngle;
	}

	public override void Interact()
	{
		base.Interact();
		PlayMovementSounds((float)GetProcessDeltaTime());
	}

	public float GetRotationPercentage()
	{
		if (ObjectRef == null || Mathf.IsZeroApprox(MaximumRotation - StartingRotation))
		{
			return 0.0f;
		}

		return (ObjectRef.Rotation.Z - StartingRotation) / (MaximumRotation - StartingRotation);
	}

	protected void PlayMovementSounds(float delta)
	{
		float velocity = Mathf.Abs(CurrentAngle - PreviousAngle);
		float targetVolume = 0.0f;
		if (velocity > CreakVelocityThreshold)
		{
			targetVolume = Mathf.Clamp((velocity - CreakVelocityThreshold) * VolumeScale, 0.0f, 1.5f);
		}

		if (MovementAudioPlayer != null && !MovementAudioPlayer.Playing && targetVolume > 0.0f)
		{
			MovementAudioPlayer.VolumeDb = -15.0f;
			MovementAudioPlayer.Play();
		}

		if (MovementAudioPlayer != null && MovementAudioPlayer.Playing)
		{
			float currentVol = Mathf.DbToLinear(MovementAudioPlayer.VolumeDb);
			float newVol = Mathf.Lerp(currentVol, targetVolume, delta * FadeSpeed);
			MovementAudioPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(newVol, 0.0f, 1.5f));

			if (newVol < 0.001f && Mathf.IsZeroApprox(targetVolume))
			{
				MovementAudioPlayer.Stop();
			}
		}
	}

	protected void StopMovementSounds(float delta)
	{
		if (!AllowMovementSound || MovementAudioPlayer == null || !MovementAudioPlayer.Playing)
		{
			return;
		}

		float currentVol = Mathf.DbToLinear(MovementAudioPlayer.VolumeDb);
		float newVol = Mathf.Lerp(currentVol, 0.0f, delta * FadeSpeed);
		MovementAudioPlayer.VolumeDb = Mathf.LinearToDb(Mathf.Clamp(newVol, 0.0f, 1.0f));

		if (newVol < 0.001f)
		{
			MovementAudioPlayer.Stop();
		}
	}
}
