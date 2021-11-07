using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using System.Collections.Generic;
using System.Collections;

namespace Rattletrap.Modules
{
  [Name("Matchmaking")]
  [Summary("Schedule some matches!")]
  public class MatchModule : ModuleBase<SocketCommandContext>
  {
    [Command("queue")]
    [Summary("Adds you to a matchmaking queue.")]
    public async Task Queue(string InQueue)
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      GuildInfo guildInfo = MatchService.GuildInfos[Context.Guild];

      if(!guildInfo.Queues.ContainsKey(InQueue))
      {
        string queues = "";

        foreach(KeyValuePair<string, IQueue> queue in guildInfo.Queues)
        {
          queues += $"`{queue.Key}` ";
        }

        await ReplyAsync($"Could not queue {Context.User.Mention} in `{InQueue}`: queue does not exist. Available queues: " + queues);
      }

      IGuildUser guildUser = Context.User as IGuildUser;

      QueueResult result = MatchService.QueueUser(guildUser, guildInfo.Queues[InQueue], guildUser, Context.Message);

      if(result == QueueResult.Success)
      {
        await ReplyAsync($"Successfully queued {Context.User.Mention} in `{InQueue}`.");
      }
      else if(result == QueueResult.AlreadyQueuing)
      {
        await ReplyAsync($"Could not queue {Context.User.Mention} in `{InQueue}`: user is already queuing. Use ;cancel to stop your current queue.");
      }
      else if(result == QueueResult.AlreadyInMatch)
      {
        await ReplyAsync($"Could not queue {Context.User.Mention}: user is already in a match. Please cancel the match or let the timer run out.");
      }
    }

