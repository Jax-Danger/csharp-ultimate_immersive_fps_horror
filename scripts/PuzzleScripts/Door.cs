using Game.Interactions;
using Godot;

public partial class Door : StaticBody3D
{
	[Export] private DoorInteraction doorInteraction;

	public void unlock()
	{
		doorInteraction.IsLocked = false;
	}
}
