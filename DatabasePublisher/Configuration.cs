namespace DatabasePublisher;

public class Configuration
{
    public required string TempConnectionString { get; init; }

    public required string TargetConnectionString { get; init; }

    public required string SchemaDirectory { get; init; }

    public required string OutputDirectory { get; init; }

    public bool GeneratePublishFile { get; init; }

    public bool Debug { get; init; }

    public bool DoNotDrop { get; init; }
}
