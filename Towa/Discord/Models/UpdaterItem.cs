namespace Towa.Discord.Models;

public class UpdaterItem
{
    public ulong MessageId { get; init; }
    public CancellationTokenSource TokenSource { get; init; } = new();
}