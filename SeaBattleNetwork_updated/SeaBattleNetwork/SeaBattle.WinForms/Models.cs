namespace SeaBattle.WinForms;

public enum CellState { Empty, Ship, Miss, Hit, Killed }
public enum ShotResult { Miss, Hit, Killed, Win, Repeat }
public enum BotDifficulty { Easy, Medium, Hard }

public sealed class Ship
{
    public int Size { get; init; }
    public List<Point> Cells { get; } = new();
    public bool IsKilled(CellState[,] field) => Cells.All(p => field[p.X, p.Y] == CellState.Hit || field[p.X, p.Y] == CellState.Killed);
}

public sealed record ShotDto(string GameId, string PlayerName, int X, int Y, string Result, string CurrentTurn);
public sealed record GameStateDto(string GameId, string Player1, string? Player2, string CurrentTurn, bool IsFinished, string? Winner);
public sealed record CreateGameRequest(string PlayerName);
public sealed record JoinGameRequest(string GameId, string PlayerName);
