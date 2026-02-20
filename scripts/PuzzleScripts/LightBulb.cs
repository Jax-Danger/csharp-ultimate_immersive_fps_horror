using Godot;

namespace Game.Scripts.Puzzles;

public partial class LightBulb : Node3D
{
	private MeshInstance3D _meshInstance;
	private StandardMaterial3D _originalMat;
	private StandardMaterial3D _emissionMat;

	public override void _Ready()
	{
		foreach (Node child in GetChildren())
		{
			if (child is MeshInstance3D mesh)
			{
				_meshInstance = mesh;
				break;
			}
		}

		_originalMat = _meshInstance?.GetActiveMaterial(0) as StandardMaterial3D;
	}

	public void Execute(float percentage)
	{
		if (percentage >= 99.0f)
		{
			if (_originalMat == null || _meshInstance == null)
			{
				return;
			}

			if (_emissionMat == null)
			{
				_emissionMat = _originalMat.Duplicate() as StandardMaterial3D;
				if (_emissionMat != null)
				{
					_emissionMat.EmissionEnabled = true;
					_emissionMat.EmissionEnergyMultiplier = 1.0f;
				}
			}

			_meshInstance.SetSurfaceOverrideMaterial(0, _emissionMat);
			return;
		}

		RestoreOriginal();
	}

	private void RestoreOriginal()
	{
		if (_originalMat != null && _meshInstance != null)
		{
			_meshInstance.SetSurfaceOverrideMaterial(0, _originalMat);
		}
	}
}
