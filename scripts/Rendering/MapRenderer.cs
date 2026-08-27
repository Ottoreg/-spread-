using Godot;

/// <summary>
/// Dessine le corps de l'hôte : fond = tissu sombre (couleur de fond du
/// viewport), organes et corridors = cavités pleines (ton chair) bordées d'une
/// membrane plus vive. Rendu à partir des FORMES (cercles + capsules), donc net
/// et bon marché. Statique : dessiné une fois.
///
/// Les remplissages sont OPAQUES : les recouvrements organe/corridor se fondent
/// sans coutures. Les anticorps (cyan) et le virus (vert) ressortent dessus.
/// </summary>
public partial class MapRenderer : Node2D
{
    public OrganMap Map;

    // Ton chair des cavités (un peu plus clair que le tissu de fond).
    private static readonly Color Chamber = new(0.30f, 0.13f, 0.16f);
    // Membrane (paroi) : bord plus vif.
    private static readonly Color Membrane = new(0.80f, 0.42f, 0.48f);
    private static readonly Color Vessel = new(0.55f, 0.28f, 0.34f);

    public override void _Ready()
    {
        ZIndex = -10; // sous les entités
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (Map == null) return;

        // 1) Remplissage des corridors (capsules).
        foreach (var c in Map.Corridors)
        {
            DrawLine(c.A, c.B, Chamber, c.HalfWidth * 2f);
            DrawCircle(c.A, c.HalfWidth, Chamber);
            DrawCircle(c.B, c.HalfWidth, Chamber);
        }

        // 2) Parois des corridors (deux bords).
        foreach (var c in Map.Corridors)
        {
            Vector2 dir = c.B - c.A;
            if (dir.LengthSquared() < 0.001f) continue;
            dir = dir.Normalized();
            Vector2 n = new Vector2(-dir.Y, dir.X) * c.HalfWidth;
            DrawLine(c.A + n, c.B + n, Vessel, 2f, true);
            DrawLine(c.A - n, c.B - n, Vessel, 2f, true);
        }

        // 3) Remplissage des organes — recouvre les stubs de parois de corridors
        //    qui entrent dans les cavités (jointures propres).
        foreach (var o in Map.Organs)
            DrawCircle(o.Center, o.Radius, Chamber);

        // 4) Membrane des organes (bord vif).
        foreach (var o in Map.Organs)
            DrawArc(o.Center, o.Radius, 0f, Mathf.Tau, 96, Membrane, 3f, true);
    }
}
