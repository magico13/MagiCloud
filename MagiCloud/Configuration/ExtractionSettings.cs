namespace MagiCloud.Configuration;

public class ExtractionSettings
{
    public int MaxTextLength { get; set; } = 1000000;
    public bool EnableOCR { get; set; } = true;
}
