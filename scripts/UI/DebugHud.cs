using Godot;
using System;

/// <summary>
/// Overlay de debug/benchmark : FPS, population, temps de chaque système (ms),
/// et contrôles (population, LOD, cap FPS). Construit entièrement en code pour
/// éviter toute édition fragile de scène.
/// </summary>
public partial class DebugHud : Control
{
    public Game Game;

    private Label _label;
    private HSlider _slider;

    public override void _Ready()
    {
        var panel = new PanelContainer();
        panel.Position = new Vector2(8, 8);
        AddChild(panel);

        var vb = new VBoxContainer();
        panel.AddChild(vb);

        _label = new Label();
        vb.AddChild(_label);

        var hb = new HBoxContainer();
        vb.AddChild(hb);
        AddButton(hb, "-1000", () => Adjust(-1000));
        AddButton(hb, "+1000", () => Adjust(1000));
        AddButton(hb, "+5000", () => Adjust(5000));
        AddButton(hb, "x2", () => SetCount(Game.EntityCount * 2));
        AddButton(hb, "LOD", () => Game.ToggleLod());
        AddButton(hb, "Cap FPS", () => Game.ToggleFpsCap());

        _slider = new HSlider
        {
            MinValue = 0,
            MaxValue = Game.Capacity,
            Step = 500,
            CustomMinimumSize = new Vector2(320, 0),
            Value = Game.EntityCount
        };
        _slider.ValueChanged += v => SetCount((int)v);
        vb.AddChild(_slider);
    }

    public void Refresh()
    {
        if (_label == null || Game == null) return;

        _label.Text =
            $"FPS: {Engine.GetFramesPerSecond()}\n" +
            $"Entites: {Game.EntityCount}   Visibles: {Game.VisibleCount}\n" +
            $"grid {Game.MsGrid:0.00}  fsm {Game.MsFsm:0.00}  flock {Game.MsFlock:0.00}  " +
            $"integ {Game.MsIntegrate:0.00}  coll {Game.MsCollision:0.00}  render {Game.MsRender:0.00}  (ms)\n" +
            $"LOD: {(Game.LodEnabled ? "ON" : "off")}    FPS cap: {(Game.FpsCapped ? "60" : "off")}\n" +
            $"WASD/fleches: deplacer   Molette: zoom   Clic gauche: definir la cible";

        _slider.SetValueNoSignal(Game.EntityCount);
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
