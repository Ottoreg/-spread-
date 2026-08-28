using Godot;
using System;

/// <summary>
/// Interface. Deux couches :
///  - HUD gameplay (toujours visible) : barre d'ALERTE en haut à gauche,
///    montants d'ADN récolté en bas à gauche.
///  - Panneau ADMIN (masqué par défaut, bascule avec Échap) : FPS, populations,
///    profiling par système, et contrôles (population, LOD, threads, FPS cap).
///
/// Construit entièrement en code pour éviter toute édition fragile de scène.
/// </summary>
public partial class DebugHud : Control
{
    public Game Game;

    // Barre d'alerte
    private ColorRect _alertFill;
    private Label _alertValue;

    // ADN
    private Label _adnOff, _adnSurv, _adnReinf;
    private VBoxContainer _adnBox;

    // Admin
    private PanelContainer _adminPanel;
    private Label _adminLabel;
    private HSlider _slider;

    private const float AlertBarWidth = 260f;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore; // ne bloque pas la visée/tir

        BuildAlertBar();
        BuildAdnPanel();
        BuildAdminPanel();
    }

    // ---------- Construction ----------

    private void BuildAlertBar()
    {
        var title = new Label { Text = "ALERTE", Position = new Vector2(12, 10) };
        title.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(title);

        var bg = new ColorRect
        {
            Position = new Vector2(12, 34),
            Size = new Vector2(AlertBarWidth, 20),
            Color = new Color(0.15f, 0.15f, 0.18f, 0.85f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(bg);

        _alertFill = new ColorRect
        {
            Position = new Vector2(12, 34),
            Size = new Vector2(0, 20),
            Color = new Color(0.3f, 0.9f, 0.3f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_alertFill);

        _alertValue = new Label { Position = new Vector2(16, 34) };
        _alertValue.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_alertValue);
    }

    private void BuildAdnPanel()
    {
        _adnBox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_adnBox);

        _adnOff = MakeAdnLabel(new Color(1.0f, 0.4f, 0.35f));   // offensif = rouge
        _adnSurv = MakeAdnLabel(new Color(0.45f, 1.0f, 0.5f));  // survie = vert
        _adnReinf = MakeAdnLabel(new Color(1.0f, 0.9f, 0.4f));  // renforcement = jaune
        _adnBox.AddChild(_adnOff);
        _adnBox.AddChild(_adnSurv);
        _adnBox.AddChild(_adnReinf);
    }

    private void BuildAdminPanel()
    {
        _adminPanel = new PanelContainer { Position = new Vector2(12, 66), Visible = false };
        AddChild(_adminPanel);

        var vb = new VBoxContainer();
        _adminPanel.AddChild(vb);

        _adminLabel = new Label();
        vb.AddChild(_adminLabel);

        var hb = new HBoxContainer();
        vb.AddChild(hb);
        AddButton(hb, "-100", () => Adjust(-100));
        AddButton(hb, "+100", () => Adjust(100));
        AddButton(hb, "+500", () => Adjust(500));
        AddButton(hb, "x2", () => SetCount(Game.EntityCount * 2));
        AddButton(hb, "LOD", () => Game.ToggleLod());
        AddButton(hb, "Threads", () => Game.ToggleThreads());
        AddButton(hb, "Cap FPS", () => Game.ToggleFpsCap());

        _slider = new HSlider
        {
            MinValue = 0,
            MaxValue = Game.Capacity,
            Step = 100,
            CustomMinimumSize = new Vector2(340, 0),
            Value = Game.EntityCount
        };
        _slider.ValueChanged += v => SetCount((int)v);
        vb.AddChild(_slider);
    }

    // ---------- Runtime ----------

    public void ToggleAdmin()
    {
        if (_adminPanel != null)
            _adminPanel.Visible = !_adminPanel.Visible;
    }

    public void Refresh()
    {
        if (Game == null) return;

        // Barre d'alerte : largeur + couleur (vert -> rouge).
        float t = Mathf.Clamp(Game.AlertPct / 100f, 0f, 1f);
        _alertFill.Size = new Vector2(AlertBarWidth * t, 20f);
        _alertFill.Color = new Color(0.25f + 0.75f * t, 0.85f * (1f - t) + 0.1f, 0.25f);
        _alertValue.Text = $"{Game.AlertPct:0}%";

        // ADN (bas à gauche) — positionné selon la hauteur du viewport.
        float h = GetViewportRect().Size.Y;
        _adnBox.Position = new Vector2(12, h - 84);
        _adnOff.Text = $"ADN offensif       {Game.AdnOffensive}";
        _adnSurv.Text = $"ADN survie         {Game.AdnSurvival}";
        _adnReinf.Text = $"ADN renforcement   {Game.AdnReinforce}";

        // Panneau admin (si visible).
        if (_adminPanel.Visible)
        {
            _adminLabel.Text =
                $"FPS: {Engine.GetFramesPerSecond()}    PV: {Game.PlayerHealth:0}    ALERTE: {Game.AlertPct:0}%    infections: {Game.TotalInfections}\n" +
                $"Cellules: {Game.EntityCount}   Défenses actives: {Game.ActivatedCount}   Visibles: {Game.VisibleCount}\n" +
                $"grid {Game.MsGrid:0.00}  ia {Game.MsBehavior:0.00}  integ {Game.MsIntegrate:0.00}  " +
                $"coll {Game.MsCollision:0.00}  viro {Game.MsProjectiles:0.00}  rendu {Game.MsRender:0.00}  (ms)\n" +
                $"LOD: {(Game.LodEnabled ? "ON" : "off")}    " +
                $"Threads: {(Game.Multithread ? $"ON ({Game.ThreadCount})" : "off")}    " +
                $"FPS cap: {(Game.FpsCapped ? "60" : "off")}\n" +
                $"Triangle=défense  Hexagone=proie  Carré=neutre  Vert=infectée\n" +
                $"ZQSD: bouger   Souris: viser   Clic gauche: coup de lame   C: compétences   Molette: zoom   R: reset   Échap: fermer";
            _slider.SetValueNoSignal(Game.EntityCount);
        }
    }

    // ---------- Helpers ----------

    private static Label MakeAdnLabel(Color color)
    {
        var l = new Label { MouseFilter = MouseFilterEnum.Ignore };
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private void SetCount(int n)
    {
        Game.SetEntityCount(n);
        _slider.SetValueNoSignal(Game.EntityCount);
    }

    private void Adjust(int delta) => SetCount(Game.EntityCount + delta);

    private static void AddButton(Node parent, string text, Action onPressed)
    {
        var b = new Button { Text = text };
        b.Pressed += () => onPressed();
        parent.AddChild(b);
    }
}
