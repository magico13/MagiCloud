namespace Goggles;

public class GogglesConfiguration
{
    public int MaxTextLength { get; set; } = 1000000;
    public bool EnableOCR { get; set; } = true;

    public AzureOCRConfiguration AzureOCRConfiguration { get; set; } = new();
    public WhisperTranscriptionConfiguration WhisperAPIConfiguration { get; set; } = new();
}
