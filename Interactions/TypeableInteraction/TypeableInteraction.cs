using Godot;
using System.Linq;

namespace Game.Interactions;

[GlobalClass]
public partial class TypeableInteraction : AbstractInteraction
{
	#region Export vars
	[Export]
	public Godot.Collections.Array<int> CorrectCode { get; set; } = new() { 5, 6, 7, 8, 9 };

	[Export]
	public AudioStream ButtonPressSoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/keypad_press.ogg");

	[Export]
	public AudioStream CorrectCodeSoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/keypad_success.ogg");

	[Export]
	public AudioStream WrongCodeSoundEffect { get; set; } = GD.Load<AudioStream>("res://assets/sound_effects/keypad_failure.ogg");
	#endregion

	#region variables
	private AudioStreamPlayer3D _buttonPressAudioPlayer;
	private AudioStreamPlayer3D _correctCodeAudioPlayer;
	private AudioStreamPlayer3D _wrongCodeAudioPlayer;
	private Godot.Collections.Array<StaticBody3D> _buttons = new();
	private Godot.Collections.Array<int> _enteredCode = new();
	private int _maxCodeLength = 5;
	private Label3D _screenLabel;
	private Node3D _targetButton;
	#endregion
	public override void _Ready()
	{
		base._Ready();

		_buttonPressAudioPlayer = new AudioStreamPlayer3D { Stream = ButtonPressSoundEffect };
		AddChild(_buttonPressAudioPlayer);

		_correctCodeAudioPlayer = new AudioStreamPlayer3D { Stream = CorrectCodeSoundEffect };
		AddChild(_correctCodeAudioPlayer);

		_wrongCodeAudioPlayer = new AudioStreamPlayer3D { Stream = WrongCodeSoundEffect };
		AddChild(_wrongCodeAudioPlayer);

		Node parent = GetParent();
		if (parent == null)
		{
			return;
		}

		_screenLabel = parent.GetNodeOrNull<Label3D>("%Screen");
		foreach (Node node in parent.GetChildren())
		{
			if (node is StaticBody3D button)
			{
				_buttons.Add(button);
			}
		}
	}

	public override void PreInteract()
	{
		base.PreInteract();
		PressButton(_targetButton);
	}

	public void SetTargetButton(Node3D target)
	{
		_targetButton = target;
	}

	private void PressButton(Node target)
	{
		if (target == null)
		{
			return;
		}

		if (target is StaticBody3D button && _buttons.Contains(button))
		{
			Tween tween = CreateTween();
			tween.TweenProperty(target, "position:z", 0.02f, 0.1f);
			tween.TweenProperty(target, "position:z", 0.0f, 0.1f);
		}

		_buttonPressAudioPlayer?.Play();

		switch (target.Name.ToString())
		{
			case "sbClear":
				_enteredCode.Clear();
				if (_screenLabel != null)
				{
					_screenLabel.Text = "-----";
					_screenLabel.Modulate = Colors.White;
				}
				break;
			case "sbOK":
				bool isCorrect = _enteredCode.SequenceEqual(CorrectCode);
				if (isCorrect)
				{
					if (_screenLabel != null)
					{
						_screenLabel.Text = "ENTER";
						_screenLabel.Modulate = Colors.Green;
					}
					_correctCodeAudioPlayer?.Play();

					foreach (Node node in NodesToAffect)
					{
						if (node != null && node.HasMethod("unlock"))
						{
							node.Call("unlock");
						}
					}
				}
				else
				{
					if (_screenLabel != null)
					{
						_screenLabel.Text = "ERROR";
						_screenLabel.Modulate = Colors.Red;
					}
					_wrongCodeAudioPlayer?.Play();
				}

				_enteredCode.Clear();
				break;
			default:
				string name = target.Name.ToString();
				if (name.Length > 2 && int.TryParse(name.Substring(2), out int num))
				{
					if (_enteredCode.Count < _maxCodeLength)
					{
						_enteredCode.Add(num);
						if (_screenLabel != null)
						{
							_screenLabel.Text = string.Concat(_enteredCode.Select(n => n.ToString()));
							_screenLabel.Modulate = Colors.White;
						}
					}
					else
					{
						GD.Print("Code is Full");
					}
				}
				break;
		}
	}
}
