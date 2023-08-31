namespace Goggles;

public class GogglesConfiguration
{
    public int MaxTextLength { get; set; } = 1000000;
    public bool EnableOCR { get; set; } = false;
    public bool EnableAudioTranscription { get; set; } = false;

    public AzureOCRConfiguration AzureOCRConfiguration { get; set; } = new();
    public WhisperTranscriptionConfiguration WhisperTranscriptionConfiguration { get; set; } = new();
}
