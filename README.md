<div align="center">
    <img src="https://cdn.discordapp.com/avatars/963613397478424596/37fdbaef0258de1738c1b165f9bdb98a.webp?size=160">
    <p><b>Elmer</b><br />A simple sticky discord bot</p>
</div>

## Commands
` /glue ` - Create a sticky message in the current channel, or a provided channel<br/>
` /unglue ` - Remove the sticky message in the current channel, or a provided channel

### Sticky Customization
` /customize pfp ` - Set the profile picture for the sticky message, defaults to server profile picture if not set<br/>
` /customize username ` - Set the bot name for the sticky message, defaults to server name if not set

### Admin Commands
` /hi ` - Simple response testing command<br/>
` /members ` - Generate a list of all members in a specific format who are in a role<br/>
` /server leave ` - Force the bot to leave a server<br/>
` /server allow ` - Allow a server, other then the admin server, to use the bot<br/>
` /server disallow ` - Remove a server from being able to use the bot<br/>

## Setup
When you first run the bot, it will create a settings.json file. This file should look as like the below
```
{
  "Token": "",
  "Admin": {
    "ServerID": 0,
    "ChannelID": 0,
    "UserID": 0
  },
  "EnabledServers": []
}
```
` Token ` - Set to your token from Discord Developer Portal<br />
` Admin.ServerID ` - Set to your primary server's id<br />
` Admin.ChannelID ` - Set to the channel for error output<br />
` Admin.UserID ` - Set to your User ID<br />
` EnabledServers ` - You can manually add server ids here, otherwise the bot will add and update. There is no need to add the admin server id.
