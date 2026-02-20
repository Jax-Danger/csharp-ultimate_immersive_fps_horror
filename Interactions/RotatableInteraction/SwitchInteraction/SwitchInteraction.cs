using Godot;
namespace Game.Interactions;

[GlobalClass]
public partial class SwitchInteraction : RotatableInteraction
{
	[Export]
	public AudioStream SnapSoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/lever_snap.ogg");

	private AudioStreamPlayer3D _snapAudioPlayer;
	private bool _isSwitchSnapping = false;
	private bool _switchMoved = false;
	private bool _switchKickbackTriggered = false;
	private float _switchTargetRotation = 0.0f;

	public override void _EnterTree()
	{
		base._EnterTree();
		MovementSound = GD.Load<AudioStream>("res://assets/sound_effects/lever_pull.ogg");
	}

	public override void _Ready()
	{
		base._Ready();
		if (ObjectRef != null)
		{
			StartingRotation = ObjectRef.Rotation.Z;
			CurrentAngle = StartingRotation;
			PreviousAngle = StartingRotation;
		}

		_snapAudioPlayer = new AudioStreamPlayer3D { Stream = SnapSoundEffect };
		AddChild(_snapAudioPlayer);

		CreakVelocityThreshold = 0.0001f;
		FadeSpeed = 50.0f;
		VolumeScale = 1000.0f;
		SmoothingCoefficient = 8.0f;
	}

	public override void PreInteract()
	{
		base.PreInteract();
		_switchMoved = false;
	}

	public override void PostInteract()
	{
		base.PostInteract();
		float percent = GetRotationPercentage();
		if (percent < 0.3f)
		{
			_switchTargetRotation = StartingRotation;
			_isSwitchSnapping = true;
		}
		else if (percent > 0.7f)
		{
			_switchTargetRotation = MaximumRotation;
			_isSwitchSnapping = true;
		}
	}

	public override void _Process(double deltaRaw)
	{
		base._Process(deltaRaw);
		if (ObjectRef == null)
		{
			return;
		}

		float delta = (float)deltaRaw;
		PreviousAngle = CurrentAngle;
		AllowMovementSound = true;

		if (IsInteracting)
		{
			PlayMovementSounds(delta);
			if (_switchMoved)
			{
				if (Mathf.Abs(ObjectRef.Rotation.Z - MaximumRotation) < 0.01f || Mathf.Abs(ObjectRef.Rotation.Z - StartingRotation) < 0.01f)
				{
					PlaySnapSound(delta);
					_switchMoved = false;
				}
			}
		}
		else
		{
			StopMovementSounds(delta);
		}

		if (_isSwitchSnapping)
		{
			if (!_switchKickbackTriggered)
			{
				_switchKickbackTriggered = true;
				if (_snapAudioPlayer != null && !_snapAudioPlayer.Playing)
				{
					_snapAudioPlayer.Stop();
					_snapAudioPlayer.VolumeDb = 0.0f;
					_snapAudioPlayer.Play();
				}
			}

			Vector3 rot = ObjectRef.Rotation;
			rot.Z = Mathf.Lerp(rot.Z, _switchTargetRotation, delta * SmoothingCoefficient);
			ObjectRef.Rotation = rot;

			if (Mathf.Abs(ObjectRef.Rotation.Z - _switchTargetRotation) < 0.01f)
			{
				rot = ObjectRef.Rotation;
				rot.Z = _switchTargetRotation;
				ObjectRef.Rotation = rot;
				_isSwitchSnapping = false;
			}

			float percentage = (ObjectRef.Rotation.Z - StartingRotation) / (MaximumRotation - StartingRotation);
			NotifyNodes(percentage);
		}
		else
		{
			_switchKickbackTriggered = false;
		}

		CurrentAngle = ObjectRef.Rotation.Z;
		AngularVelocity = CurrentAngle - PreviousAngle;
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if (!IsInteracting || @event is not InputEventMouseMotion mouse || ObjectRef == null)
		{
			return;
		}

		float prevAngle = ObjectRef.Rotation.Z;
		ObjectRef.RotateZ(mouse.Relative.Y * 0.001f);

		float minRot = Mathf.Min(StartingRotation, MaximumRotation);
		float maxRot = Mathf.Max(StartingRotation, MaximumRotation);
		Vector3 rot = ObjectRef.Rotation;
		rot.Z = Mathf.Clamp(rot.Z, minRot, maxRot);
		ObjectRef.Rotation = rot;

		float percentage = (ObjectRef.Rotation.Z - StartingRotation) / (MaximumRotation - StartingRotation);
		if (Mathf.Abs(ObjectRef.Rotation.Z - prevAngle) > 0.01f)
		{
			_switchMoved = true;
		}

		NotifyNodes(percentage);
	}

	private void PlaySnapSound(float delta)
	{
		if (_snapAudioPlayer == null)
		{
			return;
		}

		_snapAudioPlayer.VolumeDb = 0.0f;
		_snapAudioPlayer.Play();
	}
}
