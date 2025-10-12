namespace MagiCloud.Configuration;

public class GeneralSettings
{
    public string SendGridKey { get; set; }
    public string SendGridFromAddress { get; set; }
    public string SendGridFromName { get; set; }

    public string BlazoriseProductToken { get; set; } = string.Empty;
}
