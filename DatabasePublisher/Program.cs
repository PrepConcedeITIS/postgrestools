using Dapper;
using DatabasePublisher;
using Npgsql;

if (GetOptionsValue("help", true) is not null)
{
    WriteHelp();
    return;
}

var config = GetConfig();

var tempConnectionStringBuilder = new NpgsqlConnectionStringBuilder(config.TempConnectionString);

var targetConnectionStringBuilder = new NpgsqlConnectionStringBuilder(config.TargetConnectionString);
if (targetConnectionStringBuilder.Database is null)
    throw new Exception("Target database is not specified.");

await CreateTempDatabaseAsync();
tempConnectionStringBuilder.Pooling = false;
await UpdateTempDatabaseAsync();

return;


void WriteHelp()
{
    Console.WriteLine("options:");
    Console.WriteLine("--target_connection CONNECTION_STRING");
    Console.WriteLine("\tconnection string for target database. This parameter is required!");
    Console.WriteLine("--temp_connection CONNECTION_STRING");
    Console.WriteLine("\tconnection string for temp database. Default: local database inside container.");
    Console.WriteLine("--schema_directory DIRECTORY");
    Console.WriteLine("\tdirectory with sql files. Default: current directory.");
    Console.WriteLine("--generate_publish_file FLAG");
    Console.WriteLine("\tflag to enable generation of publish script file.");
    Console.WriteLine("--output_directory DIRECTORY");
    Console.WriteLine("\tdirectory for publish script files, used with generate_publish_file. Default: bin/Debug/publish");
    Console.WriteLine("--do_not_drop FLAG");
    Console.WriteLine("\tdo not drop temp database.");
    Console.WriteLine("--debug FLAG");
    Console.WriteLine("\tenable debug output.");
}

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

    if (config.Debug)
        Console.WriteLine($"Creating temp database {tempDatabaseName}.");
    
    await using var tempCrateDatabaseConnection = new NpgsqlConnection(tempConnectionStringBuilder.ConnectionString);
    await tempCrateDatabaseConnection.ExecuteAsync($"CREATE DATABASE {tempDatabaseName};");
    
    if (config.Debug)
        Console.WriteLine($"Connecting to temp database.");

    tempConnectionStringBuilder.Database = tempDatabaseName;
}

async Task UpdateTempDatabaseAsync()
{
    if (config.Debug)
        Console.WriteLine("Applying sql files.");
    
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
            if (config.Debug)
                Console.WriteLine($"Applying {fileName} to temp database.");
            
            await tempConnection.ExecuteAsync(content);
        }
    }
}