using Godot;
using System;

/// <summary>
/// Menu d'accueil : nouvelle partie et choix de la graine (seed) de génération
/// de la carte. Émet <see cref="StartRequested"/> avec la graine choisie.
/// </summary>
public partial class MainMenu : Control
{
    public event Action<ulong> StartRequested;

    private LineEdit _seedInput;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = new Color(0.06f, 0.02f, 0.03f) };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vb = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        vb.AddThemeConstantOverride("separation", 14);
        center.AddChild(vb);

        var title = new Label { Text = "INFECT", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 48);
        title.AddThemeColorOverride("font_color", new Color(0.5f, 1f, 0.55f));
        vb.AddChild(title);

        var subtitle = new Label
        {
            Text = "Rogue-like d'infection virale",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.Modulate = new Color(1, 1, 1, 0.7f);
        vb.AddChild(subtitle);

        vb.AddChild(new HSeparator());

        // Ligne seed : label + champ + bouton aléatoire.
        var seedRow = new HBoxContainer();
        vb.AddChild(seedRow);
        seedRow.AddChild(new Label { Text = "Seed :", CustomMinimumSize = new Vector2(60, 0) });

        _seedInput = new LineEdit
        {
            Text = Config.MapSeed.ToString(),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        seedRow.AddChild(_seedInput);

        var randomBtn = new Button { Text = "Aléatoire" };
        randomBtn.Pressed += () => _seedInput.Text = RandomSeed().ToString();
        seedRow.AddChild(randomBtn);

        // Bouton nouvelle partie.
        var play = new Button { Text = "Nouvelle partie", CustomMinimumSize = new Vector2(0, 44) };
        play.Pressed += OnPlay;
        vb.AddChild(play);

        var hint = new Label
        {
            Text = "ZQSD bouger · souris viser · clic gauche attaquer · C compétences",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.Modulate = new Color(1, 1, 1, 0.5f);
        vb.AddChild(hint);
    }

    private void OnPlay()
    {
        ulong seed = ulong.TryParse(_seedInput.Text.Trim(), out ulong s) ? s : RandomSeed();
        StartRequested?.Invoke(seed);
    }

    private static ulong RandomSeed()
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        return rng.Randi() * 1000u + rng.Randi() % 1000u;
    }
}
