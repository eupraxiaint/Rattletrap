using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Linq;
using Discord.Rest;
using IronPython.Hosting;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace Rattletrap
{
  // this is the main service that runs Rattletrap - all commands in MatchModule.cs should be more or less directly
  // making static function calls here
  public class MatchService
  {
    // the discord socket this service uses to communicate with discord
    public static DiscordSocketClient DiscordSocket;

    // the configuration data stored in _config.yml
    public static IConfigurationRoot Config;

    // the list of guilds mapped to their information structures

    public MatchService(DiscordSocketClient InDiscordSocket, CommandService commands, IConfigurationRoot InConfig)
    {
      DiscordSocket = InDiscordSocket;

      DiscordSocket.GuildAvailable += OnGuildAvailable;

      /*PositionsToEmotes[PlayerPosition.Safelane] = ":one:";
      PositionsToEmotes[PlayerPosition.Midlane] = ":two:";
      PositionsToEmotes[PlayerPosition.Offlane] = ":three:";
      PositionsToEmotes[PlayerPosition.SoftSupport] = ":four:";
      PositionsToEmotes[PlayerPosition.Support] = ":five:";*/

      Config = InConfig;

      LobbyRunner.CreateLobbyRunners(1);
    }

    // runs when a guild is available for the bot. be careful initializing in here, this can get called again if the
    // bot loses internet connection!
    private async Task OnGuildAvailable(SocketGuild InGuild)
    {
      GuildInstance guildInst = GuildInstance.Get(InGuild);

      if(guildInst == null)
      {
        GuildInstance.Create(InGuild);
      }
    }

    // returns whether or not the bot should send messages in the input channel
    public static bool IsAllowedChannel(IGuild InGuild, ITextChannel InChannel)
    {
      GuildInstance guildInst = GuildInstance.Get(InGuild);
      return guildInst.AdminBotChannel == InChannel || guildInst.MainBotChannel == InChannel;
    }
  } 
}