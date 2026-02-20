using Godot;
using Game.Interactions;

namespace Game.Player;

public partial class SanityController : Node
{
	private SubViewport LightDetectionViewport => GetNode<SubViewport>("%LightViewport");
	private TextureRect SanityCamView => GetNode<TextureRect>("%SanityCamView");
	private ColorRect AverageLightColorView => GetNode<ColorRect>("%AverageLightColorView");
	private Node3D LightDetection => GetNode<Node3D>("%LightDetection");
	private Label DebugLabel => GetNode<Label>("%Debug");
	private Sprite2D DistortionSprite => GetNode<Sprite2D>("%Distortion");
	private ShaderMaterial DistortionMaterial => DistortionSprite.Material as ShaderMaterial;
	private Camera3D PlayerCamera => GetNode<Camera3D>("%Camera3D");
	private Sprite2D FlashSprite => GetNode<Sprite2D>("%PuzzleComplete");
	private ShaderMaterial FlashMaterial => FlashSprite.Material as ShaderMaterial;

	private float lightLevel;
	private float sanity = 100.0f;
	private float timeSinceSanityChange;

	private const float SanityDrainInterval = 0.25f;
	private const float DarknessThreshold = 0.3f;
	private const float SanityRegenTarget = 51.0f;
	private const float SanityRegenRate = 1.0f / SanityDrainInterval;
	private const float EnemyViewRange = 10.0f;

	public override void _Ready()
	{
		LightDetectionViewport.DebugDraw = Viewport.DebugDrawEnum.Lighting;
	}

	public override void _Process(double deltaRaw)
	{
		float delta = (float)deltaRaw;
		lightLevel = GetLightLevel();
		UpdateSanity(delta);
		UpdateDistortion();
		DrainSanityFromVisibleEnemies(delta);
		DebugLabel.Text = $"FPS: {Engine.GetFramesPerSecond()}\nLight Level: {lightLevel:F2}\nSanity: {sanity:F2}\nState: {GetSanityState()}";
	}

	public void DrainSanity(float amount) => sanity = Mathf.Clamp(sanity - amount, 0.0f, 100.0f);
	public void AddSanity(float amount) => sanity = Mathf.Clamp(sanity + amount, 0.0f, 100.0f);

	public void OnPuzzleComplete(float flashDuration = 0.1f, float fadeDuration = 0.5f)
	{
		FlashSprite.Visible = true;
		FlashMaterial?.SetShaderParameter("alpha", 0.5f);

		Tween tween = GetTree().CreateTween();
		tween.TweenInterval(flashDuration);
		tween.TweenProperty(FlashMaterial, "shader_parameter/alpha", 0.0f, fadeDuration);
		tween.TweenCallback(Callable.From(OnFlashComplete));
	}

	private float GetLightLevel()
	{
		LightDetection.GlobalPosition = GetParent<Node3D>().GlobalPosition;
		ViewportTexture texture = LightDetectionViewport.GetTexture();
		SanityCamView.Texture = texture;
		Color averageColor = GetAverageColor(texture);
		AverageLightColorView.Color = averageColor;
		return averageColor.Luminance;
	}

	private static Color GetAverageColor(ViewportTexture texture)
	{
		Image image = texture.GetImage();
		image.Resize(1, 1, Image.Interpolation.Lanczos);
		return image.GetPixel(0, 0);
	}

	private void UpdateSanity(float delta)
	{
		timeSinceSanityChange += delta;

		if (lightLevel <= DarknessThreshold)
		{
			if (timeSinceSanityChange < SanityDrainInterval || sanity <= 0.0f)
			{
				return;
			}

			sanity = Mathf.Clamp(sanity - 1.0f, 0.0f, 100.0f);
			timeSinceSanityChange = 0.0f;
			return;
		}

		if (sanity >= SanityRegenTarget || timeSinceSanityChange < SanityDrainInterval)
		{
			return;
		}

		sanity = Mathf.Clamp(sanity + (SanityRegenRate * SanityDrainInterval), 0.0f, SanityRegenTarget);
		timeSinceSanityChange = 0.0f;
	}

	private string GetSanityState()
	{
		if (sanity >= 75.0f) return "Crystal Clear";
		if (sanity >= 50.0f) return "A slight headache";
		if (sanity >= 25.0f) return "Head is pounding and hands are shaking";
		if (sanity >= 1.0f) return "...";
		return "Unconscious";
	}

	private void UpdateDistortion()
	{
		float distortion = 0.0f;
		if (sanity < 50.0f)
		{
			float t = Mathf.Pow((50.0f - sanity) / 50.0f, 2.5f);
			distortion = t * 0.05f;
		}

		DistortionMaterial?.SetShaderParameter("distortion_strength", distortion);
	}

	private void DrainSanityFromVisibleEnemies(float delta)
	{
		foreach (Node node in GetTree().GetNodesInGroup("enemy"))
		{
			if (node is not Node3D enemy)
			{
				continue;
			}

			if (!IsEnemyOnScreen(enemy) || !IsEnemyInView(enemy, EnemyViewRange) || !HasLineOfSight(enemy))
			{
				continue;
			}

			DrainSanity(delta * 8.0f);
		}
	}

	private bool IsEnemyInView(Node3D enemy, float toleranceDegrees)
	{
		Vector3 cameraPos = PlayerCamera.GlobalTransform.Origin;
		Vector3 toEnemy = (enemy.GlobalTransform.Origin - cameraPos).Normalized();
		Vector3 forward = -PlayerCamera.GlobalTransform.Basis.Z;
		float dot = Mathf.Clamp(forward.Dot(toEnemy), -1.0f, 1.0f);
		return Mathf.RadToDeg(Mathf.Acos(dot)) <= toleranceDegrees;
	}

	private bool IsEnemyOnScreen(Node3D enemy)
	{
		Vector2 screenSize = PlayerCamera.GetViewport().GetVisibleRect().Size;
		Vector3 toEnemy = enemy.GlobalTransform.Origin - PlayerCamera.GlobalTransform.Origin;
		if ((-PlayerCamera.GlobalTransform.Basis.Z).Dot(toEnemy) < 0.0f)
		{
			return false;
		}

		Vector2 screenPos = PlayerCamera.UnprojectPosition(enemy.GlobalTransform.Origin);
		return screenPos.X >= 0.0f && screenPos.X <= screenSize.X && screenPos.Y >= 0.0f && screenPos.Y <= screenSize.Y;
	}

	private bool HasLineOfSight(Node3D enemy)
	{
		PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(PlayerCamera.GlobalTransform.Origin, enemy.GlobalTransform.Origin);
		query.CollisionMask = 1;
		return PlayerCamera.GetWorld3D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	private void OnFlashComplete()
	{
		FlashMaterial?.SetShaderParameter("alpha", 0.0f);
		FlashSprite.Visible = false;
	}
}
