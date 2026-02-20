using Godot;

namespace Game.Scripts.Puzzles;

public partial class PowerBox : Area3D
{
	[Export] public Godot.Collections.Array<Node> nodes_to_affect = new();
	[Export] public Light3D light;
	[Export] public Vector3 snap_offset = Vector3.Zero;

	private RigidBody3D _snappedObject;
	private bool _isBeingHeld;

	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	private void OnBodyEntered(Node body)
	{
		if (body is RigidBody3D rigidBody && !_isBeingHeld)
		{
			SnapObject(rigidBody);
		}
	}

	private void OnBodyExited(Node body)
	{
		if (body == _snappedObject)
		{
			ReleaseObject();
		}
	}

	private void SnapObject(RigidBody3D body)
	{
		_snappedObject = body;
		_snappedObject.GravityScale = 0.0f;
		_snappedObject.LinearVelocity = Vector3.Zero;
		_snappedObject.AngularVelocity = Vector3.Zero;

		Basis uprightBasis = Basis.Identity;
		uprightBasis.Y = Vector3.Up;
		_snappedObject.GlobalTransform = new Transform3D(uprightBasis, _snappedObject.GlobalTransform.Origin);
	}

	private void ReleaseObject()
	{
		if (_snappedObject != null)
		{
			_snappedObject.GravityScale = 1.0f;
			_snappedObject = null;
			_isBeingHeld = false;
		}
	}

	public override void _PhysicsProcess(double deltaRaw)
	{
		float delta = (float)deltaRaw;
		float percentage;
		if (_snappedObject != null)
		{
			percentage = 100.0f;
			if (light != null)
			{
				light.Visible = true;
			}

			Vector3 targetPos = GlobalTransform.Origin + snap_offset;
			Vector3 currentPos = _snappedObject.GlobalTransform.Origin;
			_snappedObject.GlobalTransform = new Transform3D(_snappedObject.GlobalTransform.Basis, currentPos.Lerp(targetPos, 10.0f * delta));

			Node grabbable = _snappedObject.FindChild("GrabbableInteraction", true, false);
			if (grabbable != null)
			{
				_isBeingHeld = grabbable.Get("is_interacting").AsBool();
			}
			else
			{
				_isBeingHeld = false;
			}

			if (!_isBeingHeld)
			{
				_snappedObject.RotationDegrees = Vector3.Zero;
			}
		}
		else
		{
			if (light != null)
			{
				light.Visible = false;
			}
			percentage = 0.0f;
		}

		NotifyNodes(percentage);
	}

	private void NotifyNodes(float percentage)
	{
		foreach (Node node in nodes_to_affect)
		{
			if (node != null && node.HasMethod("execute"))
			{
				node.Call("execute", percentage);
			}
		}
	}
}
