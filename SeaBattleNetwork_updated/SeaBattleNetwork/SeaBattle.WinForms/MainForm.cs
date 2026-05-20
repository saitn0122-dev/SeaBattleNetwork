using System.Data;

namespace SeaBattle.WinForms;

public sealed class MainForm : Form
{
    private readonly GameEngine _player = new();
    private readonly GameEngine _enemyBot = new();
    private readonly BotPlayer _bot = new();
    private readonly NetworkClient _network = new();
    private readonly StatisticsRepository _stats = new();

    private readonly PictureBox[,] _myCells = new PictureBox[GameEngine.Size, GameEngine.Size];
    private readonly PictureBox[,] _enemyCells = new PictureBox[GameEngine.Size, GameEngine.Size];
    private readonly Label _status = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Label _timerLabel = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly TextBox _nameBox = new() { Text = "Player1", Width = 130 };
    private readonly TextBox _gameIdBox = new() { Width = 100, PlaceholderText = "Game ID" };
    private readonly ComboBox _modeBox = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _difficultyBox = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DataGridView _statsGrid = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private readonly System.Windows.Forms.Timer _turnTimer = new() { Interval = 1000 };
    private readonly System.Windows.Forms.Timer _pollTimer = new() { Interval = 1000 };

    private bool _myTurn;
    private int _secondsLeft = 30;
    private string _gameId = "";
    private string _opponentName = "";
    private DateTime _gameStart;

    private bool _placementMode;
    private int _placingShipIndex;
    private bool _placeHorizontal = true;

    public MainForm()
    {
        Text = "Морской бой — сетевой режим и боты";
        MinimumSize = new Size(1050, 720);
        BackColor = Color.FromArgb(241, 247, 255);
        BuildUi();
        NewLocalGame();
        _turnTimer.Tick += TurnTimerTick;
        _pollTimer.Tick += async (_, _) => await PollNetworkAsync();
    }

