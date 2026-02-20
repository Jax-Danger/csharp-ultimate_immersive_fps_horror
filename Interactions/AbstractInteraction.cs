using Godot;

namespace Game.Interactions;

[GlobalClass]
public partial class AbstractInteraction : Node
{
	/// <summary>
	/// Nodes affected by this interaction. If a node has an "execute" method,
	/// it will be called by <see cref="NotifyNodes(float)"/>.
	/// </summary>
	[Export]
	public Godot.Collections.Array<Node> NodesToAffect { get; set; } = new();

	/// <summary>
	/// Reference to the interactable node, usually the parent physics/static body.
	/// </summary>
	public Node3D ObjectRef { get; private set; }

	/// <summary>
	/// True when this object can be interacted with on the current frame.
	/// </summary>
	public bool CanInteract { get; set; } = true;

	/// <summary>
	/// True while the player is currently interacting with this object.
	/// </summary>
	public bool IsInteracting { get; private set; }

	/// <summary>
	/// True when camera movement should be locked during interaction.
	/// </summary>
	public bool LockCamera { get; protected set; }

	public override void _Ready()
	{
		ObjectRef = GetParentOrNull<Node3D>();
	}

	/// <summary>
	/// Runs once when interaction begins.
	/// </summary>
	public virtual void PreInteract()
	{
		IsInteracting = true;
	}

	/// <summary>
	/// Runs every frame while interacting.
	/// </summary>
	public virtual void Interact()
	{
	}

	/// <summary>
	/// Alternate interaction (e.g., secondary input).
	/// </summary>
	public virtual void AuxInteract()
	{
	}

	/// <summary>
	/// Runs once when interaction ends.
	/// </summary>
	public virtual void PostInteract()
	{
		IsInteracting = false;
		LockCamera = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	/// <summary>
	/// Calls "execute(percentage)" on each affected node that implements it.
	/// </summary>
	public void NotifyNodes(float percentage)
	{
		foreach (Node node in NodesToAffect)
		{
			if (node != null && node.HasMethod("execute"))
			{
				node.Call("execute", percentage);
			}
		}
	}

	/// <summary>
	/// Returns true if the item was used successfully. Override in child classes.
	/// </summary>
	public virtual bool UseItem(Variant itemData)
	{
		return false;
	}
}
