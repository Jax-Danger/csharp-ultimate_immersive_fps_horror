using Godot;

namespace Game.Interactions;

[GlobalClass]
public partial class InspectableInteraction : CollectableInteraction
{
	[Export]
	public string Content { get; set; } = string.Empty;

	[Export]
	public AudioStream PutAwaySoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/drawKnife3.ogg");

	[Signal]
	public delegate void NoteInspectedEventHandler(Node3D note);

	public override void _Ready()
	{
		base._Ready();
		CollectSoundEffect = GD.Load<AudioStream>("res://assets/sound_effects/drawKnife2.ogg");
		Content = Content.Replace("\\n", "\n");
		CollectMeshAndCollisionNodes();
	}

	public override void Interact()
	{
		base.Interact();
		if (!CanInteract)
		{
			return;
		}

		Node3D parent = GetParentOrNull<Node3D>();
		if (parent != null)
		{
			EmitSignal(SignalName.NoteInspected, parent);
		}

		CanInteract = false;
	}
}
