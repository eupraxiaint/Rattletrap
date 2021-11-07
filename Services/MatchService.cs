using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Linq;

namespace Rattletrap
{
  public enum PlayerPosition
  {
    Safelane,
    Midlane,
    Offlane,
    SoftSupport,
    Support
  }

  public enum PlayerRank
  {
    Uncalibrated,
    Herald,
    Guardian,
    Crusader,
    Archon,
    Legend,
    Ancient,
    Divine,
    Immortal
  };

  // data struct we use to retrieve data about a particular player - determined via roles
  public class PlayerInfo
  {
    public List<PlayerPosition> Positions = new List<PlayerPosition>();
    public PlayerRank Rank;
    public IGuildUser User;
  }

  public enum MatchState
  {
    Pending,
    WaitingForLobby,
    Ready,
    Canceled
  }

  // manages a single match, generated by queues
  public abstract class IMatch
  {
    // the guild this match was created in
    public IGuild Guild;

    // the queue this match was created by
    public IQueue SourceQueue;

    // the current state (pending, waiting for lobby, etc) of the match
    public MatchState State;

    // all the players playing in the match
    public List<IGuildUser> Players = new List<IGuildUser>();

    // all players that have readied up
    public List<IGuildUser> ReadyPlayers = new List<IGuildUser>();

    // the sequential ID used to identify the match in chat commands
    public int Id;

    // the message sent to announce the match
    public IMessage AnnounceMessage;

    // the time the match was created at
    public DateTime CreatedTime;

    // whether or not the match has been hurried with ;hurryup
    public bool HurryUp;

    // tracks the next match ID to use
    private static int NextMatchId = 0;

    public IMatch(IGuild InGuild, IQueue InSourceQueue, List<IGuildUser> InPlayers)
    {
      Guild = InGuild;
      SourceQueue = InSourceQueue;
      State = MatchState.Pending;
      Players = InPlayers;
      Id = NextMatchId++;
      AnnounceMessage = null;
      CreatedTime = DateTime.Now;
      HurryUp = false;
    }

    // called when the match is announced (immediately after being created by a queue)
    public abstract void Announce();

    // called when all players have readied up
    public abstract void OnReady();

    // check if user is in a match
    public abstract bool IsUserInMatch(IGuildUser InUser);
    // called when a lobby has been created for the match
    public abstract void OnLobby(String InName, String InPassword);
  }

  // per-guild information - most tracked objects are contained here
  public class GuildInfo
  {
    // the name of the guild
    public string Name;

    // the list of queues by name
    public Dictionary<string, IQueue> Queues = new Dictionary<string, IQueue>();

    // the list of currently active matches
    public List<IMatch> Matches = new List<IMatch>();

    // the list of player positions mapped to their roles in the guild
    public Dictionary<PlayerPosition, IRole> PositionsToRoles = new Dictionary<PlayerPosition, IRole>();

    // the list of player ranks mapped to their roles in the guild
    public Dictionary<PlayerRank, IRole> RanksToRoles = new Dictionary<PlayerRank, IRole>();

    // the list of player ranks mapped to their emotes in the guild
    public Dictionary<PlayerRank, string> RanksToEmotes = new Dictionary<PlayerRank, string>();

    // the channel for admin bot commands
    public ITextChannel AdminBotChannel;

    // the channel for public bot commands
    public ITextChannel MainBotChannel;
  }

  // this is the main service that runs Rattletrap - all commands in MatchModule.cs should be more or less directly
  // making static function calls here
  public class MatchService
  {
    // the discord socket this service uses to communicate with discord
    public static DiscordSocketClient DiscordSocket;

    // the configuration data stored in _config.yml
    private static IConfigurationRoot config;

    // the checkmark emoji
    public static Emoji CheckmarkEmoji = new Emoji("\u2705");

    // the x emoji
    public static Emoji XEmoji = new Emoji("\u274C");

    // the list of positions mapped to emotes
    public static Dictionary<PlayerPosition, string> PositionsToEmotes = new Dictionary<PlayerPosition, string>();

    // the list of guilds mapped to their information structures
    public static Dictionary<IGuild, GuildInfo> GuildInfos = new Dictionary<IGuild, GuildInfo>();

