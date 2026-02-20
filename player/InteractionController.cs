using Godot;
using System.Threading.Tasks;
using Game.Interactions;

namespace Game.Player;

public partial class InteractionController : Node
{
	private RayCast3D InteractionRaycast => GetNode<RayCast3D>("%InteractionRaycast");
	private Camera3D PlayerCamera => GetNode<Camera3D>("%Camera3D");
	private Marker3D Hand => GetNode<Marker3D>("%Hand");
	private Marker3D NoteHand => GetNode<Marker3D>("%NoteHand");
	private Marker3D ItemHand => GetNode<Marker3D>("%ItemHand");
	private Area3D InteractableCheck => GetNode<Area3D>("../InteractableCheck");
	private Control NoteOverlay => GetNode<Control>("%NoteOverlay");
	private RichTextLabel NoteContent => GetNode<RichTextLabel>("%NoteContent");
	private InventoryController InventoryController => GetNode<InventoryController>("%InventoryController/CanvasLayer/InventoryUI");
	private Label InteractionTextbox => GetNode<Label>("%InteractionTextbox");
	private TextureRect DefaultReticle => GetNode<TextureRect>("%DefaultReticle");
	private TextureRect HighlightReticle => GetNode<TextureRect>("%HighlightReticle");
	private TextureRect InteractingReticle => GetNode<TextureRect>("%InteractingReticle");
	private TextureRect UseReticle => GetNode<TextureRect>("%UseReticle");

	private readonly Material outlineMaterial = GD.Load<Material>("res://materials/item_highlighter.tres");
	private readonly AudioStream interactFailureSoundEffect = GD.Load<AudioStream>("res://assets/sound_effects/key_use_failure.wav");
	private readonly AudioStream interactSuccessSoundEffect = GD.Load<AudioStream>("res://assets/sound_effects/key_use_success.ogg");
	private readonly AudioStream equipItemSoundEffect = GD.Load<AudioStream>("res://assets/sound_effects/key_equip.ogg");

	[Signal]
	public delegate void InventOnItemCollectedEventHandler(Variant item);

	public bool ItemEquipped { get; private set; }
	public GodotObject CurrentObject { get; private set; }
	public AbstractInteraction InteractionComponent { get; private set; }
	public AudioStreamPlayer InteractFailurePlayer { get; private set; }

	private GodotObject potentialObject;
	private AbstractInteraction potentialInteractionComponent;
	private Node3D equippedItem;
	private CollectableInteraction equippedItemInteraction;
	private StaticBody3D currentNote;
	private InspectableInteraction noteInteraction;
	private bool isNoteOverlayDisplayed;

	private AudioStreamPlayer interactSuccessPlayer;
	private AudioStreamPlayer equipItemPlayer;

	public override void _Ready()
	{
		InteractableCheck.BodyEntered += OnCollectableEnteredRange;
		InteractableCheck.BodyExited += OnCollectableExitedRange;
		Connect(SignalName.InventOnItemCollected, new Callable(InventoryController, "pickup_item"));

		InteractFailurePlayer = CreateAudioPlayer(interactFailureSoundEffect, -25.0f);
		interactSuccessPlayer = CreateAudioPlayer(interactSuccessSoundEffect, -10.0f);
		equipItemPlayer = CreateAudioPlayer(equipItemSoundEffect, -20.0f);
		AddChild(InteractFailurePlayer);
		AddChild(interactSuccessPlayer);
		AddChild(equipItemPlayer);
	}

	public override void _Process(double delta)
	{
		if (InventoryController.Visible)
		{
			CurrentObject = null;
			UpdateReticleState();
			return;
		}

		if (CurrentObject != null)
		{
			ProcessCurrentInteraction();
			return;
		}

		FindAndStartPotentialInteraction();
	}

	public override void _Input(InputEvent @event)
	{
		if (isNoteOverlayDisplayed && @event.IsActionPressed("primary"))
		{
			CollectCurrentNote();
		}

		if (ItemEquipped && Input.IsActionJustPressed("primary"))
		{
			UseEquippedItem();
		}
	}

	public bool IsCameraLocked() => InteractionComponent?.LockCamera == true && InteractionComponent.IsInteracting;

