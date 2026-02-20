using Godot;

namespace Game.Interactions;

[GlobalClass]
public partial class EquippableInteraction : CollectableInteraction
{
	[Signal]
	public delegate void ItemCollectedEventHandler(Node item);

	public override void _Ready()
	{
		base._Ready();
		CollectSoundEffect = GD.Load<AudioStream>("res://assets/sound_effects/handleCoins2.ogg");
		CollectMeshAndCollisionNodes();
	}

	public override void Interact()
	{
		base.Interact();
		if (!CanInteract)
		{
			return;
		}

		EmitSignal(SignalName.ItemCollected, GetParent());
		CanInteract = false;
	}
}
