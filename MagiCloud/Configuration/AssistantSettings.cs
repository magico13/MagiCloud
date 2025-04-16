namespace MagiCloud.Configuration;
public class AssistantSettings
{
    public string OpenAIKey { get; set; }
    public int MaxIntraSearchResults { get; set; } = 25;
    public int MaxSearchResults { get; set; } = 25;
    public int TextSegmentLength { get; set; } = 100000;
    public string Model { get; set; } = "gpt-4.1-nano";
}