	public void OnItemEquipped(Node3D item)
	{
		equippedItem = item;
		ItemEquipped = true;
		equippedItemInteraction = FindInteractionComponent(item) as CollectableInteraction;

		if (item is RigidBody3D rigidBody)
		{
			rigidBody.Freeze = true;
			rigidBody.LinearVelocity = Vector3.Zero;
			rigidBody.AngularVelocity = Vector3.Zero;
			rigidBody.GravityScale = 0.0f;
		}

		item.GetParent()?.RemoveChild(item);
		ItemHand.AddChild(item);
		ChangeMeshLayer(CollectMeshesRecursive(item), 2);
		RemoveCollisionShapes(CollectCollisionShapesRecursive(item));

		item.Transform = new Transform3D(item.Transform.Basis, ItemHand.Transform.Origin);
		item.Position = Vector3.Zero;
		item.RotationDegrees = new Vector3(0, 180, -90);
		equipItemPlayer.Play();
	}

	public void OnNoteInspected(Node3D note)
	{
		if (currentNote != null)
		{
			CollectCurrentNote();
		}

		currentNote = note as StaticBody3D;
		noteInteraction = FindInteractionComponent(currentNote) as InspectableInteraction;
		if (currentNote == null || noteInteraction == null)
		{
			return;
		}

		PlaySoundEffect(noteInteraction.CollectSoundEffect);
		currentNote.GetParent()?.RemoveChild(currentNote);
		NoteHand.AddChild(currentNote);
		ChangeMeshLayer(CollectMeshesRecursive(currentNote), 2);
		RemoveCollisionShapes(CollectCollisionShapesRecursive(currentNote));

		currentNote.Transform = new Transform3D(currentNote.Transform.Basis, NoteHand.Transform.Origin);
		currentNote.Position = Vector3.Zero;
		currentNote.RotationDegrees = new Vector3(90, 10, 0);

		NoteOverlay.Visible = true;
		isNoteOverlayDisplayed = true;
		NoteContent.BbcodeEnabled = true;
		NoteContent.Text = noteInteraction.Content;
	}

	public AbstractInteraction FindInteractionComponent(Node node)
	{
		while (node != null)
		{
			foreach (Node child in node.GetChildren())
			{
				if (child is AbstractInteraction interaction)
				{
					return interaction;
				}
			}
			node = node.GetParent();
		}
		return null;
	}

	private void ProcessCurrentInteraction()
	{
		if (InteractionComponent == null || CurrentObject == null)
		{
			EndInteraction();
			return;
		}

		if (!GodotObject.IsInstanceValid(InteractionComponent) || !GodotObject.IsInstanceValid(CurrentObject))
		{
			EndInteraction();
			return;
		}

		if (InteractionComponent.IsInteracting)
		{
			UpdateReticleState();
		}

		if (PlayerCamera.GlobalTransform.Origin.DistanceTo(InteractionRaycast.GetCollisionPoint()) > 5.0f)
		{
			EndInteraction();
			return;
		}

		if (Input.IsActionJustPressed("secondary"))
		{
			InteractionComponent.AuxInteract();
			EndInteraction();
			return;
		}

		if (Input.IsActionPressed("primary"))
		{
			if (InteractionComponent is CollectableInteraction && InventoryController.InventoryFull)
			{
				if (!InteractFailurePlayer.Playing)
				{
					_ = ShowInteractionText("Inventory Full...", 1.0f);
					InteractFailurePlayer.Play();
				}
				return;
			}

			InteractionComponent.Interact();
			if (!GodotObject.IsInstanceValid(InteractionComponent) || !GodotObject.IsInstanceValid(CurrentObject))
			{
				EndInteraction();
			}
			return;
		}

		if (GodotObject.IsInstanceValid(InteractionComponent))
		{
			InteractionComponent.PostInteract();
		}
		EndInteraction();
	}

