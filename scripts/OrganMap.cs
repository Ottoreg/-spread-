using Godot;
using System.Collections.Generic;

/// <summary>
/// La carte : des ORGANES (blobs circulaires traversables) reliés par des
/// CORRIDORS, le reste du monde étant du tissu solide (mur). Générée de façon
/// déterministe (graine fixe) et rastérisée dans une grille de navigation
/// <see cref="Open"/> (true = traversable).
///
/// Cette grille sert à trois choses :
///  - le flow field ne propage qu'à travers les cellules ouvertes (navigation
///    réaliste dans le corps, contournement des murs) ;
///  - la collision joueur/anticorps/projectiles contre les murs ;
///  - le placement (spawn) des entités uniquement en zone ouverte.
///
/// Les organes/corridors sont aussi conservés sous forme de formes (cercles,
/// segments) pour un rendu fil-de-fer net et bon marché (voir MapRenderer).
/// </summary>
public class OrganMap
{
    public struct Organ { public Vector2 Center; public float Radius; }
    public struct Corridor { public Vector2 A, B; public float HalfWidth; }

    public readonly int Cols, Rows;
    public readonly float CellSize;
    public readonly bool[] Open;

    public readonly List<Organ> Organs = new();
    public readonly List<Corridor> Corridors = new();

    public Vector2 SpawnPoint { get; private set; }

    public OrganMap(float worldW, float worldH, float cellSize, ulong seed)
    {
        CellSize = cellSize;
        Cols = Mathf.CeilToInt(worldW / cellSize) + 1;
        Rows = Mathf.CeilToInt(worldH / cellSize) + 1;
        Open = new bool[Cols * Rows];

        Generate(worldW, worldH, seed);
        Rasterize();
    }

    private void Generate(float worldW, float worldH, ulong seed)
    {
        var rng = new RandomNumberGenerator { Seed = seed };

        float minX = Config.MapMargin, maxX = worldW - Config.MapMargin;
        float minY = Config.MapMargin, maxY = worldH - Config.MapMargin;

        // Placement des organes (rejet pour éviter les recouvrements trop forts).
        for (int n = 0; n < Config.OrganCount; n++)
        {
            Organ o = default;
            for (int tries = 0; tries < 40; tries++)
            {
                o.Center = new Vector2(rng.RandfRange(minX, maxX), rng.RandfRange(minY, maxY));
                o.Radius = rng.RandfRange(Config.OrganMinRadius, Config.OrganMaxRadius);

                bool ok = true;
                foreach (var e in Organs)
                    if (o.Center.DistanceTo(e.Center) < (o.Radius + e.Radius) * 0.75f) { ok = false; break; }
                if (ok) break;
            }
            Organs.Add(o);
        }

        // Connexité : chaque organe relié au plus proche déjà placé (arbre couvrant).
        for (int i = 1; i < Organs.Count; i++)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int j = 0; j < i; j++)
            {
                float d = Organs[i].Center.DistanceTo(Organs[j].Center);
                if (d < bestD) { bestD = d; best = j; }
            }
            Corridors.Add(new Corridor { A = Organs[i].Center, B = Organs[best].Center, HalfWidth = Config.CorridorHalfWidth });
        }

        // Quelques liens supplémentaires pour créer des boucles.
        int extra = Mathf.Max(1, Config.OrganCount / 3);
        for (int e = 0; e < extra; e++)
        {
            int a = rng.RandiRange(0, Organs.Count - 1);
            int b = rng.RandiRange(0, Organs.Count - 1);
            if (a != b)
                Corridors.Add(new Corridor { A = Organs[a].Center, B = Organs[b].Center, HalfWidth = Config.CorridorHalfWidth });
        }

        SpawnPoint = Organs[0].Center;
    }

    private void Rasterize()
    {
        for (int cy = 0; cy < Rows; cy++)
        for (int cx = 0; cx < Cols; cx++)
        {
            Vector2 p = new((cx + 0.5f) * CellSize, (cy + 0.5f) * CellSize);
            Open[cy * Cols + cx] = IsInsideShapes(p);
        }
    }

    private bool IsInsideShapes(Vector2 p)
    {
        foreach (var o in Organs)
            if (p.DistanceSquaredTo(o.Center) <= o.Radius * o.Radius) return true;

        foreach (var c in Corridors)
            if (DistanceToSegment(p, c.A, c.B) <= c.HalfWidth) return true;

        return false;
    }

    /// <summary>
    /// Test ANALYTIQUE (vraies formes : cercles + capsules), utilisé pour le
    /// mouvement/collision et le spawn -> parois parfaitement lisses, pas
    /// d'escaliers de pixels. (La grille Open[] reste pour le flow field.)
    /// </summary>
    public bool IsOpen(Vector2 p)
    {
        if (p.X < 0f || p.Y < 0f || p.X > Config.WorldWidth || p.Y > Config.WorldHeight)
            return false;
        return IsInsideShapes(p);
    }

    /// <summary>Test via la grille rastérisée (utilisé par le flow field).</summary>
    public bool IsOpenWorld(Vector2 p)
    {
        if (p.X < 0f || p.Y < 0f) return false;
        int cx = (int)(p.X / CellSize), cy = (int)(p.Y / CellSize);
        if (cx < 0 || cx >= Cols || cy < 0 || cy >= Rows) return false;
        return Open[cy * Cols + cx];
    }

    public bool IsOpenCell(int cx, int cy)
    {
        if (cx < 0 || cx >= Cols || cy < 0 || cy >= Rows) return false;
        return Open[cy * Cols + cx];
    }

    /// <summary>
    /// Déplacement avec collision murs, axe par axe (glissement le long des
    /// parois, façon tilemap). Chaque composante n'est appliquée que si la
    /// destination reste en zone ouverte. Sûr en parallèle (lecture seule).
    /// </summary>
    public Vector2 Slide(Vector2 from, Vector2 delta)
    {
        float x = from.X, y = from.Y;

        float nx = x + delta.X;
        if (IsOpen(new Vector2(nx, y))) x = nx;

        float ny = y + delta.Y;
        if (IsOpen(new Vector2(x, ny))) y = ny;

        return new Vector2(x, y);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.LengthSquared();
        float t = len2 <= 0.0001f ? 0f : Mathf.Clamp((p - a).Dot(ab) / len2, 0f, 1f);
        return p.DistanceTo(a + ab * t);
    }
}