    public MatchService(DiscordSocketClient InDiscordSocket, CommandService commands, IConfigurationRoot InConfig)
    {
      DiscordSocket = InDiscordSocket;

      DiscordSocket.GuildAvailable += OnGuildAvailable;

      DiscordSocket.ReactionAdded += OnReactionAdded;
      DiscordSocket.ReactionRemoved += OnReactionRemoved;

      PositionsToEmotes[PlayerPosition.Safelane] = ":one:";
      PositionsToEmotes[PlayerPosition.Midlane] = ":two:";
      PositionsToEmotes[PlayerPosition.Offlane] = ":three:";
      PositionsToEmotes[PlayerPosition.SoftSupport] = ":four:";
      PositionsToEmotes[PlayerPosition.Support] = ":five:";

      config = InConfig;
    }

    // the async task that runs a match once it's created
    // todo: should this maybe be an abstract function on IMatch?
    public static async void RunMatch(IMatch InMatch)
    {
      GuildInfo guildInfo = GuildInfos[InMatch.Guild];

      guildInfo.Matches.Add(InMatch);

      // announce the match
      InMatch.Announce();

      // wait here until the readyup timer ends or the match gets hurried
      while((DateTime.Now - InMatch.CreatedTime) < TimeSpan.FromMinutes(5) && !InMatch.HurryUp)
      {
        if(InMatch.ReadyPlayers.Count == InMatch.Players.Count)
        {
          break;
        }
        await Task.Delay(TimeSpan.FromSeconds(5));
      }

      // if everyone readied, the match state should be WaitingForLobby - so cancel the match here if it's not
      if(InMatch.State == MatchState.Pending)
      {
        String message = $"Match id {InMatch.Id} timed out. Returning to queue: ";
        foreach(IGuildUser user in InMatch.ReadyPlayers)
        {
          message += user.Mention + " ";
        }
        InMatch.SourceQueue.AnnouncementChannel.SendMessageAsync(message);
      }
    }

    // finds a role by name in a guild
    public static IRole FindRole(IGuild InGuild, string InName)
    {
      foreach(IRole role in InGuild.Roles)
      {
        if(role.Name == InName)
        {
          return role;
        }
      }

      return null;
    }

    // finds an emote by name in a guild
    private static IEmote FindEmote(IGuild InGuild, string InName)
    {
      foreach(IEmote emote in InGuild.Emotes)
      {
        if(emote.Name == InName)
        {
          return emote;
        }
      }

      return null;
    }

    // finds a text channel by name in a guild
    public static ITextChannel FindTextChannel(IGuild InGuild, string InName)
    {
      IReadOnlyCollection<ITextChannel> textChannels = InGuild.GetTextChannelsAsync().Result;
      foreach(ITextChannel textChannel in textChannels)
      {
        if(textChannel.Name == InName)
        {
          return textChannel;
        }
      }

      return null;
    }

