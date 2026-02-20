using Godot;

namespace Game.Scripts.Legacy;

public partial class Bridge : Node3D
{
	private float _startingRotation;
	[Export] private float _finalRotation = 90.0f;

	public override void _Ready()
	{
		_startingRotation = Rotation.X;
		_finalRotation = Mathf.DegToRad(Mathf.RadToDeg(_startingRotation) + _finalRotation);
	}

	public void Execute(float percentage)
	{
		Rotation = new Vector3(_startingRotation - percentage * (_finalRotation - _startingRotation), Rotation.Y, Rotation.Z);
	}
}
