using Godot;

/// <summary>
/// Champ de direction (flow field) pour le pathfinding de masse.
///
/// Au lieu d'un A* PAR entité (impensable à 10 000 entités), on calcule UN SEUL
/// champ partagé : un BFS depuis la cellule cible remplit un coût par cellule,
/// puis chaque cellule pointe vers son voisin de plus faible coût.
/// Chaque entité lit ensuite la direction de sa cellule en O(1).
///
/// Recalculé uniquement quand la cible change (SetTargetWorld). Le prototype est
/// sans obstacle ; ajouter des murs = marquer certaines cellules infranchissables
/// avant le BFS, sans changer le reste de l'architecture.
/// </summary>
public class FlowField
{
    private readonly int _cols, _rows;
    private readonly float _cellSize;
    private readonly Vector2[] _dir;
    private readonly int[] _cost;
    private readonly int[] _queue;
    private Vector2I _target;

    private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

    public FlowField(float worldW, float worldH, float cellSize)
    {
        _cellSize = cellSize;
        _cols = Mathf.CeilToInt(worldW / cellSize) + 1;
        _rows = Mathf.CeilToInt(worldH / cellSize) + 1;
        _dir = new Vector2[_cols * _rows];
        _cost = new int[_cols * _rows];
        _queue = new int[_cols * _rows];
    }

    public Vector2 TargetWorld =>
        new((_target.X + 0.5f) * _cellSize, (_target.Y + 0.5f) * _cellSize);

    public void SetTargetWorld(Vector2 world)
    {
        int tx = Mathf.Clamp((int)(world.X / _cellSize), 0, _cols - 1);
        int ty = Mathf.Clamp((int)(world.Y / _cellSize), 0, _rows - 1);
        _target = new Vector2I(tx, ty);
        Recompute();
    }

    public Vector2 SampleDirection(Vector2 world)
    {
        int cx = Mathf.Clamp((int)(world.X / _cellSize), 0, _cols - 1);
        int cy = Mathf.Clamp((int)(world.Y / _cellSize), 0, _rows - 1);
        return _dir[cy * _cols + cx];
    }

    private void Recompute()
    {
        for (int i = 0; i < _cost.Length; i++)
            _cost[i] = int.MaxValue;

        int head = 0, tail = 0;
        int t = _target.Y * _cols + _target.X;
        _cost[t] = 0;
        _queue[tail++] = t;

        // BFS (coût uniforme). Chaque cellule est atteinte au coût optimal une fois.
        while (head < tail)
        {
            int c = _queue[head++];
            int cx = c % _cols, cy = c / _cols;
            int nc = _cost[c] + 1;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + Dx[k], ny = cy + Dy[k];
                if (nx < 0 || nx >= _cols || ny < 0 || ny >= _rows) continue;
                int ni = ny * _cols + nx;
                if (_cost[ni] <= nc) continue;
                _cost[ni] = nc;
                _queue[tail++] = ni;
            }
        }

        // Direction = vers le voisin de coût le plus faible.
        for (int cy = 0; cy < _rows; cy++)
        for (int cx = 0; cx < _cols; cx++)
        {
            int c = cy * _cols + cx;
            if (_cost[c] == int.MaxValue) { _dir[c] = Vector2.Zero; continue; }

            int best = _cost[c];
            int bx = 0, by = 0;
            for (int k = 0; k < 8; k++)
            {
                int nx = cx + Dx[k], ny = cy + Dy[k];
                if (nx < 0 || nx >= _cols || ny < 0 || ny >= _rows) continue;
                int cost = _cost[ny * _cols + nx];
                if (cost < best) { best = cost; bx = Dx[k]; by = Dy[k]; }
            }

            Vector2 d = new(bx, by);
            _dir[c] = d.LengthSquared() > 0f ? d.Normalized() : Vector2.Zero;
        }
    }
}
