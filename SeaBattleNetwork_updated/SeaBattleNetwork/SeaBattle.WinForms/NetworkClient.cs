using System.Net.Http.Json;

namespace SeaBattle.WinForms;

public sealed class NetworkClient
{
    // Для игры по локальной сети можно задать переменную среды SEABATTLE_SERVER_URL,
    // например: http://192.168.0.15:5100
    private readonly HttpClient _http = new()
    {
        BaseAddress = new Uri(Environment.GetEnvironmentVariable("SEABATTLE_SERVER_URL") ?? "http://localhost:5100")
    };

    public async Task<GameStateDto?> CreateGameAsync(string playerName)
    {
        var response = await _http.PostAsJsonAsync("/games", new CreateGameRequest(playerName));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameStateDto>();
    }

    public async Task<GameStateDto?> JoinGameAsync(string gameId, string playerName)
    {
        var response = await _http.PostAsJsonAsync($"/games/{gameId}/join", new JoinGameRequest(gameId, playerName));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GameStateDto>();
    }

    public async Task<GameStateDto?> GetStateAsync(string gameId)
        => await _http.GetFromJsonAsync<GameStateDto>($"/games/{gameId}");

    public async Task SendShotAsync(ShotDto shot)
    {
        var response = await _http.PostAsJsonAsync($"/games/{shot.GameId}/shots", shot);
        response.EnsureSuccessStatusCode();
    }

    // Возвращает следующее сообщение, адресованное текущему игроку:
    // Result = "Shot" — соперник выстрелил по нашему полю.
    // Result = "Miss/Hit/Killed/Win" — пришёл результат нашего прошлого выстрела.
    public async Task<ShotDto?> GetNextMessageAsync(string gameId, string playerName)
        => await _http.GetFromJsonAsync<ShotDto?>($"/games/{gameId}/messages/next?playerName={Uri.EscapeDataString(playerName)}");
}
