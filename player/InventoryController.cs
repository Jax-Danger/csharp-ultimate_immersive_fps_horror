using Godot;
using Game.Interactions;

namespace Game.Player;

public partial class InventoryController : Control
{
	private Camera3D PlayerCamera => GetNode<Camera3D>("../../../Head/Eyes/Camera3D");
	private InteractionController InteractionController => GetNode<InteractionController>("../../../InteractionController");
	private Node SanityController => GetNode<Node>("../../../SanityController");
	private GridContainer InventoryGrid => GetNode<GridContainer>("%GridContainer");

	public PopupMenu ContextMenu { get; private set; }
	public bool InventoryFull { get; private set; }

	private const int ItemSlotsCount = 20;
	private const int ConsumableType = 0;
	private const int EquippableType = 1;
	private const int InspectableType = 2;

	private readonly PackedScene inventorySlotPrefab = GD.Load<PackedScene>("res://inventory/inventory_slot.tscn");
	private readonly AudioStream swapSlotSoundEffect = GD.Load<AudioStream>("res://assets/sound_effects/menu_swap.ogg");

	private readonly Godot.Collections.Array<Node> inventorySlots = new();
	private AudioStreamPlayer swapSlotPlayer;

	public override void _Ready()
	{
		swapSlotPlayer = new AudioStreamPlayer { VolumeDb = -12.0f, Stream = swapSlotSoundEffect };
		AddChild(swapSlotPlayer);

		for (int i = 0; i < ItemSlotsCount; i++)
		{
			Node slot = inventorySlotPrefab.Instantiate();
			InventoryGrid.AddChild(slot);
			slot.Set("inventory_slot_id", i);
			slot.Connect("on_item_swapped", Callable.From<int, int>(OnItemSwappedOnSlot));
			slot.Connect("on_item_double_clicked", Callable.From<int>(OnItemDoubleClicked));
			slot.Connect("on_item_right_clicked", Callable.From<int>(OnSlotRightClick));
			inventorySlots.Add(slot);
		}

		ContextMenu = new PopupMenu();
		AddChild(ContextMenu);
		ContextMenu.IdPressed += OnContextMenuSelected;
	}

	public bool HasFreeSlot()
	{
		foreach (Node slot in inventorySlots)
		{
			if (IsSlotEmpty(slot))
			{
				return true;
			}
		}
		return false;
	}

	public void PickupItem(Variant itemData)
	{
		foreach (Node slot in inventorySlots)
		{
			if (slot.Get("slot_filled").AsBool())
			{
				continue;
			}

			SetSlotData(slot, itemData);
			InventoryFull = !HasFreeSlot();
			return;
		}

		InventoryFull = true;
	}

