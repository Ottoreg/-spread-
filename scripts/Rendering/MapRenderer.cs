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

        // 4) Membrane des organes (bord vif), OUVERTE aux jonctions de corridors.
        foreach (var o in Map.Organs)
            DrawMembrane(o);
    }

    /// <summary>
    /// Dessine le contour de l'organe en laissant une "porte" (trou dans la
    /// membrane) à chaque endroit où un corridor se connecte, pour que la salle
    /// soit visiblement ouverte sur les chemins.
    /// </summary>
    private void DrawMembrane(OrganMap.Organ o)
    {
        const int segments = 96;
        float step = Mathf.Tau / segments;

        for (int s = 0; s < segments; s++)
        {
            float a0 = s * step;
            float a1 = a0 + step;
            float mid = a0 + step * 0.5f;
            if (IsDoorway(o, mid)) continue; // saute le segment = ouverture

            Vector2 p0 = o.Center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * o.Radius;
            Vector2 p1 = o.Center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * o.Radius;
            DrawLine(p0, p1, Membrane, 3f, true);
        }
    }

    /// <summary>Vrai si l'angle 'ang' tombe sur une jonction de corridor (porte).</summary>
    private bool IsDoorway(OrganMap.Organ o, float ang)
    {
        foreach (var c in Map.Corridors)
        {
            Vector2 other;
            if (c.A.DistanceSquaredTo(o.Center) < 1f) other = c.B;
            else if (c.B.DistanceSquaredTo(o.Center) < 1f) other = c.A;
            else continue;

            float doorAngle = (other - o.Center).Angle();
            // demi-largeur angulaire de la porte (le corridor sous-tend cet angle).
            float half = Mathf.Asin(Mathf.Min(c.HalfWidth / o.Radius, 0.99f)) * 1.35f;

            float d = Mathf.Abs(Mathf.AngleDifference(ang, doorAngle));
            if (d < half) return true;
        }
        return false;
    }
}
