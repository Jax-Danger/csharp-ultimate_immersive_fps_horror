using Godot;

namespace Game.Interactions;

[GlobalClass]
public partial class ConsumableInteraction : CollectableInteraction
{
	[Signal]
	public delegate void ItemCollectedEventHandler(Node item);

	public override void _Ready()
	{
		base._Ready();
		CollectSoundEffect = GD.Load<AudioStream>("res://assets/sound_effects/handleCoins2.ogg");
	}

	public override void Interact()
	{
		base.Interact();
		if (!CanInteract)
		{
			return;
		}

		Node parent = GetParent();
		if (parent == null)
		{
			return;
		}

		CanInteract = false;
		EmitSignal(SignalName.ItemCollected, parent);
	}
}
