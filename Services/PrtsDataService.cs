using Microsoft.Data.Sqlite;
using System.Data;
using System;
using Dapper;

namespace ArkPlotWpf.Services;
public class PrtsDataService
{
    private readonly string _connectionString;

    public PrtsDataService(string dbPath = "PrtsAssets.db")
    {
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using IDbConnection connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
                CREATE TABLE IF NOT EXISTS PortraitLinks (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT UNIQUE NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PortraitLinkAliases (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    portrait_link_id INTEGER NOT NULL,
                    order INTEGER NOT NULL,
                    alias TEXT NOT NULL,
                    FOREIGN KEY(portrait_link_id) REFERENCES PortraitLinks(id)
                );

                CREATE TABLE IF NOT EXISTS CharacterPortraits (
                    alias TEXT PRIMARY KEY NOT NULL,
                    url TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Images (
                    key TEXT PRIMARY KEY NOT NULL,
                    url TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Audio (
                    key TEXT PRIMARY KEY NOT NULL,
                    url TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Overrides (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    data TEXT NOT NULL
                );
            ";
        command.ExecuteNonQuery();
    }

    public void AddPortraitLink(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO PortraitLinks (name) VALUES (@name)";
        command.Parameters.AddWithValue("@name", name);
        command.ExecuteNonQuery();
        connection.Execute(command.CommandText);
    }

    public List<string> GetAllPortraitLinks()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM PortraitLinks";
        using var reader = command.ExecuteReader();
        var list = new List<string>();
        while (reader.Read())
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    public void AddPortraitAlias(int portraitLinkId, int order, string alias)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO PortraitLinkAliases (portrait_link_id, order, alias) VALUES (@id, @order, @alias)";
        command.Parameters.AddWithValue("@id", portraitLinkId);
        command.Parameters.AddWithValue("@order", order);
        command.Parameters.AddWithValue("@alias", alias);
        command.ExecuteNonQuery();
    }

    public void AddCharacterPortrait(string alias, string url)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO CharacterPortraits (alias, url) VALUES (@alias, @url)";
        command.Parameters.AddWithValue("@alias", alias);
        command.Parameters.AddWithValue("@url", url);
        command.ExecuteNonQuery();
    }
    public int GetPortraitLinkId(string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM PortraitLinks WHERE name = @name";
        command.Parameters.AddWithValue("@name", name);
        var result = command.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : -1;
    }

    public string GetCharacterPortraitUrl(string alias)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT url FROM CharacterPortraits WHERE alias = @alias";
        command.Parameters.AddWithValue("@alias", alias);
        var result = command.ExecuteScalar();
        return result?.ToString();
    }

    public string GetPortraitUrl(string inputKey)
    {
        var (portraitCode, index) = ParseInputKey(inputKey);

        if (portraitCode == "-1") return null;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = @"
                SELECT cp.url
                FROM CharacterPortraits cp
                JOIN PortraitLinkAliases pla ON cp.alias = pla.alias
                JOIN PortraitLinks pl ON pla.portrait_link_id = pl.id
                WHERE pl.name = @portraitCode AND pla.order = @index
                LIMIT 1
            ";
        command.Parameters.AddWithValue("@portraitCode", portraitCode);
        command.Parameters.AddWithValue("@index", index);
        var result = command.ExecuteScalar();
        return result?.ToString();
    }

    private (string portraitCode, int index) ParseInputKey(string inputKey)
    {
        throw new NotImplementedException("ParseInputKey needs implementation");
    }
}