    private void BuildUi()
    {
        var menu = new MenuStrip();
        var gameMenu = new ToolStripMenuItem("Игра");
        gameMenu.DropDownItems.Add("Новая игра", null, (_, _) => NewLocalGame());
        gameMenu.DropDownItems.Add("Статистика", null, async (_, _) => await LoadStatsAsync());
        gameMenu.DropDownItems.Add("Выход", null, (_, _) => Close());
        menu.Items.Add(gameMenu);
        MainMenuStrip = menu;
        Controls.Add(menu);

        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };
        var gameTab = new TabPage("Игра") { BackColor = BackColor };
        var statsTab = new TabPage("Статистика") { BackColor = BackColor };
        tabs.TabPages.Add(gameTab);
        tabs.TabPages.Add(statsTab);
        Controls.Add(tabs);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(16) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        gameTab.Controls.Add(root);

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        var manualButton = Btn("Ручная расстановка", (_, _) => StartManualPlacement());
        var rotateButton = Btn("Повернуть", (_, _) => RotateShip());
        var autoButton = Btn("Авторасстановка", (_, _) => { _placementMode = false; _player.AutoPlaceShips(); DrawBoards(); SetStatus("Корабли расставлены автоматически. Можно начинать игру."); });
        var newButton = Btn("Новая игра с ботом", (_, _) => NewLocalGame());
        var createButton = Btn("Создать сетевую", async (_, _) => await CreateNetworkGameAsync());
        var joinButton = Btn("Подключиться", async (_, _) => await JoinNetworkGameAsync());
        _modeBox.Items.AddRange(new object[] { "Игра с ботом", "Сетевая игра" });
        _modeBox.SelectedIndex = 0;
        _difficultyBox.Items.AddRange(Enum.GetNames(typeof(BotDifficulty)));
        _difficultyBox.SelectedIndex = 1;
        top.Controls.AddRange(new Control[]
        {
            new Label { Text = "Имя:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) },
            _nameBox, _modeBox,
            new Label { Text = "Бот:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) },
            _difficultyBox, newButton, manualButton, rotateButton, autoButton, createButton, _gameIdBox, joinButton
        });
        root.Controls.Add(top, 0, 0);

        var boards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        boards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        boards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.Controls.Add(boards, 0, 1);
        boards.Controls.Add(BoardPanel("Ваше поле", _myCells, false), 0, 0);
        boards.Controls.Add(BoardPanel("Поле противника", _enemyCells, true), 1, 0);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        bottom.Controls.Add(_status);
        bottom.Controls.Add(new Label { Text = "   Время хода:", AutoSize = true, Padding = new Padding(10, 0, 0, 0) });
        bottom.Controls.Add(_timerLabel);
        root.Controls.Add(bottom, 0, 2);

        statsTab.Controls.Add(_statsGrid);
    }

    private static Button Btn(string text, EventHandler click)
    {
        var b = new Button { Text = text, Height = 34, AutoSize = true, BackColor = Color.FromArgb(33, 150, 243), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Margin = new Padding(6) };
        b.FlatAppearance.BorderSize = 0;
        b.Click += click;
        return b;
    }

    private Control BoardPanel(string title, PictureBox[,] cells, bool enemy)
    {
        var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10, FontStyle.Bold), Padding = new Padding(10) };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = GameEngine.Size, ColumnCount = GameEngine.Size, BackColor = Color.FromArgb(25, 42, 86) };
        for (int i = 0; i < GameEngine.Size; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10));
        }
        for (int y = 0; y < GameEngine.Size; y++)
        for (int x = 0; x < GameEngine.Size; x++)
        {
            var cell = new PictureBox
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(2),
                BackColor = Color.FromArgb(144, 202, 249),
                BorderStyle = BorderStyle.FixedSingle,
                Tag = new Point(x, y),
                Cursor = Cursors.Hand
            };
            if (enemy) cell.Click += EnemyCellClick;
            else cell.Click += MyCellClick;
            cells[x, y] = cell;
            grid.Controls.Add(cell, x, y);
        }
        group.Controls.Add(grid);
        return group;
    }

    private void NewLocalGame()
    {
        _modeBox.SelectedIndex = 0;
        _placementMode = false;
        _player.AutoPlaceShips();
        _enemyBot.AutoPlaceShips();
        _bot.Difficulty = Enum.Parse<BotDifficulty>(_difficultyBox.Text);
        _myTurn = true;
        _gameStart = DateTime.Now;
        _gameId = "BOT";
        _opponentName = "Bot";
        ResetTurnTimer();
        _pollTimer.Stop();
        SetStatus("Игра с ботом началась. Ваш ход. Для ручной расстановки нажмите 'Ручная расстановка'.");
        DrawBoards();
    }

    private void StartManualPlacement()
    {
        _placementMode = true;
        _placingShipIndex = 0;
        _placeHorizontal = true;
        _myTurn = false;
        _player.Clear();
        _enemyBot.AutoPlaceShips();
        _pollTimer.Stop();
        _turnTimer.Stop();
        DrawBoards();
        SetPlacementStatus();
    }

    private void RotateShip()
    {
        _placeHorizontal = !_placeHorizontal;
        if (_placementMode) SetPlacementStatus();
    }

    private void MyCellClick(object? sender, EventArgs e)
    {
        if (!_placementMode || sender is not PictureBox box || box.Tag is not Point p) return;

        var size = GameEngine.RequiredShipSizes[_placingShipIndex];
        if (!_player.TryPlaceShip(p.X, p.Y, size, _placeHorizontal))
        {
            SetStatus("Сюда нельзя поставить корабль: он выходит за поле или касается другого корабля.");
            return;
        }

        _placingShipIndex++;
        DrawBoards();

        if (_placingShipIndex >= GameEngine.RequiredShipSizes.Count)
        {
            _placementMode = false;
            _myTurn = true;
            _gameStart = DateTime.Now;
            ResetTurnTimer();
            SetStatus("Все корабли расставлены. Можно играть.");
            return;
        }

        SetPlacementStatus();
    }

    private void SetPlacementStatus()
    {
        var size = GameEngine.RequiredShipSizes[_placingShipIndex];
        var direction = _placeHorizontal ? "горизонтально" : "вертикально";
        SetStatus($"Ручная расстановка: поставьте корабль на {size} клетки, направление: {direction}.");
    }

    private bool ShipsReady()
    {
        if (_player.MyShips.Count == GameEngine.RequiredShipSizes.Count) return true;
        SetStatus("Сначала расставьте все корабли: вручную или кнопкой 'Авторасстановка'.");
        return false;
    }

    private async Task CreateNetworkGameAsync()
    {
        if (!ShipsReady()) return;
        try
        {
            _modeBox.SelectedIndex = 1;
            _player.ClearEnemyView();
            var state = await _network.CreateGameAsync(_nameBox.Text.Trim());
            _gameId = state?.GameId ?? "";
            _gameIdBox.Text = _gameId;
            _opponentName = state?.Player2 ?? "";
            _myTurn = true;
            _gameStart = DateTime.Now;
            _pollTimer.Start();
            ResetTurnTimer();
            SetStatus($"Сетевая игра создана. ID: {_gameId}. Ожидание второго игрока.");
            DrawBoards();
        }
        catch (Exception ex) { SetStatus("Ошибка сети: " + ex.Message); }
    }

    private async Task JoinNetworkGameAsync()
    {
        if (!ShipsReady()) return;
        try
        {
            _modeBox.SelectedIndex = 1;
            _player.ClearEnemyView();
            var state = await _network.JoinGameAsync(_gameIdBox.Text.Trim(), _nameBox.Text.Trim());
            _gameId = state?.GameId ?? _gameIdBox.Text.Trim();
            _opponentName = state?.Player1 ?? "";
            _myTurn = false;
            _gameStart = DateTime.Now;
            _pollTimer.Start();
            ResetTurnTimer();
            SetStatus("Вы подключились. Ждите ход соперника.");
            DrawBoards();
        }
        catch (Exception ex) { SetStatus("Ошибка подключения: " + ex.Message); }
    }

    private async void EnemyCellClick(object? sender, EventArgs e)
    {
        if (_placementMode) { SetStatus("Сначала завершите расстановку кораблей."); return; }
        if (!_myTurn || sender is not PictureBox box || box.Tag is not Point p) return;
        if (_player.EnemyView[p.X, p.Y] != CellState.Empty) return;

        if (_modeBox.SelectedIndex == 0) await PlayerShotBotAsync(p);
        else await PlayerNetworkShotAsync(p);
    }

    private async Task PlayerShotBotAsync(Point p)
    {
        var result = _enemyBot.ReceiveShot(p.X, p.Y);
        _player.MarkEnemyShot(p.X, p.Y, result);
        DrawBoards();
        if (result == ShotResult.Win)
        {
            await FinishGameAsync(_nameBox.Text, "Bot");
            return;
        }
        if (result == ShotResult.Miss)
        {
            _myTurn = false;
            SetStatus("Мимо. Ход бота.");
            await Task.Delay(600);
            BotTurn();
        }
        else SetStatus("Попадание! Ходите ещё.");
        ResetTurnTimer();
    }

    private async Task PlayerNetworkShotAsync(Point p)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_opponentName))
            {
                var state = await _network.GetStateAsync(_gameId);
                _opponentName = state?.Player1 == _nameBox.Text.Trim() ? state?.Player2 ?? "" : state?.Player1 ?? "";
            }

            if (string.IsNullOrWhiteSpace(_opponentName))
            {
                SetStatus("Второй игрок ещё не подключился.");
                return;
            }

            await _network.SendShotAsync(new ShotDto(_gameId, _nameBox.Text.Trim(), p.X, p.Y, "Shot", _opponentName));
            _myTurn = false;
            SetStatus("Ход отправлен. Ожидание результата от соперника.");
            ResetTurnTimer();
        }
        catch (Exception ex) { SetStatus("Ошибка отправки хода: " + ex.Message); }
    }

    private void BotTurn()
    {
        while (!_myTurn)
        {
            var shot = _bot.NextShot(_enemyBot.EnemyView);
            var result = _player.ReceiveShot(shot.X, shot.Y);
            _enemyBot.MarkEnemyShot(shot.X, shot.Y, result);
            _bot.AfterShot(shot, result);
            DrawBoards();
            if (result == ShotResult.Win)
            {
                _ = FinishGameAsync("Bot", _nameBox.Text);
                return;
            }
            if (result == ShotResult.Miss)
            {
                _myTurn = true;
                SetStatus("Бот промахнулся. Ваш ход.");
            }
            else SetStatus("Бот попал и стреляет ещё.");
        }
        ResetTurnTimer();
    }

    private async Task PollNetworkAsync()
    {
        if (string.IsNullOrWhiteSpace(_gameId) || _modeBox.SelectedIndex != 1) return;
        try
        {
            var msg = await _network.GetNextMessageAsync(_gameId, _nameBox.Text.Trim());
            if (msg == null) return;

            if (msg.Result == "Shot")
            {
                _opponentName = msg.PlayerName;
                var result = _player.ReceiveShot(msg.X, msg.Y);
                await _network.SendShotAsync(new ShotDto(_gameId, _nameBox.Text.Trim(), msg.X, msg.Y, result.ToString(), msg.PlayerName));
                _myTurn = result == ShotResult.Miss;
                SetStatus(_myTurn ? "Соперник промахнулся. Ваш ход." : "Соперник попал. Он ходит ещё.");
                DrawBoards();
                ResetTurnTimer();
                if (result == ShotResult.Win) await FinishGameAsync(msg.PlayerName, _nameBox.Text.Trim());
                return;
            }

            if (Enum.TryParse<ShotResult>(msg.Result, out var shotResult))
            {
                _player.MarkEnemyShot(msg.X, msg.Y, shotResult);
                DrawBoards();
                if (shotResult == ShotResult.Win)
                {
                    await FinishGameAsync(_nameBox.Text.Trim(), _opponentName);
                    return;
                }
                _myTurn = shotResult is ShotResult.Hit or ShotResult.Killed;
                SetStatus(_myTurn ? "Вы попали. Ходите ещё." : "Вы промахнулись. Ход соперника.");
                ResetTurnTimer();
            }
        }
        catch { /* сервер может быть временно недоступен */ }
    }

    private void TurnTimerTick(object? sender, EventArgs e)
    {
        _secondsLeft--;
        _timerLabel.Text = _secondsLeft.ToString();
        if (_secondsLeft > 0) return;
        if (_myTurn)
        {
            _myTurn = false;
            SetStatus("Время вышло. Ход переходит сопернику.");
            if (_modeBox.SelectedIndex == 0) BotTurn();
        }
        ResetTurnTimer();
    }

    private void ResetTurnTimer()
    {
        _secondsLeft = 30;
        _timerLabel.Text = _secondsLeft.ToString();
        _turnTimer.Start();
    }

    private async Task FinishGameAsync(string winner, string loser)
    {
        _turnTimer.Stop();
        _pollTimer.Stop();
        _myTurn = false;
        SetStatus($"Игра окончена. Победитель: {winner}");
        try
        {
            await _stats.SaveGameAsync(winner == _nameBox.Text ? winner : loser, winner == _nameBox.Text ? loser : winner, winner, _gameStart, DateTime.Now);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Статистика не сохранена: " + ex.Message, "MySQL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task LoadStatsAsync() => _statsGrid.DataSource = await _stats.LoadStatsAsync();

    private void DrawBoards()
    {
        DrawField(_myCells, _player.MyField, true);
        DrawField(_enemyCells, _player.EnemyView, false);
    }

    private static void DrawField(PictureBox[,] cells, CellState[,] field, bool showShips)
    {
        for (int x = 0; x < GameEngine.Size; x++)
        for (int y = 0; y < GameEngine.Size; y++)
        {
            cells[x, y].BackColor = field[x, y] switch
            {
                CellState.Ship when showShips => Color.FromArgb(69, 90, 100),
                CellState.Miss => Color.White,
                CellState.Hit => Color.OrangeRed,
                CellState.Killed => Color.DarkRed,
                _ => Color.FromArgb(144, 202, 249)
            };
        }
    }

    private void SetStatus(string text) => _status.Text = text;
}
