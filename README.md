# Towa

- [Towa](#Towa)
  - [About](#about)
  - [Usage](#usage)
  - [Config File](#config-file)
  - [Commands](#commands)

## About

This is a bot project for the Twitch channel [etacarinae_](https://twitch.tv/etacarinae_).

This project contains the following functionality:

- Twitch bot
- Discord bot
- Ability to download live stream from Twitch
- Built-in ChatGPT for Discord text channels and Twitch chat
- Getting the rank of a League of Legends account and displaying it in the Twitch chat using commands
- Custom commands for Twitch chat
- Creating and tracking giveaways in Discord channels
- Notifications about the start of a stream on Twitch
- Custom Discord commands

## Usage

To access the bot files you will need to use the following paths:

- `/towa/Config` - Path to bot configuration files
- `/towa/Logs` - Path to bot logs
- `/towa/Database` - Path to the bot database for ChatGPT operation, Discord giveaways
- `/towa/Downloader` - Path to the folder where live broadcasts are downloaded

The bot is designed to run in a docker container.

To create a bot container use the following command:

```sh
docker run -d --name towa -v /towa/Config:/path/to/folder/ -v /towa/Logs:/path/to/folder/ -v /towa/Database:/path/to/folder/ -v /towa/Downloader:/path/to/folder/ --restart unless-stopped atlantis159/towa:latest
```
## Config File

After the first launch, a configuration file `CoreSettings.etaConfig` will be generated along the path attached to `/towa/Config`.

The configuration file contains the following parameters:

- Discord
  - Token - Discord bot token
  - ServerId - ID of the Discord server to which the bot connects (you must first invite the bot to the server)
  - Database - Name of the database that stores information about current giveaways
  - FollowerRoleId - ID of the role that the bot will automatically assign to new members of the Discord server
  - IsNotificationsActive - Enables or disables notifications about the start of a stream on Twitch
  - NotificationChannelId - ID of the Discord text channel where the bot will send notifications about the start of the stream on Twitch
  - GiveawayEmote - The emoji that is needed to participate in the drawing and which the server will put in the reaction of the giveaway
  - UtcHourCorrection - The time zone in which the draw is taking place (server date is stored in UTC time zone)
- Twitch
  - BotName - Twitch bot name
  - ClientId - Bot client ID
  - OAuthKey - Bot authorization key
  - JoinChannel - Twitch channel to which the bot will connect
  - IsCommandsEnabled - Enabling and disabling custom chat commands
- StreamDownloaderSettings
  - IsDownloaderActive - Enable or disable downloading of a live stream
  - UniqueId - Unique session ID from cookies on the Twitch website
  - AuthToken - Session authorization token from cookies on the Twitch website
- ChatGpt
  - IsActiveInTwitch - Enabling and disabling ChatGPT in Twitch chat
  - IsActiveInDiscord - Enable or disable ChatGPT in Discord channels
  - Token - ChatGPT API Token
  - Database - Name of the database that stores chat history
  - ChatGptModel - ChatGPT language model
  - Temperature - 
  - PresencePenalty - 
- Riot
  - ApiKey - API key for Riot services
  - RiotAccounts
    - IsActive - Enable or disable the display of information for this account
    - ShowInGlobalCommand - Enabling or disabling the display of information for this account through the use of the global command (!elo)
    - Region - Account region
    - AdditionalMessage - Additional message that comes after the rank information
