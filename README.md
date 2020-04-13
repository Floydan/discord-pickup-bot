![.NET Core](https://github.com/Floydan/discord-pickup-bot/workflows/.NET%20Core/badge.svg?branch=master)


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
                "StorageConnectionString": "AZURE TABLES STORAGE CONNECTION STRING HERE"
            }
        }
    }
}
```

The `settings.job` can be ignores since this discord bot needs to be run continuously.

## Testing/using
To add the bot to your discord server just go here to authorize and give the bot access:

[Bot Authorization](https://discordapp.com/api/oauth2/authorize?client_id=696658931434389505&permissions=285215793&scope=bot)

After the bot has been added to your server you can run `!help` to see all the available commands.