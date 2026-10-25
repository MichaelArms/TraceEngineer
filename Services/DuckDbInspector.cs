using DuckDB.NET.Data;

namespace TraceEngineer.Services;

public class DuckDbInspector
{
    public async Task<DatabaseSchema> InspectAsync(string filePath)
    {
        var schema = new DatabaseSchema { FilePath = filePath };

        // FIX: Rimosso AccessMode=ReadOnly
        var connectionString = $"Data Source={filePath}";

        await using var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();

        // 1. Elenco tabelle
        var tables = new List<string>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'main'";
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        // 2. Per ogni tabella, colonne e tipi
        foreach (var table in tables)
        {
            var tableInfo = new TableSchema { Name = table };

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT column_name, data_type FROM information_schema.columns WHERE table_name = '{table}' ORDER BY ordinal_position";

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tableInfo.Columns.Add(new ColumnSchema
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1)
                });
            }

            // 3. Conta righe
            await using var cmdCount = connection.CreateCommand();
            cmdCount.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            tableInfo.RowCount = Convert.ToInt64(await cmdCount.ExecuteScalarAsync());

            schema.Tables.Add(tableInfo);
        }

        return schema;
    }

    public async Task<List<Dictionary<string, object>>> PreviewTableAsync(string filePath, string tableName, int limit = 10)
    {
        var rows = new List<Dictionary<string, object>>();
        
        // FIX: Rimosso AccessMode=ReadOnly
        var connectionString = $"Data Source={filePath}";

        await using var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT * FROM \"{tableName}\" LIMIT {limit}";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return rows;
    }
}

public class DatabaseSchema
{
    public string FilePath { get; set; } = string.Empty;
    public List<TableSchema> Tables { get; set; } = new();
}

public class TableSchema
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnSchema> Columns { get; set; } = new();
    public long RowCount { get; set; }
}

public class ColumnSchema
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
}