	public override bool _CanDropData(Vector2 atPosition, Variant data) => true;

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		DropCollectable(data.AsInt32());
		InventoryFull = false;
	}

	private void OnItemSwappedOnSlot(int fromSlotId, int toSlotId)
	{
		Node from = inventorySlots[fromSlotId];
		Node to = inventorySlots[toSlotId];
		Variant fromData = GetSlotData(from);
		Variant toData = GetSlotData(to);
		SetSlotData(to, fromData);
		SetSlotData(from, toData);
		swapSlotPlayer.Play();
	}

	private void OnItemDoubleClicked(int slotId)
	{
		Node slot = inventorySlots[slotId];
		if (IsSlotEmpty(slot))
		{
			return;
		}

		switch (GetItemActionType(GetSlotData(slot)))
		{
			case ConsumableType:
				UseCollectable(slotId);
				break;
			case EquippableType:
				EquipCollectable(slotId);
				break;
			case InspectableType:
				ViewInspectable(slotId);
				break;
		}
	}

	private void OnSlotRightClick(int slotId)
	{
		Node slot = inventorySlots[slotId];
		if (IsSlotEmpty(slot))
		{
			return;
		}

		ContextMenu.Clear();
		switch (GetItemActionType(GetSlotData(slot)))
		{
			case ConsumableType:
				ContextMenu.AddItem("Use", 0);
				ContextMenu.AddItem("Drop", 1);
				break;
			case EquippableType:
				ContextMenu.AddItem("Equip", 0);
				ContextMenu.AddItem("Drop", 1);
				break;
			case InspectableType:
				ContextMenu.AddItem("View", 0);
				ContextMenu.AddItem("Drop", 1);
				break;
		}

		ContextMenu.SetMeta("slot_id", slotId);
		Vector2 mousePos = GetViewport().GetMousePosition();
		ContextMenu.Popup(new Rect2I(new Vector2I((int)mousePos.X, (int)mousePos.Y), new Vector2I(1, 1)));
	}

	private void OnContextMenuSelected(long choice)
	{
		int slotId = ContextMenu.GetMeta("slot_id").AsInt32();
		Node slot = inventorySlots[slotId];
		if (IsSlotEmpty(slot))
		{
			return;
		}

		switch (GetItemActionType(GetSlotData(slot)))
		{
			case ConsumableType:
				if (choice == 0) UseCollectable(slotId);
				if (choice == 1) DropCollectable(slotId);
				break;
			case EquippableType:
				if (choice == 0) EquipCollectable(slotId);
				if (choice == 1) DropCollectable(slotId);
				break;
			case InspectableType:
				if (choice == 0) ViewInspectable(slotId);
				if (choice == 1) DropCollectable(slotId);
				break;
		}
	}

	private void UseCollectable(int slotId)
	{
		Node slot = inventorySlots[slotId];
		Variant itemData = GetSlotData(slot);
		if (itemData.VariantType == Variant.Type.Nil)
		{
			return;
		}

		GodotObject actionData = itemData.AsGodotObject()?.Get("action_data").AsGodotObject();
		if (actionData?.Get("modifier_name").AsString() == "sanity")
		{
			SanityController.Call("add_sanity", actionData.Get("modifier_value").AsSingle());
		}

		InventoryFull = false;
		SetSlotData(slot, Variant.CreateFrom((GodotObject)null));
	}

	public void DropCollectable(int slotId)
	{
		Node slot = inventorySlots[slotId];
		PackedScene prefab = GetPrefabFromSlot(slot);
		if (prefab == null)
		{
			return;
		}

		PhysicsBody3D instance = prefab.Instantiate<PhysicsBody3D>();
		GetTree().CurrentScene.AddChild(instance);
		if (!TryGetDropPosition(out Vector3 groundPos))
		{
			instance.QueueFree();
			return;
		}

		PlaceDroppedInstance(instance, groundPos);
		swapSlotPlayer.Play();
		InventoryFull = false;
		SetSlotData(slot, Variant.CreateFrom((GodotObject)null));
	}

	private void EquipCollectable(int slotId)
	{
		if (InteractionController.ItemEquipped)
		{
			return;
		}

		Node slot = inventorySlots[slotId];
		PackedScene prefab = GetPrefabFromSlot(slot);
		if (prefab == null)
		{
			return;
		}

		InteractionController.OnItemEquipped(prefab.Instantiate<PhysicsBody3D>());
		InventoryFull = false;
		SetSlotData(slot, Variant.CreateFrom((GodotObject)null));
	}

	private void ViewInspectable(int slotId)
	{
		Node slot = inventorySlots[slotId];
		PackedScene prefab = GetPrefabFromSlot(slot);
		if (prefab == null)
		{
			return;
		}

		InteractionController.OnNoteInspected(prefab.Instantiate<PhysicsBody3D>());
		InventoryFull = false;
		SetSlotData(slot, Variant.CreateFrom((GodotObject)null));
	}

	private bool TryGetDropPosition(out Vector3 groundPos)
	{
		groundPos = Vector3.Zero;
		PhysicsDirectSpaceState3D spaceState = PlayerCamera.GetWorld3D().DirectSpaceState;
		Vector3 origin = PlayerCamera.GlobalTransform.Origin;
		Vector3 targetPos = origin + (-PlayerCamera.GlobalTransform.Basis.Z.Normalized() * 2.0f);

		if (spaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(origin, targetPos)).Count > 0)
		{
			GD.Print("Cannot drop: path blocked");
			InteractionController.InteractFailurePlayer.Play();
			return false;
		}

		Godot.Collections.Dictionary groundHit = spaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(targetPos + Vector3.Up * 2.0f, targetPos - Vector3.Up * 5.0f));
		if (groundHit.Count == 0)
		{
			GD.Print("Cannot drop: no ground");
			return false;
		}

		groundPos = (Vector3)groundHit["position"];
		return true;
	}

	private static void PlaceDroppedInstance(PhysicsBody3D instance, Vector3 groundPos)
	{
		if (instance is RigidBody3D rigidBody)
		{
			rigidBody.GlobalTransform = new Transform3D(rigidBody.GlobalTransform.Basis, groundPos + Vector3.Up * 0.7f);
			rigidBody.Freeze = false;
			rigidBody.GravityScale = 1.0f;
			rigidBody.RotationDegrees = new Vector3((float)GD.RandRange(0, 360), rigidBody.RotationDegrees.Y, (float)GD.RandRange(0, 360));
		}
		else
		{
			instance.GlobalTransform = new Transform3D(instance.GlobalTransform.Basis, groundPos + Vector3.Up * 0.0001f);
		}

		instance.RotationDegrees = new Vector3(instance.RotationDegrees.X, (float)GD.RandRange(0, 360), instance.RotationDegrees.Z);
	}

	private static bool IsSlotEmpty(Node slot) => slot.Get("slot_data").VariantType == Variant.Type.Nil;
	private static Variant GetSlotData(Node slot) => slot.Get("slot_data");
	private static void SetSlotData(Node slot, Variant value) => slot.Call("fill_slot", value);

	private static PackedScene GetPrefabFromSlot(Node slot)
	{
		Variant itemData = GetSlotData(slot);
		if (itemData.VariantType == Variant.Type.Nil)
		{
			return null;
		}

		return itemData.AsGodotObject()?.Get("item_model_prefab").AsGodotObject() as PackedScene;
	}

	private static int GetItemActionType(Variant itemData)
	{
		GodotObject item = itemData.AsGodotObject();
		GodotObject actionData = item?.Get("action_data").AsGodotObject();
		return actionData?.Get("action_type").AsInt32() ?? -1;
	}
}