    // runs when a guild is available for the bot. be careful initializing in here, this can get called again if the
    // bot loses internet connection!
    private async Task OnGuildAvailable(SocketGuild InGuild)
    {
      if(config["guild"] == InGuild.Name && !GuildInfos.ContainsKey(InGuild))
      {
        ITextChannel announcementChannel = FindTextChannel(InGuild, "\U0001f514inhouse-announcement");

        GuildInfo guildInfo = new GuildInfo();
        guildInfo.Name = InGuild.Name;
        guildInfo.Queues["eu"] = new InhouseQueue("eu", announcementChannel);
        guildInfo.Queues["na"] = new InhouseQueue("na", announcementChannel);
        guildInfo.Queues["sea"] = new InhouseQueue("sea", announcementChannel);
        guildInfo.Queues["cis"] = new InhouseQueue("cis", announcementChannel);
        guildInfo.Queues["eu-1v1"] = new OneVOneQueue("eu-1v1", announcementChannel);
        guildInfo.Queues["na-1v1"] = new OneVOneQueue("na-1v1", announcementChannel);
        guildInfo.Queues["sea-1v1"] = new OneVOneQueue("sea-1v1", announcementChannel);
        guildInfo.Queues["cis-1v1"] = new OneVOneQueue("cis-1v1", announcementChannel);

        guildInfo.PositionsToRoles[PlayerPosition.Safelane] = FindRole(InGuild, "Safelane");
        guildInfo.PositionsToRoles[PlayerPosition.Midlane] = FindRole(InGuild, "Midlane");
        guildInfo.PositionsToRoles[PlayerPosition.Offlane] = FindRole(InGuild, "Offlane");
        guildInfo.PositionsToRoles[PlayerPosition.SoftSupport] = FindRole(InGuild, "Soft Support");
        guildInfo.PositionsToRoles[PlayerPosition.Support] = FindRole(InGuild, "Support");

        guildInfo.RanksToRoles[PlayerRank.Uncalibrated] = FindRole(InGuild, "Uncalibrated");
        guildInfo.RanksToRoles[PlayerRank.Herald] = FindRole(InGuild, "Herald");
        guildInfo.RanksToRoles[PlayerRank.Guardian] = FindRole(InGuild, "Guardian");
        guildInfo.RanksToRoles[PlayerRank.Crusader] = FindRole(InGuild, "Crusader");
        guildInfo.RanksToRoles[PlayerRank.Archon] = FindRole(InGuild, "Archon");
        guildInfo.RanksToRoles[PlayerRank.Legend] = FindRole(InGuild, "Legend");
        guildInfo.RanksToRoles[PlayerRank.Ancient] = FindRole(InGuild, "Ancient");
        guildInfo.RanksToRoles[PlayerRank.Divine] = FindRole(InGuild, "Divine");
        guildInfo.RanksToRoles[PlayerRank.Immortal] = FindRole(InGuild, "Immortal");

        guildInfo.RanksToEmotes[PlayerRank.Uncalibrated] = "<:Uncalibrated:901649283546234931>";
        guildInfo.RanksToEmotes[PlayerRank.Herald] = "<:Herald:901649551230906368>";
        guildInfo.RanksToEmotes[PlayerRank.Guardian] = "<:Guardian:901649591580098620>";
        guildInfo.RanksToEmotes[PlayerRank.Crusader] = "<:Crusader:901649627437203516>";
        guildInfo.RanksToEmotes[PlayerRank.Archon] = "<:Archon:901649670252679248>";
        guildInfo.RanksToEmotes[PlayerRank.Legend] = "<:Legend:901649722077491231>";
        guildInfo.RanksToEmotes[PlayerRank.Ancient] = "<:Ancient:901649761269063720>";
        guildInfo.RanksToEmotes[PlayerRank.Divine] = "<:Divine:901649806559154216>";
        guildInfo.RanksToEmotes[PlayerRank.Immortal] = "<:Immortal:901649831582380112>";

        guildInfo.MainBotChannel = FindTextChannel(InGuild, "\U0001f50einhouse-queue");
        guildInfo.AdminBotChannel = FindTextChannel(InGuild, "admin-bot-commands");

        GuildInfos.Add(InGuild, guildInfo);
      }
    }

    // returns whether or not the bot should send messages in the input channel
    public static bool IsAllowedChannel(IGuild InGuild, ITextChannel InChannel)
    {
      GuildInfo guildInfo = GuildInfos[InGuild];
      return guildInfo.AdminBotChannel == InChannel || guildInfo.MainBotChannel == InChannel;
    }

    // adds a user to the matchmaking queue
    public static QueueResult QueueUser(IGuildUser InUser, IQueue InQueue, IGuildUser InTriggeringUser = null, IMessage InTriggeringMessage = null)
    {
      GuildInfo guildInfo = GuildInfos[InUser.Guild];

      foreach(KeyValuePair<string, IQueue> playerCheckQueue in guildInfo.Queues)
      {
        if(playerCheckQueue.Value.IsUserInQueue(InUser))
        {
          return QueueResult.AlreadyQueuing;
        }
      }

      foreach(IMatch matchesRunning in guildInfo.Matches) 
      {
        if(matchesRunning.IsUserInMatch(InUser))
        {
          return QueueResult.AlreadyInMatch;
        }
      }

      return InQueue.Queue(InUser, InTriggeringUser, InTriggeringMessage);
    }

    // removes a user from the matchmaking queue
    public static UnqueueResult UnqueueUser(IGuildUser InUser, IGuildUser InTriggeringUser = null, IMessage InTriggeringMessage = null)
    {
      GuildInfo guildInfo = GuildInfos[InUser.Guild];

      foreach(KeyValuePair<string, IQueue> queue in guildInfo.Queues)
      {
        if(queue.Value.IsUserInQueue(InUser))
        {
          queue.Value.Unqueue(InUser, InTriggeringUser, InTriggeringMessage);
          return UnqueueResult.Success;
        }
      }

      return UnqueueResult.NotQueuing;
    }

    // announces a lobby for a match
    public static void AnnounceLobby(IGuild InGuild, IMatch InMatch, string InName, string InPassword)
    {
      GuildInfo guildInfo = GuildInfos[InGuild];

      InMatch.OnLobby(InName, InPassword);

      guildInfo.Matches.Remove(InMatch);
    }

