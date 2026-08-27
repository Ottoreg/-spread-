using Godot;

/// <summary>
/// Rendu de toutes les cellules en fil-de-fer, une forme par TYPE :
///  - Defensive : triangle   - Prey : hexagone   - Neutral : carré.
/// Un MultiMeshInstance2D par type (donc ~3 draw calls), rempli par culling.
/// Couleur selon l'état : infectée (vert) sinon selon type/comportement.
/// </summary>
public partial class EntityRenderer : Node2D
{
    private readonly MultiMesh[] _mm = new MultiMesh[CellKind.Count];
    private int _capacity;

    private static readonly Color Infected = new(0.40f, 1.00f, 0.45f);
    private static readonly Color DefDormant = new(0.30f, 0.70f, 1.00f);
    private static readonly Color DefActive = new(1.00f, 0.35f, 0.30f);
    private static readonly Color Prey = new(1.00f, 0.55f, 0.75f);
    private static readonly Color Neutral = new(0.65f, 0.70f, 0.85f);

    public int VisibleCount { get; private set; }

    public void Init(int capacity)
    {
        _capacity = capacity;
        float r = Config.EntityRadius;
        var meshes = new Mesh[]
        {
            WireMesh.Polygon(3, r),          // Defensive
            WireMesh.Polygon(6, r * 1.1f),   // Prey
            WireMesh.Polygon(4, r),          // Neutral
        };

        for (int k = 0; k < CellKind.Count; k++)
        {
            var inst = new MultiMeshInstance2D();
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
                UseColors = true,
                Mesh = meshes[k]
            };
            mm.InstanceCount = capacity;
            mm.VisibleInstanceCount = 0;
            inst.Multimesh = mm;
            AddChild(inst);
            _mm[k] = mm;
        }
    }

    public void UpdateInstances(Simulation sim, Rect2 view)
    {
        int count = sim.Count;
        var pos = sim.Position;
        var vel = sim.Velocity;
        var state = sim.State;
        var kind = sim.Kind;

        System.Span<int> v = stackalloc int[CellKind.Count];

        for (int i = 0; i < count; i++)
        {
            Vector2 p = pos[i];
            if (!view.HasPoint(p)) continue;

            int kk = kind[i];
            int idx = v[kk];
            if (idx >= _capacity) continue;

            _mm[kk].SetInstanceTransform2D(idx, new Transform2D(vel[i].Angle(), p));
            _mm[kk].SetInstanceColor(idx, ColorFor(kind[i], state[i]));
            v[kk]++;
        }

        int total = 0;
        for (int k = 0; k < CellKind.Count; k++)
        {
            _mm[k].VisibleInstanceCount = v[k];
            total += v[k];
        }
        VisibleCount = total;
    }

    private static Color ColorFor(byte kind, byte state)
    {
        if (state == Simulation.Infected) return Infected;
        return kind switch
        {
            CellKind.Defensive => state == Simulation.Activated ? DefActive : DefDormant,
            CellKind.Prey => Prey,
            _ => Neutral,
        };
    }
}
