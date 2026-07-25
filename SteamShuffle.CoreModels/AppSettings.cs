namespace SteamShuffle.CoreModels;

public class AppSettings
{
    public string SteamApiKey { get; set; } = string.Empty;
    public string SteamId64 { get; set; } = string.Empty;

    /// <summary>Steam "cc" country code used for store pricing. "ca" = Canada / CAD.</summary>
    public string CountryCode { get; set; } = "ca";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SteamApiKey) && !string.IsNullOrWhiteSpace(SteamId64);
}
