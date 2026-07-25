namespace SteamShuffle.CoreModels;

public class StoreDetails
{
    public int AppId { get; set; }
    public string? Name { get; set; }
    public string? HeaderImageUrl { get; set; }
    public bool IsFree { get; set; }
    public decimal? PriceCad { get; set; }
    public List<string> Genres { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}
