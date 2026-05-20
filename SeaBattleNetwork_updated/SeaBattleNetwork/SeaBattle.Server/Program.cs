using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var games = new ConcurrentDictionary<string, ServerGame>();

app.MapPost("/games", (CreateGameRequest request) =>
{
    var id = Guid.NewGuid().ToString("N")[..6].ToUpper();
    var game = new ServerGame(id, request.PlayerName) { CurrentTurn = request.PlayerName };
    games[id] = game;
    return Results.Ok(game.ToDto());
});

app.MapPost("/games/{id}/join", (string id, JoinGameRequest request) =>
{
    if (!games.TryGetValue(id, out var game)) return Results.NotFound("Игра не найдена");
    game.Player2 = request.PlayerName;
    return Results.Ok(game.ToDto());
});

app.MapGet("/games/{id}", (string id) => games.TryGetValue(id, out var game) ? Results.Ok(game.ToDto()) : Results.NotFound());

app.MapPost("/games/{id}/shots", (string id, ShotDto shot) =>
{
    if (!games.TryGetValue(id, out var game)) return Results.NotFound();

    var target = shot.CurrentTurn;
    game.Messages.Add(new ServerMessage(shot, target));

    if (shot.Result == "Shot")
    {
        game.CurrentTurn = target;
    }
    else
    {
        if (shot.Result == "Win")
        {
            game.IsFinished = true;
            game.Winner = target;
        }
        game.CurrentTurn = shot.Result == "Miss" ? shot.PlayerName : target;
    }

    return Results.Ok();
});

app.MapGet("/games/{id}/messages/next", (string id, string playerName) =>
{
    if (!games.TryGetValue(id, out var game)) return Results.NotFound();

    lock (game.Messages)
    {
        var message = game.Messages.FirstOrDefault(m => !m.Delivered && m.TargetPlayer == playerName);
        if (message == null) return Results.Ok(null);
        message.Delivered = true;
        return Results.Ok(message.Shot);
    }
});

// 0.0.0.0 нужен для локальной сети: сервер будет доступен другим ПК по IP компьютера.
app.Run("http://0.0.0.0:5100");

public sealed record CreateGameRequest(string PlayerName);
public sealed record JoinGameRequest(string GameId, string PlayerName);
public sealed record ShotDto(string GameId, string PlayerName, int X, int Y, string Result, string CurrentTurn);
public sealed record GameStateDto(string GameId, string Player1, string? Player2, string CurrentTurn, bool IsFinished, string? Winner);

public sealed class ServerGame(string id, string player1)
{
    public string GameId { get; } = id;
    public string Player1 { get; } = player1;
    public string? Player2 { get; set; }
    public string CurrentTurn { get; set; } = player1;
    public bool IsFinished { get; set; }
    public string? Winner { get; set; }
    public List<ServerMessage> Messages { get; } = new();
    public GameStateDto ToDto() => new(GameId, Player1, Player2, CurrentTurn, IsFinished, Winner);
}

public sealed class ServerMessage(ShotDto shot, string targetPlayer)
{
    public ShotDto Shot { get; } = shot;
    public string TargetPlayer { get; } = targetPlayer;
    public bool Delivered { get; set; }
}
