
namespace AgentTown.World;

public static class PathFinder
{
    // Paliekame seną metodą, kad veiktų ankstesni testai
    public static List<(int X, int Y)> FindPath(
        WorldMap map,
        Location start,
        Location destination)
    {
        return FindPath(
            map,
            (start.X, start.Y),
            (destination.X, destination.Y));
    }

    // Naujas metodas: kelio paieška pagal koordinates
    public static List<(int X, int Y)> FindPath(
        WorldMap map,
        (int X, int Y) startPos,
        (int X, int Y) endPos)
    {
        if (!map.IsInside(startPos.X, startPos.Y) ||
            !map.IsInside(endPos.X, endPos.Y))
        {
            return new();
        }

        var queue = new Queue<(int X, int Y)>();
        var visited = new HashSet<(int X, int Y)>();
        var previous = new Dictionary<
            (int X, int Y), (int X, int Y)>();

        queue.Enqueue(startPos);
        visited.Add(startPos);

        var directions = new[]
        {
            (0, 1),
            (1, 0),
            (0, -1),
            (-1, 0)
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (current == endPos)
            {
                var path = new List<(int X, int Y)>();

                while (current != startPos)
                {
                    path.Add(current);
                    current = previous[current];
                }

                path.Add(startPos);
                path.Reverse();

                return path;
            }

            foreach (var (dx, dy) in directions)
            {
                var next = (
                    X: current.X + dx,
                    Y: current.Y + dy);

                if (!map.IsInside(next.X, next.Y))
                    continue;

                if (next != endPos &&
                    !map.IsRoad(next.X, next.Y))
                    continue;

                if (!visited.Add(next))
                    continue;

                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        return new();
    }
}
