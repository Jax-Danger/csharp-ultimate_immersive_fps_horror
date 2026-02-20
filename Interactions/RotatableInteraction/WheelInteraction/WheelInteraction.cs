using Godot;

namespace Game.Interactions;

[GlobalClass]
public partial class WheelInteraction : RotatableInteraction
{
	[Export]
	public AudioStream KickbackSoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/wheel_kickback.ogg");

	[Export]
	public float WheelKickIntensity { get; set; } = 0.05f;

	private AudioStreamPlayer3D _kickbackAudioPlayer;
	private bool _wheelKickbackTriggered = false;
	private float _wheelKickback = 0.0f;
	private Camera3D _camera;
	private Vector2 _previousMousePosition = Vector2.Zero;

	public override void _EnterTree()
	{
		base._EnterTree();
		MovementSound = GD.Load<AudioStream>("res://assets/sound_effects/wheel_spin.ogg");
	}

	public override void _Ready()
	{
		base._Ready();
		if (ObjectRef != null)
		{
			StartingRotation = ObjectRef.Rotation.Z;
			CurrentAngle = StartingRotation / 0.1f;
			PreviousAngle = CurrentAngle;
		}

		_camera = GetTree().CurrentScene?.FindChild("Camera3D", true, false) as Camera3D;

		_kickbackAudioPlayer = new AudioStreamPlayer3D { Stream = KickbackSoundEffect };
		AddChild(_kickbackAudioPlayer);

		CreakVelocityThreshold = 0.0001f;
		FadeSpeed = 5.0f;
		VolumeScale = 1000.0f;
		SmoothingCoefficient = 8.0f;
	}

	public override void PreInteract()
	{
		base.PreInteract();
		_previousMousePosition = GetViewport().GetMousePosition();
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void PostInteract()
	{
		base.PostInteract();
		_wheelKickback = -WheelKickIntensity;
	}

	public override void _Process(double deltaRaw)
	{
		base._Process(deltaRaw);
		if (ObjectRef == null)
		{
			return;
		}

		float delta = (float)deltaRaw;
		AllowMovementSound = true;

		if (IsInteracting)
		{
			PlayMovementSounds(delta);
		}
		else
		{
			StopMovementSounds(delta);
		}

		if (Mathf.Abs(_wheelKickback) > 0.0001f)
		{
			CurrentAngle += _wheelKickback;
			_wheelKickback = Mathf.Lerp(_wheelKickback, 0.0f, delta * 6.0f);

			float minWheelRotation = Mathf.Min(StartingRotation, MaximumRotation) / 0.1f;
			float maxWheelRotation = Mathf.Max(StartingRotation, MaximumRotation) / 0.1f;
			CurrentAngle = Mathf.Clamp(CurrentAngle, minWheelRotation, maxWheelRotation);
			AngularVelocity = CurrentAngle - PreviousAngle;

			Vector3 rot = ObjectRef.Rotation;
			rot.Z = CurrentAngle * 0.1f;
			ObjectRef.Rotation = rot;

			float percentage = GetRotationPercentage();
			NotifyNodes(percentage);

			if (!IsInteracting && !_wheelKickbackTriggered && Mathf.Abs(_wheelKickback) > 0.01f)
			{
				_wheelKickbackTriggered = true;
				_kickbackAudioPlayer?.Stop();
				if (_kickbackAudioPlayer != null)
				{
					_kickbackAudioPlayer.VolumeDb = 0.0f;
					_kickbackAudioPlayer.Play();
				}
			}
		}
		else
		{
			_wheelKickbackTriggered = false;
		}

		AngularVelocity = CurrentAngle - PreviousAngle;
		PreviousAngle = CurrentAngle;
	}

	public override void _Input(InputEvent @event)
	{
		base._Input(@event);
		if (!IsInteracting || @event is not InputEventMouseMotion mouse || ObjectRef == null || _camera == null)
		{
			return;
		}

		Vector2 mousePosition = mouse.Position;
		if (CalculateCrossProduct(mousePosition) > 0.0f)
		{
			CurrentAngle += 0.1f;
		}
		else
		{
			CurrentAngle -= 0.1f;
		}

		float minWheelRotation = Mathf.Min(StartingRotation, MaximumRotation) / 0.1f;
		float maxWheelRotation = Mathf.Max(StartingRotation, MaximumRotation) / 0.1f;
		CurrentAngle = Mathf.Clamp(CurrentAngle, minWheelRotation, maxWheelRotation);

		Vector3 rot = ObjectRef.Rotation;
		rot.Z = CurrentAngle * 0.1f;
		ObjectRef.Rotation = rot;

		float percentage = GetRotationPercentage();
		_previousMousePosition = mousePosition;
		NotifyNodes(percentage);
	}

	private float CalculateCrossProduct(Vector2 mousePosition)
	{
		Vector2 centerPosition = _camera.UnprojectPosition(ObjectRef.GlobalTransform.Origin);
		Vector2 vectorToPrevious = _previousMousePosition - centerPosition;
		Vector2 vectorToCurrent = mousePosition - centerPosition;
		return vectorToCurrent.X * vectorToPrevious.Y - vectorToCurrent.Y * vectorToPrevious.X;
	}
}
