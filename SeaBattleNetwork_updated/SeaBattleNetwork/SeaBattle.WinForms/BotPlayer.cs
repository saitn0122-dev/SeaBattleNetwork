namespace SeaBattle.WinForms;

public sealed class BotPlayer
{
    private readonly Random _random = new();
    private readonly Queue<Point> _targets = new();

    public BotDifficulty Difficulty { get; set; } = BotDifficulty.Medium;

    public Point NextShot(CellState[,] enemyView)
    {
        return Difficulty switch
        {
            BotDifficulty.Easy => RandomShot(enemyView),
            BotDifficulty.Medium => MediumShot(enemyView),
            BotDifficulty.Hard => HardShot(enemyView),
            _ => RandomShot(enemyView)
        };
    }

    public void AfterShot(Point shot, ShotResult result)
    {
        if (Difficulty == BotDifficulty.Easy) return;
        if (result is ShotResult.Hit or ShotResult.Killed)
        {
            foreach (var p in Neighbours(shot)) _targets.Enqueue(p);
        }
        if (result is ShotResult.Killed or ShotResult.Win) _targets.Clear();
    }

    private Point MediumShot(CellState[,] view)
    {
        while (_targets.Count > 0)
        {
            var p = _targets.Dequeue();
            if (CanShoot(view, p)) return p;
        }
        return RandomShot(view);
    }

    private Point HardShot(CellState[,] view)
    {
        while (_targets.Count > 0)
        {
            var p = _targets.Dequeue();
            if (CanShoot(view, p)) return p;
        }

        var candidates = new List<Point>();
        for (int x = 0; x < GameEngine.Size; x++)
        for (int y = 0; y < GameEngine.Size; y++)
            if (CanShoot(view, new Point(x, y)) && (x + y) % 2 == 0)
                candidates.Add(new Point(x, y));
        return candidates.Count > 0 ? candidates[_random.Next(candidates.Count)] : RandomShot(view);
    }

    private Point RandomShot(CellState[,] view)
    {
        while (true)
        {
            var p = new Point(_random.Next(GameEngine.Size), _random.Next(GameEngine.Size));
            if (CanShoot(view, p)) return p;
        }
    }

    private static bool CanShoot(CellState[,] view, Point p) => GameEngine.Inside(p.X, p.Y) && view[p.X, p.Y] == CellState.Empty;

    private static IEnumerable<Point> Neighbours(Point p)
    {
        yield return new Point(p.X + 1, p.Y);
        yield return new Point(p.X - 1, p.Y);
        yield return new Point(p.X, p.Y + 1);
        yield return new Point(p.X, p.Y - 1);
    }
}
