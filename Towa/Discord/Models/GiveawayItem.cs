using System.ComponentModel.DataAnnotations;

namespace Towa.Discord.Models;

public class GiveawayItem
{
    [Key] public ulong MessageId { get; set; }
    public ulong ChannelId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int WinnerCount { get; set; }
    public bool IsInfinite { get; set; }
    public long Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public bool IsOver { get; set; }
}