extends Control
class_name InventorySlot

"""
This class holds all necessary logic for the individual inventory slots in the inventory.
"""

## TextureRect that holds the item's icon. Null if no item is in this slot
@onready var icon_slot: TextureRect = %TextureRect

## Unique ID for the inventory slot
var inventory_slot_id: int = -1

## True if this slot if dilled with an item, false otherwise
var slot_filled: bool = false

## ItemData for the inventory slot. Null if no item is in this slot
var slot_data: ItemData = null

## Signal fired when an item is dropped on this slot
signal on_item_swapped(from_slot_id: int, to_slot_id: int)
## Signal fired when this slot is double clicked on
signal on_item_double_clicked(slot_id: int)
##Signal fired when this slot is right clicked on
signal on_item_right_clicked(slot_id: int)

## Fills the inventory slot with the provided item_data
func fill_slot(item_data: ItemData) -> void:
	slot_data = item_data
	if (slot_data != null):
		slot_filled = true
		icon_slot.texture = item_data.item_icon
	else:
		slot_filled = false
		icon_slot.texture = null
		
## Shows a preview image of the item being dragged from this slot if filled, returns false if slot is empty
func _get_drag_data(_at_position: Vector2) -> Variant:
	if (slot_filled):
		var preview: TextureRect = TextureRect.new()
		preview.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		preview.size = icon_slot.size
		preview.pivot_offset = icon_slot.size / 2.0
		preview.texture = icon_slot.texture
		set_drag_preview(preview)
		return inventory_slot_id
	else:
		return false

## True if this data objecy can be dropped onto this inventory slot
func _can_drop_data(_at_position: Vector2, data: Variant) -> bool:
	return typeof(data) == TYPE_INT

## Drops the provided data onto this inventory slot
func _drop_data(_at_position: Vector2, data: Variant) -> void:
	on_item_swapped.emit(data as int, inventory_slot_id)

func _gui_input(event: InputEvent) -> void:
	if not slot_filled:
		return

	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_LEFT and event.double_click:
			on_item_double_clicked.emit(inventory_slot_id)
		elif event.button_index == MOUSE_BUTTON_RIGHT and event.pressed:
			on_item_right_clicked.emit(inventory_slot_id)
