using Godot;
using System;

/// <summary>
/// Grille de hachage spatiale uniforme.
/// Reconstruite chaque tick par counting-sort en O(n), sans allocation.
/// Permet des requêtes de voisinage en O(k) (9 cellules autour) au lieu de O(n²).
/// C'est la structure qui rend flocking et collisions viables à grande échelle.
///
/// Layout : après Rebuild, les indices d'entités de la cellule c occupent
/// Entities[CellStart[c] .. CellStart[c+1]-1].
/// </summary>
public class SpatialHashGrid
{
    private readonly float _cellSize;
    public int Cols { get; }
    public int Rows { get; }
    private readonly int _cellCount;

    private readonly int[] _cellStart;   // taille cellCount+1 (offsets, prefix sum)
    private readonly int[] _cursor;      // curseurs de remplissage, taille cellCount
    private readonly int[] _entities;    // indices d'entités triés par cellule
    private readonly int[] _cellOf;      // cellule de chaque entité

    public int[] CellStart => _cellStart;
    public int[] Entities => _entities;
    public int[] CellOf => _cellOf;

    public SpatialHashGrid(float worldW, float worldH, float cellSize, int capacity)
    {
        _cellSize = cellSize;
        Cols = Mathf.CeilToInt(worldW / cellSize) + 1;
        Rows = Mathf.CeilToInt(worldH / cellSize) + 1;
        _cellCount = Cols * Rows;
        _cellStart = new int[_cellCount + 1];
        _cursor = new int[_cellCount];
        _entities = new int[capacity];
        _cellOf = new int[capacity];
    }

    public int CellIndex(Vector2 p)
    {
        int cx = Mathf.Clamp((int)(p.X / _cellSize), 0, Cols - 1);
        int cy = Mathf.Clamp((int)(p.Y / _cellSize), 0, Rows - 1);
        return cy * Cols + cx;
    }

    public void Rebuild(Vector2[] positions, int count)
    {
        Array.Clear(_cursor, 0, _cellCount);

        // 1) compter les entités par cellule
        for (int i = 0; i < count; i++)
        {
            int c = CellIndex(positions[i]);
            _cellOf[i] = c;
            _cursor[c]++;
        }

        // 2) prefix sum -> offsets de début de cellule
        int sum = 0;
        for (int c = 0; c < _cellCount; c++)
        {
            _cellStart[c] = sum;
            sum += _cursor[c];
        }
        _cellStart[_cellCount] = sum;

        // 3) réinitialiser les curseurs au début de chaque cellule, puis placer
        Array.Copy(_cellStart, _cursor, _cellCount);
        for (int i = 0; i < count; i++)
        {
            int c = _cellOf[i];
            _entities[_cursor[c]++] = i;
        }
    }
}
