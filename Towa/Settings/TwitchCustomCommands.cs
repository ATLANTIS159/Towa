namespace Towa.Settings;

public class TwitchCustomCommands
{
    public List<CustomCommand> CustomCommands { get; set; } = new();
}

public class CustomCommand
{
    public string ChatCommand { get; set; } = "test";
    public string Alias { get; set; } = "test1";
    public bool IsActive { get; set; } = true;
    public bool IsForEveryone { get; set; } = true;
    public string Message { get; set; } = "Test Message";
}