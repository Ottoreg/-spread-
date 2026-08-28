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

        // Ébauche d'arbre : compétences regroupées par catégorie d'ADN.
        vb.AddChild(MakeHeader("OFFENSIF  (ADN offensif)", new Color(1f, 0.45f, 0.4f)));
        AddSkillRow(vb, Skills.Attaque);
        AddSkillRow(vb, Skills.Infection);

        vb.AddChild(MakeHeader("SURVIE  (ADN survie)", new Color(0.5f, 1f, 0.55f)));
        AddSkillRow(vb, Skills.Protection);
        AddSkillRow(vb, Skills.Regeneration);
        AddSkillRow(vb, Skills.Fuite);

        vb.AddChild(MakeHeader("RENFORCEMENT  (ADN joker)", new Color(1f, 0.9f, 0.45f)));
        var renf = new Label { Text = "Complète automatiquement n'importe quel achat." };
        renf.Modulate = new Color(1, 1, 1, 0.7f);
        vb.AddChild(renf);

        vb.AddChild(new HSeparator());
        var hint = new Label { Text = "C : fermer   (jeu en pause)" };
        hint.Modulate = new Color(1, 1, 1, 0.6f);
        vb.AddChild(hint);
    }

    private void AddSkillRow(VBoxContainer vb, int branch)
    {
        var row = new HBoxContainer();
        vb.AddChild(row);

        _name[branch] = new Label { CustomMinimumSize = new Vector2(130, 0) };
        row.AddChild(_name[branch]);

        _level[branch] = new Label { CustomMinimumSize = new Vector2(80, 0) };
        row.AddChild(_level[branch]);

        _effect[branch] = new Label { CustomMinimumSize = new Vector2(190, 0) };
        row.AddChild(_effect[branch]);

        _buy[branch] = new Button { CustomMinimumSize = new Vector2(130, 0) };
        _buy[branch].Pressed += () => Game.TryUpgradeSkill(branch);
        row.AddChild(_buy[branch]);
    }

    private static Label MakeHeader(string text, Color color)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", 16);
        l.AddThemeColorOverride("font_color", color);
        return l;
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
