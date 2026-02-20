using Godot;

namespace Game.Interactions;

[GlobalClass]
public partial class CollectableInteraction : AbstractInteraction
{
	[Export]
	public AudioStream collect_sound_effect = GD.Load<AudioStream>("res://assets/sound_effects/handleCoins2.ogg");

	[Export]
	public Resource item_data;

	public AudioStream CollectSoundEffect
	{
		get => collect_sound_effect;
		set => collect_sound_effect = value;
	}

	public Resource ItemData
	{
		get => item_data;
		set => item_data = value;
	}

	protected Godot.Collections.Array<MeshInstance3D> Meshes { get; } = new();
	protected Godot.Collections.Array<CollisionShape3D> CollisionShapes { get; } = new();

	public override void _Ready()
	{
		base._Ready();

		Node parent = GetParent();
		if (parent == null || item_data == null)
		{
			return;
		}

		string scenePath = parent.SceneFilePath;
		if (!string.IsNullOrEmpty(scenePath))
		{
			item_data.Set("item_model_prefab", ResourceLoader.Load(scenePath));
		}
	}

	protected void CollectMeshAndCollisionNodes()
	{
		Node parent = GetParent();
		if (parent == null)
		{
			return;
		}

		Meshes.Clear();
		CollisionShapes.Clear();
		CollectMeshChildrenRecursive(parent, Meshes);
		CollectCollisionChildrenRecursive(parent, CollisionShapes);
	}

	private void CollectMeshChildrenRecursive(Node parent, Godot.Collections.Array<MeshInstance3D> result)
	{
		if (parent is MeshInstance3D mesh)
		{
			result.Add(mesh);
		}

		foreach (Node child in parent.GetChildren())
		{
			CollectMeshChildrenRecursive(child, result);
		}
	}

	private void CollectCollisionChildrenRecursive(Node parent, Godot.Collections.Array<CollisionShape3D> result)
	{
		if (parent is CollisionShape3D collisionShape)
		{
			result.Add(collisionShape);
		}

		foreach (Node child in parent.GetChildren())
		{
			CollectCollisionChildrenRecursive(child, result);
		}
	}
}
