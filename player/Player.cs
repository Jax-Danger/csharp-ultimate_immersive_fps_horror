using Godot;
using Game.Interactions;

namespace Game.Player;

public partial class Player : CharacterBody3D
{
	private Node3D Head => GetNode<Node3D>("%Head");
	private Node3D Eyes => GetNode<Node3D>("%Eyes");
	private Camera3D Camera3D => GetNode<Camera3D>("%Camera3D");
	private CollisionShape3D StandingCollisionShape => GetNode<CollisionShape3D>("StandingCollisionShape");
	private CollisionShape3D CrouchingCollisionShape => GetNode<CollisionShape3D>("CrouchingCollisionShape");
	private ShapeCast3D StandupCheck => GetNode<ShapeCast3D>("StandupCheck");
	private InteractionController InteractionController => GetNode<InteractionController>("%InteractionController");
	private AudioStreamPlayer3D FootstepsSe => GetNode<AudioStreamPlayer3D>("%Footsteps");
	private AudioStreamPlayer3D JumpSe => GetNode<AudioStreamPlayer3D>("%Jump");
	private Camera3D NoteCamera => GetNode<Camera3D>("%NoteCamera");
	private Marker3D NoteHand => GetNode<Marker3D>("%NoteHand");
	private Marker3D ItemHand => GetNode<Marker3D>("%ItemHand");
	private InventoryController InventoryController => GetNode<InventoryController>("%InventoryController/CanvasLayer/InventoryUI");
	private RayCast3D InteractionRaycast => GetNode<RayCast3D>("%InteractionRaycast");

	[Export] private float noteSwayAmount = 0.1f;

	private const float WalkingSpeed = 3.0f;
	private const float SprintingSpeed = 5.0f;
	private const float CrouchingSpeed = 1.0f;
	private const float CrouchingDepth = -0.9f;
	private const float JumpVelocity = 4.0f;
	private const float LerpSpeed = 10.0f;
	private const float BaseFov = 90.0f;
	private const float NormalSensitivity = 0.2f;
	private const float SensitivityRestoreSpeed = 5.0f;

	private const float HeadBobbingSprintingSpeed = 22.0f;
	private const float HeadBobbingWalkingSpeed = 14.0f;
	private const float HeadBobbingCrouchingSpeed = 10.0f;
	private const float HeadBobbingSprintingIntensity = 0.2f;
	private const float HeadBobbingWalkingIntensity = 0.1f;
	private const float HeadBobbingCrouchingIntensity = 0.05f;

	private enum PlayerState
	{
		IdleStand,
		IdleCrouch,
		Crouching,
		Walking,
		Sprinting,
		Air
	}

	private PlayerState playerState = PlayerState.IdleStand;
	private float currentSpeed = WalkingSpeed;
	private bool moving;
	private bool isInAir;
	private bool inventoryOpenedFlag;
	private bool sensitivityFadingIn;
	private float currentSensitivity = NormalSensitivity;

	private Vector2 inputDir = Vector2.Zero;
	private Vector2 mouseInput;
	private Vector3 direction = Vector3.Zero;
	private Vector2 headBobbingVector = Vector2.Zero;
	private float headBobbingIndex;
	private float headBobbingCurrentIntensity;

	private float targetLean;
	private float currentLean;
	private const float LeanAngle = 12.0f;
	private const float LeanOffset = 0.25f;
	private const float LeanSpeed = 8.0f;