	private void FindAndStartPotentialInteraction()
	{
		potentialObject = InteractionRaycast.GetCollider();
		if (potentialObject is not Node node)
		{
			UpdateReticleState();
			return;
		}

		potentialInteractionComponent = FindInteractionComponent(node);
		if (potentialInteractionComponent == null)
		{
			CurrentObject = null;
			UpdateReticleState();
			return;
		}

		if (!potentialInteractionComponent.CanInteract)
		{
			return;
		}

		UpdateReticleState();
		if (!Input.IsActionJustPressed("primary"))
		{
			return;
		}

		InteractionComponent = potentialInteractionComponent;
		CurrentObject = potentialObject;

		if (InteractionComponent is TypeableInteraction typeable)
		{
			typeable.SetTargetButton(node as Node3D);
		}

		InteractionComponent.PreInteract();

		if (InteractionComponent is GrabbableInteraction grabbable)
		{
			grabbable.SetPlayerHandPosition(Hand);
		}

		if (InteractionComponent is ConsumableInteraction consumable)
		{
			consumable.ItemCollected -= OnItemCollected;
			consumable.ItemCollected += OnItemCollected;
		}

		if (InteractionComponent is EquippableInteraction equippable)
		{
			equippable.ItemCollected -= OnItemCollected;
			equippable.ItemCollected += OnItemCollected;
		}

		if (InteractionComponent is InspectableInteraction inspectable)
		{
			inspectable.NoteInspected -= OnNoteInspected;
			inspectable.NoteInspected += OnNoteInspected;
		}

		if (InteractionComponent is DoorInteraction door && node is Node3D node3D)
		{
			door.SetDirection(node3D.ToLocal(InteractionRaycast.GetCollisionPoint()));
		}
	}

	private void OnItemCollected(Node item)
	{
		if (item is not Node3D item3D)
		{
			return;
		}

		CollectableInteraction interaction = FindInteractionComponent(item3D) as CollectableInteraction;
		if (interaction == null)
		{
			return;
		}

		AddItemToInventory(interaction.ItemData);
		PlaySoundEffect(interaction.CollectSoundEffect);
		item3D.QueueFree();

		if (ReferenceEquals(CurrentObject, item3D) || !GodotObject.IsInstanceValid(InteractionComponent))
		{
			EndInteraction();
		}
	}

	private void CollectCurrentNote()
	{
		NoteOverlay.Visible = false;
		isNoteOverlayDisplayed = false;

		if (noteInteraction != null)
		{
			AddItemToInventory(noteInteraction.ItemData);
			PlaySoundEffect(noteInteraction.PutAwaySoundEffect);
		}

		currentNote?.QueueFree();
		currentNote = null;
		noteInteraction = null;
	}

	private void UseEquippedItem()
	{
		if (potentialObject == null)
		{
			_ = ShowInteractionText("Nothing to be used on...", 1.0f);
			FailEquippedItemUse();
			return;
		}

		if (potentialInteractionComponent == null || equippedItemInteraction == null)
		{
			_ = ShowInteractionText("Nothing interesting happens...", 1.0f);
			FailEquippedItemUse();
			return;
		}

		if (!potentialInteractionComponent.UseItem(equippedItemInteraction.ItemData))
		{
			_ = ShowInteractionText("Nothing interesting happens...", 1.0f);
			FailEquippedItemUse();
			return;
		}

		GodotObject actionData = equippedItemInteraction.ItemData?.Get("action_data").AsGodotObject();
		if (actionData?.Get("one_time_use").AsBool() == true)
		{
			equippedItem?.QueueFree();
			equippedItem = null;
			ItemEquipped = false;
		}

		string successText = actionData?.Get("success_text").AsString() ?? "Used.";
		_ = ShowInteractionText(successText, 1.0f);
		interactSuccessPlayer.Play();
	}

	private void FailEquippedItemUse()
	{
		InteractFailurePlayer.Play();

		if (equippedItemInteraction?.ItemData != null)
		{
			InventoryController.PickupItem(equippedItemInteraction.ItemData);
		}

		equippedItem?.QueueFree();
		equippedItem = null;
		ItemEquipped = false;
		CurrentObject = null;
		potentialInteractionComponent = null;
	}

	private void EndInteraction()
	{
		CurrentObject = null;
		InteractionComponent = null;
		UpdateReticleState();
	}

