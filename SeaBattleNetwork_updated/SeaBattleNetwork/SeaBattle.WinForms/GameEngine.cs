namespace SeaBattle.WinForms;

public sealed class GameEngine
{
    public const int Size = 10;
    public CellState[,] MyField { get; } = new CellState[Size, Size];
    public CellState[,] EnemyView { get; } = new CellState[Size, Size];
    public List<Ship> MyShips { get; } = new();
    private readonly Random _random = new();

    private static readonly int[] ShipSet = { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
    public static IReadOnlyList<int> RequiredShipSizes => ShipSet;

    public void Clear()
    {
        Array.Clear(MyField);
        Array.Clear(EnemyView);
        MyShips.Clear();
    }

    public void ClearEnemyView() => Array.Clear(EnemyView);

    public void AutoPlaceShips()
    {
        Clear();
        foreach (var shipSize in ShipSet)
        {
            var placed = false;
            for (int attempts = 0; attempts < 1000 && !placed; attempts++)
            {
                var horizontal = _random.Next(2) == 0;
                var x = _random.Next(Size);
                var y = _random.Next(Size);
                placed = TryPlaceShip(x, y, shipSize, horizontal);
            }
            if (!placed) throw new InvalidOperationException("Не удалось расставить корабли автоматически.");
        }
    }

    public bool TryPlaceShip(int x, int y, int shipSize, bool horizontal)
    {
        var cells = new List<Point>();
        for (int i = 0; i < shipSize; i++)
        {
            int cx = x + (horizontal ? i : 0);
            int cy = y + (horizontal ? 0 : i);
            if (!Inside(cx, cy) || !CanPlaceAt(cx, cy)) return false;
            cells.Add(new Point(cx, cy));
        }
        var ship = new Ship { Size = shipSize };
        foreach (var p in cells)
        {
            MyField[p.X, p.Y] = CellState.Ship;
            ship.Cells.Add(p);
        }
        MyShips.Add(ship);
        return true;
    }

    private bool CanPlaceAt(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        {
            int nx = x + dx, ny = y + dy;
            if (Inside(nx, ny) && MyField[nx, ny] == CellState.Ship) return false;
        }
        return true;
    }

    public ShotResult ReceiveShot(int x, int y)
    {
        if (!Inside(x, y)) return ShotResult.Repeat;
        if (MyField[x, y] is CellState.Miss or CellState.Hit or CellState.Killed) return ShotResult.Repeat;

        if (MyField[x, y] == CellState.Empty)
        {
            MyField[x, y] = CellState.Miss;
            return ShotResult.Miss;
        }

        MyField[x, y] = CellState.Hit;
        var ship = MyShips.First(s => s.Cells.Contains(new Point(x, y)));
        if (ship.IsKilled(MyField))
        {
            foreach (var p in ship.Cells) MyField[p.X, p.Y] = CellState.Killed;
            return MyShips.All(s => s.IsKilled(MyField)) ? ShotResult.Win : ShotResult.Killed;
        }
        return ShotResult.Hit;
    }

    public void MarkEnemyShot(int x, int y, ShotResult result)
    {
        if (!Inside(x, y)) return;
        EnemyView[x, y] = result switch
        {
            ShotResult.Miss => CellState.Miss,
            ShotResult.Hit => CellState.Hit,
            ShotResult.Killed or ShotResult.Win => CellState.Killed,
            _ => EnemyView[x, y]
        };
    }

    public static bool Inside(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size;
}
