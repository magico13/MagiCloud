namespace MagiCloud.Configuration;
public class AssistantSettings
{
    public string OpenAIKey { get; set; }
    public int MaxIntraSearchResults { get; set; } = 25;
    public int MaxSearchResults { get; set; } = 25;
    public int TextSegmentLength { get; set; } = 100000;
    public string Model { get; set; } = "gpt-5-nano";
    
    /// <summary>
    /// Reasoning effort level: "low", "medium", "high". 
    /// Higher values favor more complete reasoning but use more tokens.
    /// </summary>
    public string ReasoningEffort { get; set; } = "medium";
    
    /// <summary>
    /// Whether to include reasoning summaries in responses.
    /// If true, reasoning summaries will be visible in the chat.
    /// </summary>
    public bool IncludeReasoningSummaries { get; set; } = false;
    
    /// <summary>
    /// Whether to store responses for better context management.
    /// Recommended for better function-calling performance.
    /// </summary>
    public bool StoreResponses { get; set; } = true;
}
