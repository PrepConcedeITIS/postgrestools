namespace DatabasePublisher;

public class Configuration
{
    public required string TempConnectionString { get; set; }
    public required string TargetConnectionString { get; set; }
    public required string SchemaDirectory { get; set; }
    public required string OutputDirectory { get; set; }
    public bool GeneratePublishFile { get; set; }
    public bool Debug { get; set; }
    public bool DoNotDrop { get; set; }
}