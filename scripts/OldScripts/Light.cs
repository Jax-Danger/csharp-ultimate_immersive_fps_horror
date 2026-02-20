using Godot;

namespace Game.Scripts.Legacy;

public partial class Light : SpotLight3D
{
	[Export] private float _actuationPercentage = 0.8f;

	public void Execute(float percentage)
	{
		if (percentage > 0.97f)
		{
			LightEnergy = 100.0f;
		}
		else if (percentage < 0.03f)
		{
			LightEnergy = 0.0f;
		}
	}
}
