using Godot;

/// <summary>
/// Dessine la carte en fil-de-fer : contour des organes (cercles) et parois des
/// corridors (deux bords parallèles). Rendu à partir des FORMES (pas de la
/// grille rastérisée), donc net et bon marché. Statique : dessiné une fois.
/// </summary>
public partial class MapRenderer : Node2D
{
    public OrganMap Map;

    private static readonly Color Membrane = new(0.55f, 0.40f, 0.75f, 0.9f);

    public override void _Ready()
    {
        ZIndex = -10; // sous les entités
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Map == null) return;

        foreach (var o in Map.Organs)
            DrawArc(o.Center, o.Radius, 0f, Mathf.Tau, 72, Membrane, 2f, true);

        foreach (var c in Map.Corridors)
        {
            Vector2 dir = (c.B - c.A);
            if (dir.LengthSquared() < 0.001f) continue;
            dir = dir.Normalized();
            Vector2 n = new Vector2(-dir.Y, dir.X) * c.HalfWidth;

            DrawLine(c.A + n, c.B + n, Membrane, 2f, true);
            DrawLine(c.A - n, c.B - n, Membrane, 2f, true);
        }
    }
}