	private float lastBobPositionX;
	private int lastBobDirection;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
		StandupCheck.ExcludeParent = true;
		StandupCheck.CollideWithAreas = false;
		StandupCheck.CollideWithBodies = true;
		StandupCheck.Enabled = true;
	}

	public override void _Input(InputEvent @event)
	{
		if (Input.IsActionJustPressed("quit"))
		{
			GetTree().Quit();
			return;
		}

		UpdateLeanInput();

		if (HandleInventoryInput())
		{
			return;
		}

		if (@event is InputEventMouseMotion motion)
		{
			HandleMouseLook(motion);
		}
	}

	public override void _PhysicsProcess(double deltaRaw)
	{
		float delta = (float)deltaRaw;
		UpdatePlayerState();
		UpdateCamera(delta);
		ApplyVerticalMotion(delta);
		ApplyHorizontalMotion(delta);
		MoveAndSlide();
		NoteTiltAndSway(delta);
	}

	public override void _Process(double deltaRaw)
	{
		float delta = (float)deltaRaw;
		if (inventoryOpenedFlag && !Input.IsActionPressed("inventory"))
		{
			CloseInventory();
		}

		if (sensitivityFadingIn)
		{
			currentSensitivity = Mathf.Lerp(currentSensitivity, NormalSensitivity, delta * SensitivityRestoreSpeed);
			if (Mathf.Abs(currentSensitivity - NormalSensitivity) < 0.01f)
			{
				currentSensitivity = NormalSensitivity;
				sensitivityFadingIn = false;
			}
		}

		SetCameraLocked(InteractionController.IsCameraLocked());
	}

	private void UpdateLeanInput()
	{
		targetLean = Input.IsActionPressed("lean_left") ? -1.0f : Input.IsActionPressed("lean_right") ? 1.0f : 0.0f;
	}

	private bool HandleInventoryInput()
	{
		if (Input.IsActionJustPressed("inventory"))
		{
			InteractionController.InteractionComponent?.PostInteract();
			InventoryController.Visible = true;
			InteractionRaycast.Enabled = false;
			inventoryOpenedFlag = true;
			Input.MouseMode = Input.MouseModeEnum.Visible;
			return true;
		}

		if (Input.IsActionPressed("inventory"))
		{
			return true;
		}

		if (!Input.IsActionJustReleased("inventory"))
		{
			return false;
		}

		CloseInventory();
		return true;
	}

	private void CloseInventory()
	{
		InventoryController.Visible = false;
		InventoryController.ContextMenu.Visible = false;
		InteractionRaycast.Enabled = true;
		inventoryOpenedFlag = false;
		if (InteractionController.CurrentObject == null)
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	private void HandleMouseLook(InputEventMouseMotion motion)
	{
		if (currentSensitivity <= 0.01f || InteractionController.IsCameraLocked())
		{
			return;
		}

		mouseInput = motion.Relative;
		RotateY(Mathf.DegToRad(-mouseInput.X * currentSensitivity));
		Head.RotateX(Mathf.DegToRad(-mouseInput.Y * currentSensitivity));

		Vector3 headRot = Head.Rotation;
		headRot.X = Mathf.Clamp(headRot.X, Mathf.DegToRad(-85), Mathf.DegToRad(85));
		Head.Rotation = headRot;
	}

	private void UpdatePlayerState()
	{
		moving = inputDir != Vector2.Zero;
		if (!IsOnFloor())
		{
			playerState = PlayerState.Air;
		}
		else if (Input.IsActionPressed("crouch"))
		{
			playerState = moving ? PlayerState.Crouching : PlayerState.IdleCrouch;
		}
		else if (!StandupCheck.IsColliding())
		{
			playerState = !moving ? PlayerState.IdleStand : Input.IsActionPressed("sprint") ? PlayerState.Sprinting : PlayerState.Walking;
		}

		bool crouching = playerState is PlayerState.Crouching or PlayerState.IdleCrouch;
		StandingCollisionShape.Disabled = crouching;
		CrouchingCollisionShape.Disabled = !crouching;
		StandingCollisionShape.Visible = !crouching;
		CrouchingCollisionShape.Visible = crouching;

		currentSpeed = playerState switch
		{
			PlayerState.Crouching or PlayerState.IdleCrouch => CrouchingSpeed,
			PlayerState.Sprinting => SprintingSpeed,
			_ => WalkingSpeed
		};
	}

	private void ApplyVerticalMotion(float delta)
	{
		if (!IsOnFloor())
		{
			isInAir = true;
			Velocity += GetGravity() * delta * (Velocity.Y >= 0 ? 1.0f : 2.0f);
			return;
		}

		if (isInAir)
		{
			FootstepsSe.Play();
			isInAir = false;
		}

		if (Input.IsActionJustPressed("jump") && playerState != PlayerState.Crouching)
		{
			Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
			JumpSe.Play();
		}
	}

	private void ApplyHorizontalMotion(float delta)
	{
		inputDir = Input.GetVector("left", "right", "forward", "backward");
		direction = direction.Lerp((Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized(), delta * 10.0f);

		if (direction == Vector3.Zero)
		{
			Velocity = new Vector3(Mathf.MoveToward(Velocity.X, 0, currentSpeed), Velocity.Y, Mathf.MoveToward(Velocity.Z, 0, currentSpeed));
			return;
		}

		Velocity = new Vector3(direction.X * currentSpeed, Velocity.Y, direction.Z * currentSpeed);
	}

	private void UpdateCamera(float delta)
	{
		(float targetHeadY, float targetFov, float intensity, float speed) = playerState switch
		{
			PlayerState.Crouching or PlayerState.IdleCrouch => (1.8f + CrouchingDepth, BaseFov * 0.95f, HeadBobbingCrouchingIntensity, HeadBobbingCrouchingSpeed),
			PlayerState.Sprinting => (1.8f, BaseFov * 1.05f, HeadBobbingSprintingIntensity, HeadBobbingSprintingSpeed),
			_ => (1.8f, BaseFov, HeadBobbingWalkingIntensity, HeadBobbingWalkingSpeed)
		};

		Head.Position = new Vector3(Head.Position.X, Mathf.Lerp(Head.Position.Y, targetHeadY, delta * LerpSpeed), Head.Position.Z);
		Camera3D.Fov = Mathf.Lerp(Camera3D.Fov, targetFov, delta * LerpSpeed);
		headBobbingCurrentIntensity = intensity;
		headBobbingIndex += speed * delta;

		headBobbingVector = new Vector2(Mathf.Sin(headBobbingIndex / 2.0f), Mathf.Sin(headBobbingIndex));
		Vector2 targetEye = moving
			? new Vector2(headBobbingVector.X * headBobbingCurrentIntensity, headBobbingVector.Y * (headBobbingCurrentIntensity / 2.0f))
			: Vector2.Zero;

		Eyes.Position = new Vector3(
			Mathf.Lerp(Eyes.Position.X, targetEye.X, delta * LerpSpeed),
			Mathf.Lerp(Eyes.Position.Y, targetEye.Y, delta * LerpSpeed),
			Eyes.Position.Z);

		currentLean = Mathf.Lerp(currentLean, targetLean, delta * LeanSpeed);
		float targetTilt = Mathf.DegToRad(-LeanAngle) * currentLean;
		float targetOffset = LeanOffset * currentLean;
		Camera3D.Rotation = new Vector3(Camera3D.Rotation.X, Camera3D.Rotation.Y, Mathf.Lerp(Camera3D.Rotation.Z, targetTilt, delta * LeanSpeed));
		Camera3D.Position = new Vector3(Mathf.Lerp(Camera3D.Position.X, targetOffset, delta * LeanSpeed), Camera3D.Position.Y, Camera3D.Position.Z);
		NoteCamera.Fov = Camera3D.Fov;
		PlayFootsteps();
	}

	private void SetCameraLocked(bool locked)
	{
		if (locked)
		{
			currentSensitivity = 0.0f;
			sensitivityFadingIn = false;
			return;
		}

		sensitivityFadingIn = true;
	}

	private void NoteTiltAndSway(float delta)
	{
		NoteHand.Rotation = new Vector3(
			Mathf.Lerp(NoteHand.Rotation.X, -inputDir.Y * noteSwayAmount, 10 * delta),
			NoteHand.Rotation.Y,
			Mathf.Lerp(NoteHand.Rotation.Z, -inputDir.X * noteSwayAmount, 10 * delta));

		ItemHand.Rotation = new Vector3(
			Mathf.Lerp(ItemHand.Rotation.X, -inputDir.Y * noteSwayAmount * 2, 10 * delta),
			ItemHand.Rotation.Y,
			Mathf.Lerp(ItemHand.Rotation.Z, -inputDir.X * noteSwayAmount * 2, 10 * delta));
	}

	private void PlayFootsteps()
	{
		if (!moving || !IsOnFloor())
		{
			lastBobDirection = 0;
			lastBobPositionX = headBobbingVector.X;
			return;
		}

		float bobPositionX = headBobbingVector.X;
		float directionDelta = bobPositionX - lastBobPositionX;
		int bobDirection = directionDelta > 0 ? 1 : directionDelta < 0 ? -1 : 0;
		if (bobDirection != 0 && bobDirection != lastBobDirection && lastBobDirection != 0)
		{
			FootstepsSe.Play();
		}

		lastBobDirection = bobDirection;
		lastBobPositionX = bobPositionX;
	}
}
