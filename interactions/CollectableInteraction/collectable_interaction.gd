class_name CollectableInteraction
extends AbstractInteraction

"""
CollectableInteraction is a base interaction component meant for objects
that can be picked up and stored in the player's inventory.  

This includes items like notes, consumables, keys, or any other
pickupable object in the world.  

It provides a place to store relevant action data (e.g., type, 
one-time use, modifiers) and can be attached to the item prefab
to define how the inventory should handle it when collected.
"""

## Sound effect to play when the player collects this item
@export var collect_sound_effect: AudioStreamOggVorbis

## Define the ItemData for when this item enters the player inventory
@export var item_data: ItemData

## Array of all the MeshInstance3D nodes that make up this item
var meshes: Array[MeshInstance3D] = []
## Array of all the CollisionShape3D nodes that make up this item
var collision_shapes: Array[CollisionShape3D] = []

## Runs once, after the node and all its children have entered the scene tree and are ready
func _ready() -> void:
	super()
	
	# Cache the scene path for the item model's prefab so it can be reused when it leaves the inventory
	var scene_path: String = get_parent().scene_file_path
	item_data.item_model_prefab = load(scene_path)

## Runs once, when the player FIRST clicks on an object to interact with
func pre_interact() -> void:
	super()
	
## Run every frame while the player is interacting with this object
func interact() -> void:
	super()
	
## Alternate interaction using secondary button
func aux_interact() -> void:
	super()
	
## Runs once, when the player LAST interacts with an object
func post_interact() -> void:
	super()
	
## Collects all mesh and collision shape nodes to be modified/removed for interaction
func _collect_mesh_and_collision_nodes() -> void:
	# Find all MeshInstance3D nodes under the parent (recursively)
	meshes = get_mesh_instance_children_recursive(get_parent())
	
	# Find all CollisionShape3D nodes under the parent (recursively)
	collision_shapes = get_collision_shape_children_recursive(get_parent())
	
## Recursively find all child MeshInstance3Ds
func get_mesh_instance_children_recursive(parent: Node) -> Array:
	var result: Array[MeshInstance3D] = []
	
	# Check if this node is a collision shape 3D
	if parent is MeshInstance3D:
		result.append(parent)
		
	# Recursively check all children
	for child in parent.get_children():
		result += get_mesh_instance_children_recursive(child)
	
	return result
	
## Recursively find all child CollisionShape3Ds
func get_collision_shape_children_recursive(parent: Node) -> Array:
	var result: Array[CollisionShape3D] = []
	
	# Check if this node is a collision shape 3D
	if parent is CollisionShape3D:
		result.append(parent)
		
	# Recursively check all children
	for child in parent.get_children():
		result += get_collision_shape_children_recursive(child)
	
	return result
