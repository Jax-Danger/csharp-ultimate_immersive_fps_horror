using Godot;

namespace Game.Scripts.Legacy;

public partial class PuzzleComplete : Node3D
{
	private Node _sanityController;
	private bool _completed;

	public void Execute(float percentage)
	{
		if (!_completed && percentage > 0.9f)
		{
			_sanityController = GetTree().CurrentScene.FindChild("SanityController", true, false);
			if (_sanityController != null && _sanityController.HasMethod("add_sanity"))
			{
				_sanityController.Call("add_sanity", 10);
				_sanityController.Call("on_puzzle_complete", 0.1f, 0.5f);
				_completed = true;
			}
		}
	}
}
