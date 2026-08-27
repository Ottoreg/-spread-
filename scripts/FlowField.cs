using Godot;

/// <summary>
/// Champ de direction (flow field) pour le pathfinding de masse, désormais
/// AWARE DES MURS : le BFS ne se propage qu'à travers les cellules ouvertes de
/// l'<see cref="OrganMap"/>. Les anticorps activés suivent donc les corridors
/// pour rejoindre le joueur en contournant le tissu solide.
///
/// Un seul champ partagé, re-ciblé sur le joueur périodiquement. Chaque entité
/// lit la direction de sa cellule en O(1). Grille alignée sur celle de la carte
/// (mêmes dimensions/résolution).
/// </summary>
public class FlowField
{
    private readonly int _cols, _rows;
    private readonly float _cellSize;
    private readonly bool[] _open;
    private readonly Vector2[] _dir;
    private readonly int[] _cost;
    private readonly int[] _queue;
    private Vector2I _target;

    private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
    private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

    public FlowField(OrganMap map)
    {
        _cols = map.Cols;
        _rows = map.Rows;
        _cellSize = map.CellSize;
        _open = map.Open;
        _dir = new Vector2[_cols * _rows];
        _cost = new int[_cols * _rows];
        _queue = new int[_cols * _rows];
    }

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

        // Si le joueur est sur un mur (ne devrait pas arriver), on ne propage pas.
        if (!_open[t])
        {
            System.Array.Clear(_dir, 0, _dir.Length);
            return;
        }

        _cost[t] = 0;
        _queue[tail++] = t;

        // BFS coût uniforme, uniquement à travers les cellules ouvertes.
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
                if (!_open[ni]) continue;          // mur : infranchissable
                if (_cost[ni] <= nc) continue;
                _cost[ni] = nc;
                _queue[tail++] = ni;
            }
        }

        // Direction = vers le voisin ouvert de coût le plus faible.
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
                int ni = ny * _cols + nx;
                if (!_open[ni]) continue;
                if (_cost[ni] < best) { best = _cost[ni]; bx = Dx[k]; by = Dy[k]; }
            }

            Vector2 d = new(bx, by);
            _dir[c] = d.LengthSquared() > 0f ? d.Normalized() : Vector2.Zero;
        }
    }
}
