![build](https://github.com/Floydan/discord-pickup-bot/workflows/build/badge.svg?branch=master) 
[![Join the chat at https://gitter.im/discord-pickup-bot/community](https://badges.gitter.im/discord-pickup-bot/community.svg)](https://gitter.im/discord-pickup-bot/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Maintainability](https://api.codeclimate.com/v1/badges/66275c62b74e27401e3b/maintainability)](https://codeclimate.com/github/Floydan/discord-pickup-bot/maintainability)

# Discord pickup game bot

A discord bot for managing pickup games

The bot can use azure tables for storing queues or just an in memory store using `ConcurrentDictionary`.

You can change which one is used by changing the dependency injection in `PickupBot/Program.cs`

## Setup
Create a `launchSettings.json` file in the PickupBot/Properties folder

Then add this to the json file substituting the values
```javascript
{
    "profiles": {
        "PickupBot": {
            "commandName": "Project",
            "environmentVariables": {
                "DiscordToken": "DISCORD TOKEN HERE",
                "StorageConnectionString": "AZURE TABLES STORAGE CONNECTION STRING HERE",
                "RCONHost": "ip/host",
                "RCONPort": "portnumber",
                "RCONPassword": "password",
                "GoogleTranslateAPIKey": "Google API Key",
                "CommandPrefix": "!",
            }
        }
    }
}
```

- The RCON Prefixed environment variables enable the bot to query and interact with an RCON enabled game server.
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
