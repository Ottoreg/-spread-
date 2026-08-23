using Godot;

/// <summary>
/// Rendu des projectiles d'infection via un unique MultiMeshInstance2D
/// (petit segment fil-de-fer orienté selon la vitesse).
/// </summary>
public partial class ProjectileRenderer : MultiMeshInstance2D
{
    private MultiMesh _mm;
    private int _capacity;

    private static readonly Color ColorShot = new(1.0f, 0.95f, 0.4f);

    public void Init(int capacity)
    {
        _capacity = capacity;

        _mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
            UseColors = true,
            Mesh = WireMesh.Segment(Config.ProjectileRadius)
        };
        _mm.InstanceCount = capacity;
        _mm.VisibleInstanceCount = 0;

        Multimesh = _mm;
        ZIndex = 5;
    }

    public void UpdateInstances(Projectiles proj)
    {
        int count = proj.Count;
        var pos = proj.Position;
        var vel = proj.Velocity;

        for (int i = 0; i < count && i < _capacity; i++)
        {
            _mm.SetInstanceTransform2D(i, new Transform2D(vel[i].Angle(), pos[i]));
            _mm.SetInstanceColor(i, ColorShot);
        }

        _mm.VisibleInstanceCount = Mathf.Min(count, _capacity);
    }
}
