namespace Towa.ChatGpt.Enums;

public enum ChatPlatform
{
    Twitch,
    Discord
}

public static class ChatPlatformProcess
{
    public static int GetMaxTokensAmount(ChatPlatform platform)
    {
        return platform switch
        {
            ChatPlatform.Twitch => 500,
            ChatPlatform.Discord => 1500,
            _ => 500
        };
    }

    public static int GetMaxCharactersAmount(ChatPlatform platform)
    {
        return platform switch
        {
            ChatPlatform.Twitch => 400,
            ChatPlatform.Discord => 1900,
            _ => 400
        };
    }
}