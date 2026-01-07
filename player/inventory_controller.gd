extends Control
class_name InventoryController

"""
High level controller for inventory interactions.
"""

## Reference to the player camera. Used to drop items back into the world
@onready var player_camera: Camera3D = $"../../../Head/Eyes/Camera3D"
## Reference to the interaction controller. Used to interact with objects in the world
@onready var interaction_controller: Node = $"../../../InteractionController"
## Refernce to the sanity controller. Used for items that affect the player's sanity
@onready var sanity_controller: Node = $"../../../SanityController"
## Context menu that displays when the player right clicks on an inventroy item (prompts use/equip/view/drop)
@onready var context_menu: PopupMenu = PopupMenu.new()
## Reference to the grid of inventory slots in the inventory panel
@onready var inventory_grid: GridContainer = %GridContainer
## Number of slots in the inventory
var item_slots_count:int = 20
## Prefab of the inventory slot. Used to populate the inventory
var inventory_slot_prefab: PackedScene = load("res://inventory/inventory_slot.tscn")
## Sound effect and player to play when switching items between inventory slots
var swap_slot_player: AudioStreamPlayer
var swap_slot_sound_effect: AudioStreamOggVorbis = load("res://assets/sound_effects/menu_swap.ogg")
## Private array of inventory slots used as the internal view of the inventory
var inventory_slots: Array[InventorySlot] = []
## True if all inventory slots are filled, false otherwise
var inventory_full: bool = false 

## Runs once, after the node and all its children have entered the scene tree and are ready
func _ready() -> void:
	swap_slot_player = AudioStreamPlayer.new()
	swap_slot_player.volume_db = -12.0
	swap_slot_player.stream = swap_slot_sound_effect
	add_child(swap_slot_player)

	# Populate the inventory with inventory slots. Attach all necessary signals
	for item_slot_index: int in item_slots_count:
		var slot: InventorySlot = inventory_slot_prefab.instantiate()
		inventory_grid.add_child(slot)
		slot.inventory_slot_id = item_slot_index
		slot.on_item_swapped.connect(_on_item_swapped_on_slot)
		slot.on_item_double_clicked.connect(_on_item_double_clicked)
		slot.on_item_right_clicked.connect(_on_slot_right_click)
		inventory_slots.append(slot)

	# Initialize the context menu for right clicks
	add_child(context_menu)
	context_menu.connect("id_pressed", Callable(self, "_on_context_menu_selected"))


## Helper method that returns true if there is any free inventory slots. False if the inventory is full
func has_free_slot() -> bool:
	for slot in inventory_slots:
		if slot.slot_data == null:
			return true
	return false
	
## Places and item into the player inventory
func pickup_item(item_data: ItemData) -> void:
	for slot in inventory_slots:
		if not slot.slot_filled:
			slot.fill_slot(item_data)
			inventory_full = not has_free_slot()
			return
	inventory_full = true

## Switches the place of two items in the inventory
func _on_item_swapped_on_slot(from_slot_id: int, to_slot_id: int) -> void:
	var to_slot_item = inventory_slots[to_slot_id].slot_data
	var from_slot_item = inventory_slots[from_slot_id].slot_data
	inventory_slots[to_slot_id].fill_slot(from_slot_item)
	inventory_slots[from_slot_id].fill_slot(to_slot_item)
	swap_slot_player.play()

## Returns true if a given item can be dropped from the inventory, false otherwise
func _can_drop_data(_at_position: Vector2, _data: Variant) -> bool:
	# TODO: ALl items can be dropped from the inventory.
	# Perhaps certain quest/story items are not allowed? (Keys, codes, etc...)
	return true

## Remove a given item from the inventory and spawns it back into the world
func _drop_data(_at_position: Vector2, data: Variant) -> void:
	drop_collectable(data)
	inventory_full = false


## Auto runs the "use/equip/view" option for this slot
func _on_item_double_clicked(slot_id: int) -> void:
	var slot: InventorySlot = inventory_slots[slot_id]
	
	# If the slot is empty, dont perform any action
	if slot.slot_data == null:
		return
	
	match _get_item_action_type(slot.slot_data):
		ActionData.ActionType.CONSUMABLE:
			use_collectable(slot_id)
		ActionData.ActionType.EQUIPPABLE:
			equip_collectable(slot_id)
		ActionData.ActionType.INSPECTABLE:
			view_inspectable(slot_id)

