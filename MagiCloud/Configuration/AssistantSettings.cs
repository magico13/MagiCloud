namespace MagiCloud.Configuration;
public class AssistantSettings
{
    public string OpenAIKey { get; set; }
    public int MaxIntraSearchResults { get; set; } = 10;
    public int MaxSearchResults { get; set; } = 10;
    public int TextSegmentLength { get; set; } = 10000;
}
