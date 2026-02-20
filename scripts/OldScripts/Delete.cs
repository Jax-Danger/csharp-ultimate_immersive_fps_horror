using Godot;

namespace Game.Scripts.Legacy;

public partial class Delete : Node3D
{
	private MeshInstance3D _suzanne;

	public override void _Ready()
	{
		_suzanne = GetNodeOrNull<MeshInstance3D>("Suzanne");
	}

	public void Execute(float percentage)
	{
		if (percentage > 0.95f)
		{
			QueueFree();
		}
	}
}
