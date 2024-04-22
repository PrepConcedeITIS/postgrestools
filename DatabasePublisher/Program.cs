using Dapper;
using DatabasePublisher;
using Npgsql;

var config = GetConfig();

var tempConnectionStringBuilder = new NpgsqlConnectionStringBuilder(config.TempConnectionString);

var targetConnectionStringBuilder = new NpgsqlConnectionStringBuilder(config.TargetConnectionString);
if (targetConnectionStringBuilder.Database is null)
    throw new Exception("Target database is not specified.");

await CreateTempDatabaseAsync();
tempConnectionStringBuilder.Pooling = false;
await UpdateTempDatabaseAsync();

return;


Configuration GetConfig() => new()
{
    TempConnectionString = GetOptionsValue("temp_connection") ??
                           "Server=127.0.0.1;Port=9797;Database=;User Id=docker;Password=docker;", //TODO: default TempConnectionString
    TargetConnectionString = GetOptionsValue("target_connection") ??
                             throw new Exception("Connection string is not specified"),
    SchemaDirectory = GetOptionsValue("schema_directory") ?? ".",
    OutputDirectory = GetOptionsValue("output_directory") ?? "bin/Debug/publish",
    GeneratePublishFile = GetOptionsValue("generate_publish_file", true) is not null,
    Debug = GetOptionsValue("debug", true) is not null,
    DoNotDrop = GetOptionsValue("do_not_drop", true) is not null
};

string? GetOptionsValue(string name, bool isFlag = false) =>
    args.SkipWhile(arg => !arg.StartsWith($"--{name}")).Skip(isFlag ? 0 : 1).FirstOrDefault() ??
    Environment.GetEnvironmentVariable(name);

async Task CreateTempDatabaseAsync()
{
    var tempDatabaseName = $"{targetConnectionStringBuilder.Database}_{DateTime.UtcNow:yyyyMMddHHmmss}_temp";

    await using var tempCrateDatabaseConnection = new NpgsqlConnection(tempConnectionStringBuilder.ConnectionString);
    await tempCrateDatabaseConnection.ExecuteAsync($"CREATE DATABASE {tempDatabaseName};");

    tempConnectionStringBuilder.Database = tempDatabaseName;
}

async Task UpdateTempDatabaseAsync()
{
    await using var tempConnection = new NpgsqlConnection(tempConnectionStringBuilder.ConnectionString);

    var directories = Directory.GetDirectories(config.SchemaDirectory)
        .Where(directory => Path.GetFileName(directory) is not "bin" and not "obj")
        .OrderByDescending(directory => Path.GetFileName(directory).ToLower() is "schemas");
    
    foreach (var directory in directories)
    {
        var files = Directory.GetFiles(directory, "*.sql", SearchOption.AllDirectories)
            .Where(file => !file.EndsWith(".postdeploy.sql"))
            .ToAsyncEnumerable()
            .SelectAwait(async file => (FileName: file, Content: (await File.ReadAllTextAsync(file)).ToLower()))
            .OrderBy(item => item.Content.Contains("create view", StringComparison.OrdinalIgnoreCase) ||
                             item.Content.Contains("create materialized view", StringComparison.OrdinalIgnoreCase))
            .ThenBy(item => item.Content.Contains("foreign key", StringComparison.InvariantCultureIgnoreCase))
            .ThenBy(item => item.FileName);

        await foreach (var (fileName, content) in files)
        {
            await tempConnection.ExecuteAsync(content);
        }
    }
}