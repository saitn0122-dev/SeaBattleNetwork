using System.Data;
using MySqlConnector;

namespace SeaBattle.WinForms;

public sealed class StatisticsRepository
{
    private readonly string _connectionString = "Server=localhost;Port=3306;Database=seabattle_db;Uid=root;Pwd=12345;";

    public async Task SaveGameAsync(string player, string opponent, string winner, DateTime start, DateTime end)
    {
        try
        {
            await using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandText = @"
INSERT INTO players(name) VALUES(@p) ON DUPLICATE KEY UPDATE name=name;
INSERT INTO players(name) VALUES(@o) ON DUPLICATE KEY UPDATE name=name;
INSERT INTO games(player1_id, player2_id, winner_id, start_time, end_time, status)
SELECT p1.id, p2.id, w.id, @start, @end, 'Finished'
FROM players p1, players p2, players w
WHERE p1.name=@p AND p2.name=@o AND w.name=@w;
UPDATE players SET wins = wins + 1 WHERE name=@w;
UPDATE players SET losses = losses + 1 WHERE name<>@w AND name IN (@p,@o);";
            cmd.Parameters.AddWithValue("@p", player);
            cmd.Parameters.AddWithValue("@o", opponent);
            cmd.Parameters.AddWithValue("@w", winner);
            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Статистика не сохранена: " + ex.Message, "MySQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public async Task<DataTable> LoadStatsAsync()
    {
        var table = new DataTable();
        try
        {
            await using var con = new MySqlConnection(_connectionString);
            await con.OpenAsync();
            await using var cmd = new MySqlCommand("SELECT name AS Игрок, wins AS Победы, losses AS Поражения FROM players ORDER BY wins DESC", con);
            await using var reader = await cmd.ExecuteReaderAsync();
            table.Load(reader);
        }
        catch
        {
            table.Columns.Add("Сообщение");
            table.Rows.Add("Не удалось подключиться к MySQL. Проверьте строку подключения.");
        }
        return table;
    }
}
