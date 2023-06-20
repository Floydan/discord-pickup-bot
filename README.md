![build](https://github.com/Floydan/discord-pickup-bot/workflows/build/badge.svg?branch=main) 
[![Join the chat at https://gitter.im/discord-pickup-bot/community](https://badges.gitter.im/discord-pickup-bot/community.svg)](https://gitter.im/discord-pickup-bot/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Maintainability](https://api.codeclimate.com/v1/badges/66275c62b74e27401e3b/maintainability)](https://codeclimate.com/github/Floydan/discord-pickup-bot/maintainability)
[![Discord](https://img.shields.io/badge/discord-chat-blue?logo=discord)](https://discord.gg/pkPMjWV)


- [Discord pickup game bot](#discord-pickup-game-bot)
  - [Setup](#setup)
  - [Testing/using](#testingusing)
  - [Pickups and Bot commands](#pickups-and-bot-commands)
  - [Command examples](#command-examples)
  
  
# Discord pickup game bot

A discord bot for managing pickup games.

The bot stores all data in an Azure Table but this can easily be changed using dependency injections and craeting new repositories inheriting from these interfaces:
+ IDuelChallengeRepository
+ IDuelMatchRepository
+ IDuelPlayerRepository
+ IFlaggedSubscriberRepository
+ IQueueRepository
+ IServerRepository
+ ISubscriberActivitiesRepository

You can change which implementation is used by changing the dependency injection in `PickupBot/Program.cs`

## Setup
Edit the `appSettings.json` file in the `PickupBot` folder

Then add this to the json file substituting the values
```javascript
{
    "ConnectionStrings": {
        "StorageConnectionString": "<PlaceHolder for connection: AzureStorage>"
    },
    "PickupBot": {
        "DiscordToken": "TOKEN",
        "RCONServerPassword": "RCON-PASSWORD",
        "RCONHost": "RCON-HOST",
        "RCONPort": "RCON-PORT",
        "CommandPrefix": "!",
        "GoogleTranslateAPIKey": "GOOGLE-TRANSLATE-KEY"
    },
    "Encryption": {
        "Key": "string with 32 characters",
        "IV": "string with 16 characters" 
    } 
}
```

- The RCON Prefixed variables enable the bot to query and interact with an RCON enabled game server.
  + With the latest updates you can now add servers and store them in the DB with an encrypted rcon password so the prefixed app settings variables are only used as a fallback from now on.
  + The Encryption uses the Encryption.Key and Ecryption.IV app settings
- Google translate API Key enables the bot to translate messages in discord
  + To translate a message just add a **reaction** in the form of a country flag that represents the language you wish the message to be translated to.
  + Supported languages are the Scandinavian languages, major european languages and russian.
- CommandPrefix sets what character/characters mus precede a command
  + This is very useful to have a different `CommandPrefix` for different environments.

The `settings.job` can be ignored since this discord bot needs to be run continuously.

## Testing/using
To add the bot to your discord server just go here to authorize and give the bot access:

[Bot Authorization](https://discordapp.com/api/oauth2/authorize?client_id=696658931434389505&permissions=285215793&scope=bot)

After the bot has been added to your server you can run `!help` to see all the available commands.

## Pickups and Bot commands
Use the `!help` command in the #pickup channel to get familiar with the pickup bot commands

`!subscribe` to get notifications when pickups are promoted

The pickup bot has has translation support.
To translate a message just add a **reaction**  with a country flag that represents the language you want to translate the message to.

_Supported languages are:_

:sweden: :norway: :denmark: :finland: :de: :netherlands: :es: :it: :fr: :poland: :portugal: :greece: :gb: :us: :ru: 

## Command examples
When creating a pickup queue you use the `!create` command

For example
`!create queuename 4`

Additional operator flags can be added, for instance
`!create "sunday night pickup" 4 -norcon -game Quake III -host server.address.com -port 12345

Available operators are:
- `-rcon`/`-norcon`
- `-game`
- `-host`
- `-port`
- `-coop`
- `-nocoop`
- `-novoice`
- `-gamemode`/`-gmode`
- `-gmode`
- `-game`
- `-teamsize`

The operators can also be used to update a pickup queue after it has been created with the same operators
`!update "sunday night pickup" -teamsize 3`