## Builds and Displays the context menu options specific to the given item type
func _on_slot_right_click(slot_id: int) -> void:
	var slot: InventorySlot = inventory_slots[slot_id]
	
	# If the slot is empty, dont perform any action
	if slot.slot_data == null:
		return

	context_menu.clear()
	match _get_item_action_type(slot.slot_data):
		ActionData.ActionType.CONSUMABLE:
			context_menu.add_item("Use", 0)
			context_menu.add_item("Drop", 1)
		ActionData.ActionType.EQUIPPABLE:
			context_menu.add_item("Equip", 0)
			context_menu.add_item("Drop", 1)
		ActionData.ActionType.INSPECTABLE:
			context_menu.add_item("View", 0)
			context_menu.add_item("Drop", 1)

	# Put the slot_id in the meta data to be read when the player selects something
	# The context menu doesnt automatically know which inventory slot its been displayed for
	context_menu.set_meta("slot_id", slot_id)
	
	# Show the context menu relative to where the players mouse is
	var mouse_pos: Vector2 = get_viewport().get_mouse_position()
	var rect: Rect2i = Rect2i(mouse_pos.floor(), Vector2i(1, 1))
	context_menu.popup(rect)

## Performs whatever action the player chose from the context menu
func _on_context_menu_selected(context_menu_choice: int) -> void:
	# Read in which inventory slot we are acting on
	var slot_id: int = context_menu.get_meta("slot_id") as int
	var slot: InventorySlot = inventory_slots[slot_id]
	
	# If the slot is empty, dont perform any action
	if slot.slot_data == null:
		return

	match _get_item_action_type(slot.slot_data):
		ActionData.ActionType.CONSUMABLE:
			match context_menu_choice:
				0: use_collectable(slot_id)
				1: drop_collectable(slot_id)
		ActionData.ActionType.EQUIPPABLE:
			match context_menu_choice:
				0: equip_collectable(slot_id)
				1: drop_collectable(slot_id)
		ActionData.ActionType.INSPECTABLE:
			match context_menu_choice:
				0: view_inspectable(slot_id)
				1: drop_collectable(slot_id)


## Use's a given collectable from the inventory. Modifier actions are defind here
func use_collectable(slot_id: int) -> void:
	var slot: InventorySlot = inventory_slots[slot_id]
	var item_data: ItemData = slot.slot_data
	if item_data == null:
		return

	# Cache the item's action data
	var action_data: ActionData = item_data.action_data
	
	# Call the respective controller to handle the modifier's action
	match action_data.modifier_name:
		"sanity":
			sanity_controller.add_sanity(action_data.modifier_value)

	# Collectable has been used, the inventory is no longer full
	inventory_full = false
	# Make the slot empty again
	slot.fill_slot(null)

