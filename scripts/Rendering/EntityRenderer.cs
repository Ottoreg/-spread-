using Godot;

/// <summary>
/// Rendu de TOUTES les entités via un unique MultiMeshInstance2D.
/// Un seul draw call (ou presque) pour des dizaines de milliers de sprites.
///
/// Culling : seules les entités dans le rectangle visible (élargi d'une marge)
/// sont poussées dans le MultiMesh. VisibleInstanceCount limite le dessin au
/// nombre réellement visible.
///
/// Couleur par état FSM (debug). Rotation par le cap (angle de la vitesse).
/// </summary>
public partial class EntityRenderer : MultiMeshInstance2D
{
    private MultiMesh _mm;
    private int _capacity;

    private static readonly Color ColorState0 = new(0.30f, 0.80f, 1.00f);
    private static readonly Color ColorState1 = new(1.00f, 0.70f, 0.20f);

    public int VisibleCount { get; private set; }

    public void Init(int capacity)
    {
        _capacity = capacity;

        var quad = new QuadMesh
        {
            Size = new Vector2(Config.EntityRadius * 2f, Config.EntityRadius * 2f)
        };

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = quad
        };
        _mm.InstanceCount = capacity;
        _mm.VisibleInstanceCount = 0;

        Multimesh = _mm;
        Texture = MakeWhiteTexture();
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
            _mm.SetInstanceColor(v, state[i] == 0 ? ColorState0 : ColorState1);

            v++;
            if (v >= _capacity) break;
        }

        VisibleCount = v;
        _mm.VisibleInstanceCount = v;
    }

    private static Texture2D MakeWhiteTexture()
    {
        var img = Image.Create(2, 2, false, Image.Format.Rgba8);
        img.Fill(Colors.White);
        return ImageTexture.CreateFromImage(img);
    }
}