    // gets the PlayerInfo struct for a user based on their roles
    public static PlayerInfo GetPlayerInfo(IGuild InGuild, IGuildUser InUser)
    {
      GuildInfo guildInfo = GuildInfos[InGuild];

      PlayerInfo result = new PlayerInfo();
      result.User = InUser;

      IGuildUser guildUser = InUser as IGuildUser;

      foreach(PlayerPosition position in PlayerPosition.GetValues(typeof(PlayerPosition)))
      {
        if(guildUser.RoleIds.Contains(guildInfo.PositionsToRoles[position].Id))
        {
          result.Positions.Add(position);
        }
      }

      if(result.Positions.Count == 0)
      {
        result.Positions.Add(PlayerPosition.Safelane);
        result.Positions.Add(PlayerPosition.Midlane);
        result.Positions.Add(PlayerPosition.Offlane);
        result.Positions.Add(PlayerPosition.SoftSupport);
        result.Positions.Add(PlayerPosition.Support);
      }

      result.Rank = PlayerRank.Uncalibrated;

      foreach(PlayerRank rank in PlayerRank.GetValues(typeof(PlayerRank)))
      {
        IRole role = guildInfo.RanksToRoles[rank];
        if(guildUser.RoleIds.Contains(role.Id))
        {
          result.Rank = rank;
          break;
        }
      }

      return result;
    }

    // called when a reaction is added to any message - right now we just use this for readying up
    //
    // todo: break this function up a bit, maybe move some of its implementation to abstract function calls on IMatch
    //       or maybe a separate helper class for readying up?
    private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, 
      ISocketMessageChannel originChannel, SocketReaction reaction)
    {
      if(reaction.User.Value.Id != DiscordSocket.CurrentUser.Id && reaction.User.IsSpecified && cachedMessage.HasValue)
      {
        SocketGuildChannel channel = originChannel as SocketGuildChannel;

        GuildInfo guildInfo = GuildInfos[channel.Guild];

        foreach(IMatch match in guildInfo.Matches)
        {
          if(match != null && match.State == MatchState.Pending && match.AnnounceMessage.Id == cachedMessage.Value.Id && match.Players.Contains(reaction.User.Value))
          {
            if(reaction.Emote.Name == CheckmarkEmoji.Name)
            {
              match.ReadyPlayers.Add(reaction.User.Value as IGuildUser);

              if(match.ReadyPlayers.Count == match.Players.Count)
              {
                match.OnReady();
                match.State = MatchState.WaitingForLobby;
              }
            }
            else if(reaction.Emote.Name == XEmoji.Name)
            {
              String message = $"{reaction.User.Value.Mention} declined the match. (id: {match.Id}) Returning to queue: ";
              foreach(IUser player in match.Players)
              {
                if(player.Id != reaction.User.Value.Id)
                {
                  message += player.Mention + " ";
                }
              }

              guildInfo.Matches.Remove(match);
              match.State = MatchState.Canceled;

              await originChannel.SendMessageAsync(message);

              List<IGuildUser> playersToRequeue = match.Players;
              playersToRequeue.Remove(reaction.User.Value as IGuildUser);

              match.SourceQueue.Requeue(playersToRequeue);
            }

            break;
          }
        }
      }
    }

    // called when a reaction is removed from any message - right now we just use this for readying up
    private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cachedMessage, 
      ISocketMessageChannel originChannel, SocketReaction reaction)
    {
      if(reaction.User.Value.Id != DiscordSocket.CurrentUser.Id)
      {
        SocketGuildChannel channel = originChannel as SocketGuildChannel;

        GuildInfo guildInfo = GuildInfos[channel.Guild];

        foreach(IMatch match in guildInfo.Matches)
        {
          if(match.AnnounceMessage.Id == cachedMessage.Value.Id && match.Players.Contains(reaction.User.Value))
          {
            if(reaction.Emote.Name == CheckmarkEmoji.Name)
            {
              match.ReadyPlayers.Remove(reaction.User.Value as IGuildUser);
            }

            break;
          }
        }
      }
    }

    public enum HurryUpResult
    {
      Success,
      MatchNotPending
    }

    // hurries up a match, causing its ready-up timer to expire
    public static HurryUpResult HurryUp(IMatch InMatch)
    {
      if(InMatch.State != MatchState.Pending)
      {
        return HurryUpResult.MatchNotPending;
      }

      InMatch.HurryUp = true;

      return HurryUpResult.Success;
    }
  } 
}