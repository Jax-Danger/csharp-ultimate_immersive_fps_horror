using Godot;

namespace Game.Interactions;

[GlobalClass]
public partial class DoorInteraction : RotatableInteraction
{
	[Export]
	public Node3D PivotPoint { get; set; }

	[Export]
	public string UnlockKeyName { get; set; } = string.Empty;

	[Export]
	public bool IsLocked { get; set; } = false;

	[Export]
	public bool FlipPivot { get; set; } = false;

	[Export]
	public bool ReverseInputDirection { get; set; } = false;

	[Export]
	public AudioStream ShutSoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/DoorClose2.ogg");

	[Export]
	public AudioStream LockedSoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/DoorLocked.ogg");

	private AudioStreamPlayer3D _shutAudioPlayer;
	private AudioStreamPlayer3D _lockedAudioPlayer;
	private bool _isFront;
	private bool _doorOpened = false;
	private float _shutAngleThreshold = 0.2f;
	private float _shutSnapRange = 0.05f;
	private bool _wasJustUnlocked = false;

	public override void _EnterTree()
	{
		base._EnterTree();
		MovementSound = GD.Load<AudioStream>("res://assets/sound_effects/DoorCreak.ogg");
	}

	public override void _Ready()
	{
		base._Ready();

		if (PivotPoint != null)
		{
			StartingRotation = PivotPoint.Rotation.Y;
			MaximumRotation = FlipPivot
				? StartingRotation - Mathf.Abs(MaximumRotation)
				: StartingRotation + Mathf.Abs(MaximumRotation);
			CurrentAngle = StartingRotation;
			PreviousAngle = StartingRotation;
		}

		_shutAudioPlayer = new AudioStreamPlayer3D { Stream = ShutSoundEffect };
		AddChild(_shutAudioPlayer);

		_lockedAudioPlayer = new AudioStreamPlayer3D { Stream = LockedSoundEffect };
		AddChild(_lockedAudioPlayer);

		CreakVelocityThreshold = 0.005f;
		FadeSpeed = 1.0f;
		VolumeScale = 1000.0f;
		SmoothingCoefficient = 80.0f;
	}

	public override void _Process(double deltaRaw)
	{
		base._Process(deltaRaw);
		if (PivotPoint == null)
		{
			return;
		}

		float delta = (float)deltaRaw;

		if (_wasJustUnlocked)
		{
			AngularVelocity = 0.0f;
			InputActive = false;
			CurrentAngle = StartingRotation;
			Vector3 rot = PivotPoint.Rotation;
			rot.Y = StartingRotation;
			PivotPoint.Rotation = rot;
			_wasJustUnlocked = false;
		}
		else
		{
			if (!InputActive)
			{
				AngularVelocity = Mathf.Lerp(AngularVelocity, 0.0f, delta * 4.0f);
			}

			CurrentAngle += AngularVelocity;

			if (IsLocked)
			{
				float lockWiggle = 0.02f;
				CurrentAngle = FlipPivot
					? Mathf.Clamp(CurrentAngle, StartingRotation - lockWiggle, StartingRotation)
					: Mathf.Clamp(CurrentAngle, StartingRotation, StartingRotation + lockWiggle);

				Vector3 rot = PivotPoint.Rotation;
				rot.Y = CurrentAngle;
				PivotPoint.Rotation = rot;

				if (InputActive && _lockedAudioPlayer != null && !_lockedAudioPlayer.Playing && !Mathf.IsEqualApprox(PreviousAngle, CurrentAngle))
				{
					_lockedAudioPlayer.Play();
					InputActive = false;
				}
			}
			else
			{
				CurrentAngle = FlipPivot
					? Mathf.Clamp(CurrentAngle, MaximumRotation, StartingRotation)
					: Mathf.Clamp(CurrentAngle, StartingRotation, MaximumRotation);

				Vector3 rot = PivotPoint.Rotation;
				rot.Y = CurrentAngle;
				PivotPoint.Rotation = rot;
				InputActive = false;

				if (Mathf.IsEqualApprox(PreviousAngle, CurrentAngle))
				{
					StopMovementSounds(delta);
				}
				else
				{
					PlayMovementSounds(delta);
				}
			}

			PreviousAngle = CurrentAngle;
		}

		if (Mathf.Abs(CurrentAngle - StartingRotation) > _shutAngleThreshold)
		{
			_doorOpened = true;
		}

		if (_doorOpened && Mathf.Abs(CurrentAngle - StartingRotation) < _shutSnapRange)
		{
			AllowMovementSound = false;
			AngularVelocity = 0.0f;
			MovementAudioPlayer?.Stop();
			_shutAudioPlayer?.Stop();
			_shutAudioPlayer?.Play();
			_doorOpened = false;
		}
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if (!IsInteracting || @event is not InputEventMouseMotion mouse)
		{
			return;
		}

		InputActive = true;
		AllowMovementSound = true;

		float inputDelta = -mouse.Relative.Y * 0.001f;
		if (!_isFront)
		{
			inputDelta = -inputDelta;
		}
		if (FlipPivot)
		{
			inputDelta = -inputDelta;
		}
		if (ReverseInputDirection)
		{
			inputDelta = -inputDelta;
		}
		if (Mathf.Abs(inputDelta) < 0.01f)
		{
			inputDelta *= 0.25f;
		}

		AngularVelocity = Mathf.Lerp(AngularVelocity, inputDelta, 1.0f / SmoothingCoefficient);
	}

	public void SetDirection(Vector3 normal)
	{
		_isFront = normal.Z > 0.0f;
	}

	public void Unlock()
	{
		IsLocked = false;
		_wasJustUnlocked = true;
		AngularVelocity = 0.0f;
		InputActive = false;
		CurrentAngle = StartingRotation;

		if (PivotPoint != null)
		{
			Vector3 rot = PivotPoint.Rotation;
			rot.Y = StartingRotation;
			PivotPoint.Rotation = rot;
		}
	}

	public void PlayDoorShutSound(float volumeDb = 0.0f)
	{
		AllowMovementSound = false;
		AngularVelocity = 0.0f;
		MovementAudioPlayer?.Stop();
		if (_shutAudioPlayer == null)
		{
			return;
		}

		_shutAudioPlayer.Stop();
		_shutAudioPlayer.VolumeDb = volumeDb;
		_shutAudioPlayer.Play();
	}

	public override bool UseItem(Variant itemData)
	{
		GodotObject obj = itemData.AsGodotObject();
		if (obj == null)
		{
			return false;
		}

		string itemName = obj.Get("item_name").AsString();
		if (itemName == UnlockKeyName)
		{
			IsLocked = false;
			return true;
		}

		return false;
	}
}
