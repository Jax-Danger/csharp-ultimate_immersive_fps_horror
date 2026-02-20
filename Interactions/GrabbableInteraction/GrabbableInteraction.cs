using Godot;
using System.Threading.Tasks;

namespace Game.Interactions;

[GlobalClass]
public partial class GrabbableInteraction : AbstractInteraction
{
	[Export]
	public AudioStream CollisionSoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/impactPlank_medium_003.ogg");

	private AudioStreamPlayer3D _collisionAudioPlayer;
	private Marker3D _playerHand;
	private Vector3 _lastVelocity = Vector3.Zero;
	private float _contactVelocityThreshold = 1.0f;

	public override void _Ready()
	{
		base._Ready();

		_collisionAudioPlayer = new AudioStreamPlayer3D
		{
			Stream = CollisionSoundEffect
		};
		AddChild(_collisionAudioPlayer);

		if (ObjectRef is RigidBody3D rigidBody)
		{
			rigidBody.BodyEntered += FireCollision;
			rigidBody.ContactMonitor = true;
			rigidBody.MaxContactsReported = 1;
		}
	}

	public override void Interact()
	{
		base.Interact();
		if (!CanInteract || _playerHand == null)
		{
			return;
		}

		if (ObjectRef is RigidBody3D rigidBody)
		{
			rigidBody.LinearVelocity = CalculateObjectDistance() * (5f / rigidBody.Mass);
		}
	}

	public override async void AuxInteract()
	{
		base.AuxInteract();
		if (!CanInteract || _playerHand == null)
		{
			return;
		}

		if (ObjectRef is not RigidBody3D rigidBody)
		{
			return;
		}

		Vector3 throwDirection = -_playerHand.GlobalTransform.Basis.Z.Normalized();
		float throwStrength = 20.0f / rigidBody.Mass;
		rigidBody.LinearVelocity = throwDirection * throwStrength;

		CanInteract = false;
		await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
		CanInteract = true;
	}

	public override void _PhysicsProcess(double delta)
	{
		base._PhysicsProcess(delta);
		if (ObjectRef is RigidBody3D rigidBody)
		{
			_lastVelocity = rigidBody.LinearVelocity;
		}
	}

	public void SetPlayerHandPosition(Marker3D hand)
	{
		_playerHand = hand;
	}

	private void FireCollision(Node node)
	{
		if (ObjectRef is not RigidBody3D rigidBody)
		{
			return;
		}

		float impactStrength = (_lastVelocity - rigidBody.LinearVelocity).Length();
		if (impactStrength > _contactVelocityThreshold)
		{
			_ = PlayCollisionSoundEffect();
		}
	}

	private async Task PlayCollisionSoundEffect()
	{
		if (_collisionAudioPlayer == null)
		{
			return;
		}

		_collisionAudioPlayer.Play();
		await ToSignal(_collisionAudioPlayer, AudioStreamPlayer3D.SignalName.Finished);
	}

	private Vector3 CalculateObjectDistance()
	{
		return _playerHand.GlobalTransform.Origin - ObjectRef.GlobalTransform.Origin;
	}
}