## Drops the item from the provided slot, assuming it will be placed in a valid position. Otherwise, it remains in the inventory
func drop_collectable(slot_id: int) -> void:
	var slot: InventorySlot = inventory_slots[slot_id]
	var item_data: ItemData = slot.slot_data
	if item_data == null:
		return

	# Create an instance of the item based on its prefab, and add it into the scene tree
	var instance: PhysicsBody3D = item_data.item_model_prefab.instantiate() as PhysicsBody3D
	get_tree().current_scene.add_child(instance)
	
	# Get the space state
	var space_state: PhysicsDirectSpaceState3D  = player_camera.get_world_3d().direct_space_state

	## Determine if there are any obstacles preventing the player from dropping this object
	# Draw a vector from the players face, 2 meters forward
	var drop_distance: float = 2.0
	var forward_dir: Vector3 = -player_camera.global_transform.basis.z.normalized()
	var target_pos: Vector3 = player_camera.global_transform.origin + forward_dir * drop_distance

	# Check if the forward vector collides with anything
	var obstacle_params = PhysicsRayQueryParameters3D.new()
	obstacle_params.from = player_camera.global_transform.origin
	obstacle_params.to = target_pos
	obstacle_params.exclude = [player_camera]

	# If there is an obstacle in the way (i.e. a wall) then do NOT drop the item
	var obstacle_hit: Dictionary = space_state.intersect_ray(obstacle_params)
	if not obstacle_hit.is_empty():
		print("Cannot drop: path blocked")
		interaction_controller.interact_failure_player.play()
		instance.queue_free()
		return

	# Draw a vector from the forward point, down to the ground
	var ground_params = PhysicsRayQueryParameters3D.new()
	ground_params.from = target_pos + Vector3.UP * 2.0
	ground_params.to = target_pos - Vector3.UP * 5.0
	ground_params.exclude = [player_camera]

	# If there is ground in front of the player (i.e. not dropping item off a cliff)
	var ground_hit: Dictionary = space_state.intersect_ray(ground_params)
	if not ground_hit:
		print("Cannot drop: no ground")
		instance.queue_free()
		return

	# Cache the valid place to drop an item
	var ground_pos: Vector3 = ground_hit.position
	
	## DebugDraw3D is a debugging tool to show the vector checks made above.
	# Visualize forward ray (obstacle check)
	DebugDraw3D.draw_line(
		player_camera.global_transform.origin,
		target_pos,
		Color.RED,
		1.5
	)

	# Visualize downward ray (ground check)
	DebugDraw3D.draw_line(
		target_pos + Vector3.UP * 2.0,
		target_pos - Vector3.UP * 5.0,
		Color.GREEN,
		1.5
	)
	
	# Add some height to the object when it is dropped so there is movement to it
	var buffer_height: float = 0.2
	
	# Place instance into the world physically
	if instance is RigidBody3D:
		# If the object is a rigid body, it can move/roll. Apply the buffer height to generate movement on impact
		instance.global_transform.origin = ground_pos + Vector3.UP * buffer_height
		instance.freeze = false
		instance.gravity_scale = 1.0
	else:
		# If the object is a static body, it cant move or roll. Simply place it on the ground, with a small height
		# increase to ensure there is no z-clipping with the floor
		instance.global_transform.origin = ground_pos + Vector3.UP * 0.0001
	
	# Optional: rotate item randomly on Y for variety
	instance.rotation_degrees.y = randf() * 360
	
	# Play a sound effect when droppping items
	swap_slot_player.play()
	
	# Collectable has been used, the inventory is no longer full
	inventory_full = false
	# Make the slot empty again
	slot.fill_slot(null)


## Equips the item from the provided slot into the player's hand
func equip_collectable(slot_id: int) -> void:
	if interaction_controller.item_equipped:
		return
		
	var slot: InventorySlot = inventory_slots[slot_id]
	var item_data: ItemData = slot.slot_data
	if item_data == null:
		return

	# Create an instance of the item based on its prefab and assign it to the player
	var instance: PhysicsBody3D = item_data.item_model_prefab.instantiate() as PhysicsBody3D
	# Equipped Objects are handled by the interaction controller
	interaction_controller.on_item_equipped(instance)
	
	# Collectable has been used, the inventory is no longer full
	inventory_full = false
	# Make the slot empty again
	slot.fill_slot(null)

## Places an instance of the item into the players hand to be inspected
func view_inspectable(slot_id: int) -> void:
	var slot: InventorySlot = inventory_slots[slot_id]
	var item_data: ItemData = slot.slot_data
	if item_data == null:
		return

	# Create an instance of the item based on its prefab and assign it to the player
	var instance: PhysicsBody3D = item_data.item_model_prefab.instantiate() as PhysicsBody3D
	# Inspected Objects are handled by the interaction controller
	interaction_controller.on_note_inspected(instance)
	
	# Collectable has been used, the inventory is no longer full
	inventory_full = false
	# Make the slot empty again
	slot.fill_slot(null)

## Helper method to return what type of action this item is expected to perform
func _get_item_action_type(item_data: ItemData) -> ActionData.ActionType:
	# If the item_data or its prefab are null, return invalid
	if not item_data or not item_data.item_model_prefab:	
		return ActionData.ActionType.INVALID
	
	return item_data.action_data.action_type
