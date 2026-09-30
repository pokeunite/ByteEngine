using System.Numerics;

namespace ByteEngine.Core.Construction;

/// <summary>Shared four-neighbor route field; agents sample it without pathfinding.</summary>
public sealed class SwarmFlowField
{
    private readonly bool[] _blocked;
    private readonly int[] _distance;
    private readonly int[] _queue;
    private readonly Vector2[] _direction;
    private readonly float _cellSize;
    private readonly Vector2 _origin;

    public int Width { get; }
    public int Height { get; }

    public SwarmFlowField(int width, int height, float cellSize, Vector2 origin)
    {
        if (width <= 0 || height <= 0 || width > 4096 || height > 4096 ||
            (long)width * height > 4_000_000 || !float.IsFinite(cellSize) || cellSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        _cellSize = cellSize;
        _origin = origin;
        int count = width * height;
        _blocked = new bool[count];
        _distance = new int[count];
        _queue = new int[count];
        _direction = new Vector2[count];
    }

    public void SetBlocked(int x, int y, bool blocked) => _blocked[Index(x, y)] = blocked;

    public void Rebuild(int goalX, int goalY)
    {
        int goal = Index(goalX, goalY);
        Array.Fill(_distance, int.MaxValue);
        Array.Clear(_direction);
        if (_blocked[goal]) return;
        int head = 0, tail = 0;
        _queue[tail++] = goal;
        _distance[goal] = 0;
        while (head < tail)
        {
            int current = _queue[head++];
            int x = current % Width, y = current / Width;
            Visit(x - 1, y, current, ref tail);
            Visit(x + 1, y, current, ref tail);
            Visit(x, y - 1, current, ref tail);
            Visit(x, y + 1, current, ref tail);
        }
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int index = y * Width + x;
                if (_distance[index] is 0 or int.MaxValue) continue;
                int best = _distance[index];
                Vector2 direction = Vector2.Zero;
                Choose(x - 1, y, new Vector2(-1, 0), ref best, ref direction);
                Choose(x + 1, y, new Vector2(1, 0), ref best, ref direction);
                Choose(x, y - 1, new Vector2(0, -1), ref best, ref direction);
                Choose(x, y + 1, new Vector2(0, 1), ref best, ref direction);
                _direction[index] = direction;
            }
    }

    public Vector2 Sample(Vector2 world)
    {
        int x = (int)MathF.Floor((world.X - _origin.X) / _cellSize);
        int y = (int)MathF.Floor((world.Y - _origin.Y) / _cellSize);
        return x < 0 || y < 0 || x >= Width || y >= Height
            ? Vector2.Zero : _direction[y * Width + x];
    }

    /// <summary>Updates packed agent positions with no per-agent heap allocation.</summary>
    public void Step(Span<Vector2> positions, float speed, float deltaTime)
    {
        if (!float.IsFinite(speed) || speed < 0 || !float.IsFinite(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(speed));
        float distance = speed * deltaTime;
        for (int i = 0; i < positions.Length; i++)
            positions[i] += Sample(positions[i]) * distance;
    }

    private int Index(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
            throw new ArgumentOutOfRangeException(nameof(x));
        return y * Width + x;
    }

    private void Visit(int x, int y, int previous, ref int tail)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        int index = y * Width + x;
        if (_blocked[index] || _distance[index] != int.MaxValue) return;
        _distance[index] = _distance[previous] + 1;
        _queue[tail++] = index;
    }

    private void Choose(int x, int y, Vector2 candidate, ref int best, ref Vector2 direction)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        int value = _distance[y * Width + x];
        if (value >= best) return;
        best = value;
        direction = candidate;
    }
}
