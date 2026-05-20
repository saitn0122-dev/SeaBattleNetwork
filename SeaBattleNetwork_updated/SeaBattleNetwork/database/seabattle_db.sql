CREATE DATABASE IF NOT EXISTS seabattle_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE seabattle_db;

CREATE TABLE IF NOT EXISTS players (
    id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    wins INT NOT NULL DEFAULT 0,
    losses INT NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS games (
    id INT AUTO_INCREMENT PRIMARY KEY,
    player1_id INT NOT NULL,
    player2_id INT NOT NULL,
    winner_id INT NULL,
    start_time DATETIME NOT NULL,
    end_time DATETIME NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Finished',
    FOREIGN KEY (player1_id) REFERENCES players(id),
    FOREIGN KEY (player2_id) REFERENCES players(id),
    FOREIGN KEY (winner_id) REFERENCES players(id)
);

CREATE TABLE IF NOT EXISTS moves (
    id INT AUTO_INCREMENT PRIMARY KEY,
    game_id INT NOT NULL,
    player_id INT NOT NULL,
    x INT NOT NULL,
    y INT NOT NULL,
    result VARCHAR(50) NOT NULL,
    move_time DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (game_id) REFERENCES games(id),
    FOREIGN KEY (player_id) REFERENCES players(id)
);
