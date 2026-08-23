using Godot;

/// <summary>
/// Rendu de TOUS les anticorps via un unique MultiMeshInstance2D, en fil-de-fer.
/// Un seul draw call (ou presque) pour des dizaines de milliers de formes.
///
/// Culling : seules les entités dans le rectangle visible (élargi d'une marge)
/// sont poussées. Couleur par état FSM : dormant (froid, atténué) vs activé
/// (chaud, vif). Rotation par le cap (angle de la vitesse).
/// </summary>
public partial class EntityRenderer : MultiMeshInstance2D
{
    private MultiMesh _mm;
    private int _capacity;

    private static readonly Color ColorDormant = new(0.25f, 0.55f, 0.75f);
    private static readonly Color ColorActive = new(1.00f, 0.35f, 0.30f);

    public int VisibleCount { get; private set; }

    public void Init(int capacity)
    {
        _capacity = capacity;

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = WireMesh.Polygon(3, Config.EntityRadius) // anticorps = triangle wireframe
        };
        _mm.InstanceCount = capacity;
        _mm.VisibleInstanceCount = 0;

        Multimesh = _mm;
    }

    public void UpdateInstances(Simulation sim, Rect2 view)
    {
        int count = sim.Count;
        var pos = sim.Position;
        var vel = sim.Velocity;
        var state = sim.State;

        int v = 0;
        for (int i = 0; i < count; i++)
        {
            Vector2 p = pos[i];
            if (!view.HasPoint(p)) continue;

            float ang = vel[i].Angle();
            _mm.SetInstanceTransform2D(v, new Transform2D(ang, p));
            _mm.SetInstanceColor(v, state[i] == Simulation.Activated ? ColorActive : ColorDormant);

            v++;
            if (v >= _capacity) break;
        }

        VisibleCount = v;
        _mm.VisibleInstanceCount = v;
    }
}