	private void AddItemToInventory(Variant itemData)
	{
		if (itemData.VariantType == Variant.Type.Nil)
		{
			GD.Print("Item not found");
			return;
		}

		EmitSignal(SignalName.InventOnItemCollected, itemData);
	}

	private async Task ShowInteractionText(string text, float duration)
	{
		InteractionTextbox.Text = text;
		InteractionTextbox.Visible = true;
		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
		InteractionTextbox.Visible = false;
	}

	private void PlaySoundEffect(AudioStream soundEffect)
	{
		if (soundEffect == null)
		{
			return;
		}

		AudioStreamPlayer player = new() { Stream = soundEffect };
		AddChild(player);
		player.Finished += player.QueueFree;
		player.Play();
	}

	private void OnCollectableEnteredRange(Node3D body)
	{
		if (body.Name == "Player")
		{
			return;
		}

		AbstractInteraction interaction = FindInteractionComponent(body);
		if (interaction is not ConsumableInteraction && interaction is not EquippableInteraction)
		{
			return;
		}

		if (body.FindChild("MeshInstance3D", true, false) is MeshInstance3D mesh)
		{
			mesh.MaterialOverlay = outlineMaterial;
		}
	}

	private void OnCollectableExitedRange(Node3D body)
	{
		if (body.Name == "Player")
		{
			return;
		}

		AbstractInteraction interaction = FindInteractionComponent(body);
		if (interaction is not ConsumableInteraction && interaction is not EquippableInteraction)
		{
			return;
		}

		if (body.FindChild("MeshInstance3D", true, false) is MeshInstance3D mesh)
		{
			mesh.MaterialOverlay = null;
		}
	}

	private void UpdateReticleState()
	{
		DefaultReticle.Visible = false;
		HighlightReticle.Visible = false;
		InteractingReticle.Visible = false;
		UseReticle.Visible = false;

		if (InventoryController.Visible)
		{
			return;
		}

		if (ItemEquipped)
		{
			UseReticle.Visible = true;
			return;
		}

		if (CurrentObject != null && InteractionComponent != null)
		{
			if (InteractionComponent.IsInteracting)
			{
				InteractingReticle.Visible = true;
				return;
			}

			if (InteractionComponent.CanInteract)
			{
				HighlightReticle.Visible = true;
				return;
			}

			DefaultReticle.Visible = true;
			return;
		}

		if (potentialObject != null && potentialInteractionComponent?.CanInteract == true)
		{
			HighlightReticle.Visible = true;
			return;
		}

		DefaultReticle.Visible = true;
	}

	private static AudioStreamPlayer CreateAudioPlayer(AudioStream stream, float volumeDb)
	{
		AudioStreamPlayer player = new() { Stream = stream, VolumeDb = volumeDb };
		return player;
	}

	private static Godot.Collections.Array<MeshInstance3D> CollectMeshesRecursive(Node node)
	{
		Godot.Collections.Array<MeshInstance3D> result = new();
		if (node is MeshInstance3D mesh)
		{
			result.Add(mesh);
		}

		foreach (Node child in node.GetChildren())
		{
			foreach (MeshInstance3D childMesh in CollectMeshesRecursive(child))
			{
				result.Add(childMesh);
			}
		}
		return result;
	}

	private static Godot.Collections.Array<CollisionShape3D> CollectCollisionShapesRecursive(Node node)
	{
		Godot.Collections.Array<CollisionShape3D> result = new();
		if (node is CollisionShape3D shape)
		{
			result.Add(shape);
		}

		foreach (Node child in node.GetChildren())
		{
			foreach (CollisionShape3D childShape in CollectCollisionShapesRecursive(child))
			{
				result.Add(childShape);
			}
		}
		return result;
	}

	private static void ChangeMeshLayer(Godot.Collections.Array<MeshInstance3D> meshes, uint layer)
	{
		foreach (MeshInstance3D mesh in meshes)
		{
			mesh.Layers = layer;
		}
	}

	private static void RemoveCollisionShapes(Godot.Collections.Array<CollisionShape3D> collisionShapes)
	{
		foreach (CollisionShape3D shape in collisionShapes)
		{
			shape.QueueFree();
		}
	}
}
