using Godot;

namespace Game.Scripts.Legacy;

public partial class Monkey : Node3D
{
	private MeshInstance3D _suzanne;
	private float _hueOffset = (float)GD.RandRange(-0.05f, 0.05f);

	public override void _Ready()
	{
		_suzanne = GetNodeOrNull<MeshInstance3D>("Suzanne");
	}

	public void Execute(float percentage)
	{
		float baseHue = percentage;
		float hue = Mathf.PosMod(baseHue + _hueOffset, 1.0f);
		Color color = Color.FromHsv(hue, 1.0f, 1.0f);

		if (_suzanne != null)
		{
			StandardMaterial3D material = _suzanne.GetActiveMaterial(0) as StandardMaterial3D;
			if (material != null)
			{
				StandardMaterial3D duplicated = material.Duplicate() as StandardMaterial3D;
				duplicated.AlbedoColor = color;
				_suzanne.SetSurfaceOverrideMaterial(0, duplicated);
			}
		}

		if (Name.ToString().Contains("2"))
		{
			RotateX(percentage / 10.0f);
		}
		else if (Name.ToString().Contains("3"))
		{
			RotateY(percentage / 10.0f);
		}
		else
		{
			RotateZ(percentage / 10.0f);
		}
	}
}