    [Command("cancel")]
    [Summary("Removes you from the matchmaking queue.")]
    public async Task Cancel()
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }
      
      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      IGuildUser guildUser = Context.User as IGuildUser;

      UnqueueResult result = MatchService.UnqueueUser(guildUser, guildUser, Context.Message);

      if(result == UnqueueResult.Success)
      {
        await ReplyAsync($"Successfully removed {Context.User.Mention} from matchmaking.");
      }
      else if(result == UnqueueResult.NotQueuing)
      {
        await ReplyAsync($"Could not remove {Context.User.Mention} from matchmaking: user was not queuing.");
      }
    }

    [Command("remove")]
    [Summary("Removes a user from the matchmaking queue. (admin-only)")]
    public async Task Remove(IGuildUser InUser)
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      IGuildUser guildUser = Context.User as IGuildUser;

      if(!guildUser.RoleIds.Contains(MatchService.FindRole(Context.Guild, "Admin").Id))
      {
        await ReplyAsync(";remove is an admin-only command.");
        return;
      }

      UnqueueResult result = MatchService.UnqueueUser(InUser, guildUser, Context.Message);

      if(result == UnqueueResult.Success)
      {
        await ReplyAsync($"Successfully removed {InUser.Mention} from matchmaking.");
      }
      else if(result == UnqueueResult.NotQueuing)
      {
        await ReplyAsync($"Could not remove {InUser.Mention} from matchmaking: user was not queuing.");
      }
    }

    [Command("hurryup")]
    [Summary("Forces the ready timer to expire for a pending match. (admin-only)")]
    public async Task HurryUp(int InMatchId)
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      IGuildUser guildUser = Context.User as IGuildUser;

      if(!guildUser.RoleIds.Contains(MatchService.FindRole(Context.Guild, "Admin").Id))
      {
        await ReplyAsync(";hurryup is an admin-only command.");
        return;
      }

      GuildInfo guildInfo = MatchService.GuildInfos[Context.Guild];
      IMatch matchToHurry = null;

      foreach(IMatch match in guildInfo.Matches)
      {
        if(match.Id == InMatchId)
        {
          matchToHurry = match;
          break;
        }
      }

      if(matchToHurry == null)
      {
        await ReplyAsync($"Could not hurry up match id {InMatchId}: a match with that id does not exist.");
        return;
      }

      MatchService.HurryUpResult result = MatchService.HurryUp(matchToHurry);

      if(result == MatchService.HurryUpResult.Success)
      {
        await ReplyAsync($"Successfully hurried up match id {InMatchId}.");
      }
      else if(result == MatchService.HurryUpResult.MatchNotPending)
      {
        await ReplyAsync($"Could not hurry up match id {InMatchId}: that match is not pending.");
      }
    }

    [Command("queueinfo")]
    [Summary("Displays information about the current queues")]
    public async Task QueueInfo()
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      GuildInfo guildInfo = MatchService.GuildInfos[Context.Guild];

      string message = "Queue info for " + guildInfo.Name + ":\n";

      foreach(KeyValuePair<string, IQueue> queue in guildInfo.Queues)
      {
        message += $"`{queue.Key}`: " + queue.Value.GetMatchInfo() + "\n";
      }

      await ReplyAsync(message);
    }

    [Command("matchinfo")]
    [Summary("Displays information about the current matches")]
    public async Task MatchInfo()
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }
      
      GuildInfo guildInfo = MatchService.GuildInfos[Context.Guild];

      string message = "Match info for " + guildInfo.Name + ":\n";

      foreach(IMatch match in guildInfo.Matches)
      {
        message += $"Match {match.Id} - state: {match.State.ToString()}, queue: {match.SourceQueue.Name}, ready: {match.ReadyPlayers.Count}, players: ";
        foreach(IUser player in match.Players)
        {
          IGuildUser guildUser = player as IGuildUser;
          message += (guildUser.Nickname == null ? guildUser.Username : guildUser.Nickname);
          if(player != match.Players.Last())
          {
            message += ", ";
          }
        }
        message += "\n";
      }

      await ReplyAsync(message);
    }

    [Command("lobby")]
    [Summary("Announces lobby information for a match.")]
    public async Task Lobby(int InId, string InName, string InPassword)
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }
      
      GuildInfo guildInfo = MatchService.GuildInfos[Context.Guild];

      IMatch matchToAnnounce = null;

      foreach(IMatch match in guildInfo.Matches)
      {
        if(match.Id == InId)
        {
          matchToAnnounce = match;
          break;
        }
      }

      if(matchToAnnounce == null)
      {
        await ReplyAsync($"Could not announce lobby info: match {InId} does not exist.");
      }
      else if(matchToAnnounce.State != MatchState.WaitingForLobby)
      {
        await ReplyAsync($"Could not announce lobby info: not all players are ready for match {InId}.");
      }
      else
      {
        MatchService.AnnounceLobby(Context.Guild, matchToAnnounce, InName, InPassword);
      }
    }

    [Command("playerinfo")]
    [Summary("Gets player information for a given player.")]
    public async Task DisplayPlayerInfo(IGuildUser InUser)
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }
      
      GuildInfo guildInfo = MatchService.GuildInfos[Context.Guild];

      PlayerInfo playerInfo = MatchService.GetPlayerInfo(Context.Guild, InUser);

      string message = $"Player info for {InUser.Username}:\nPositions: ";

      foreach(PlayerPosition position in playerInfo.Positions)
      {
        message += MatchService.PositionsToEmotes[position];
      }

      string rankEmote = guildInfo.RanksToEmotes[playerInfo.Rank];

      message += $"\nRank: {rankEmote}";

      await ReplyAsync(message);
    }

    [Command("ping")]
    [Summary("Checks if Rattletrap is running.")]
    public async Task Ping()
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }
      
      await ReplyAsync($"My gears turn!");
    }

    [Command("help")]
    [Summary("Displays the list of commands Rattletrap can run.")]
    public async Task Help()
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {MatchService.GuildInfos[Context.Guild].MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      GuildInfo guildInfo = MatchService.GuildInfos[Context.Guild];
      
      string messageText =
          "**;queue <queue-name>** - Adds you to a matchmaking queue. Available queues: ";

      foreach(KeyValuePair<string, IQueue> queue in guildInfo.Queues)
      {
        messageText += $"`{queue.Key}` ";
      }

      messageText += "\n**;cancel** - Removes you from the matchmaking queue.\n"
        + "**;queueinfo** - Displays details about the available queues.\n"
        + "**;matchinfo** - Displays details about the current pending or ready matches.\n"
        + "**;playerinfo <user-mention>** - Displays positions/ranks for a particular player.\n"
        + "**;ping** - Check if the bot is online.\n"
        + "**;help** - Displays this help text.";

      IGuildUser guildUser = Context.User as IGuildUser;

      if(guildUser.RoleIds.Contains(MatchService.FindRole(Context.Guild, "Admin").Id))
      {
        messageText += "\n\nSince you're an admin, you also have access to these admin-only commands:\n"
        + "**;remove <user-mention>** - Removes another player from the matchmaking queue.\n"
        + "**;hurryup <match-id>** - Forces a match's ready-up timer to expire.";
      }

      await ReplyAsync(messageText);
    }

    [Command("version")]
    public async Task Version()
    {
      if(!MatchService.GuildInfos.ContainsKey(Context.Guild))
      {
        return;
      }

      await ReplyAsync("Rattletrap v0.5 prototype");
    }
  }
}