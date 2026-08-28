using Godot;

/// <summary>
/// Interface de l'arbre de compétences (touche C). Une ligne par branche du GDD
/// (Attaque, Infection, Protection, Régénération, Fuite) : niveau, effet actuel,
/// coût du prochain niveau, et un bouton « + » pour l'améliorer avec l'ADN.
/// La simulation est mise en pause tant que le panneau est ouvert (voir Game).
/// </summary>
public partial class SkillTreeHud : Control
{
    public Game Game;

    private Label _adnLine;
    private readonly Label[] _name = new Label[Skills.Count];
    private readonly Label[] _level = new Label[Skills.Count];
    private readonly Label[] _effect = new Label[Skills.Count];
    private readonly Button[] _buy = new Button[Skills.Count];

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer();
        center.AddChild(panel);

        var vb = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        panel.AddChild(vb);

        var title = new Label { Text = "COMPÉTENCES  —  ADN pour évoluer" };
        title.AddThemeFontSizeOverride("font_size", 22);
        vb.AddChild(title);

        _adnLine = new Label();
        vb.AddChild(_adnLine);
        vb.AddChild(new HSeparator());

        for (int b = 0; b < Skills.Count; b++)
        {
            int branch = b; // capture
            var row = new HBoxContainer();
            vb.AddChild(row);

            _name[b] = new Label { CustomMinimumSize = new Vector2(120, 0) };
            row.AddChild(_name[b]);

            _level[b] = new Label { CustomMinimumSize = new Vector2(80, 0) };
            row.AddChild(_level[b]);

            _effect[b] = new Label { CustomMinimumSize = new Vector2(200, 0) };
            row.AddChild(_effect[b]);

            _buy[b] = new Button { CustomMinimumSize = new Vector2(120, 0) };
            _buy[b].Pressed += () => Game.TryUpgradeSkill(branch);
            row.AddChild(_buy[b]);
        }

        vb.AddChild(new HSeparator());
        var hint = new Label { Text = "C : fermer   (jeu en pause)" };
        hint.Modulate = new Color(1, 1, 1, 0.6f);
        vb.AddChild(hint);
    }

    public void Toggle() => Visible = !Visible;

    public void Refresh()
    {
        if (!Visible || Game == null) return;
        var s = Game.PlayerSkills;

        _adnLine.Text =
            $"ADN — offensif {Game.AdnOffensive}   survie {Game.AdnSurvival}   renforcement {Game.AdnReinforce}";

        for (int b = 0; b < Skills.Count; b++)
        {
            _name[b].Text = s.Name(b);
            _level[b].Text = $"Nv {s.Level[b]}/{Skills.MaxLevel}";
            _effect[b].Text = s.Effect(b);

            if (s.IsMaxed(b))
            {
                _buy[b].Text = "MAX";
                _buy[b].Disabled = true;
            }
            else
            {
                string type = s.IsOffensive(b) ? "off" : "surv";
                _buy[b].Text = $"+  ({s.NextCost(b)} {type})";
                _buy[b].Disabled = !Game.CanAfford(b);
            }
        }
    }
}
