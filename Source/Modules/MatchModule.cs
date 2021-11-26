using Discord.Commands;
using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord;
using System.Collections.Generic;
using System.Collections;
using System.Security;

namespace Rattletrap.Modules
{
  [Name("Matchmaking")]
  [Summary("Schedule some matches!")]
  public class MatchModule : ModuleBase<SocketCommandContext>
  {
    [Command("queue")]
    [Summary("Adds you to a matchmaking queue.")]
    public async Task Queue()
    {
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      Player player = Player.GetOrCreate(Context.User as IGuildUser);

      GuildInstance.QueuePlayerResult result = guildInst.QueuePlayer(player);

      EmbedBuilder embed = new EmbedBuilder();

      if(result == GuildInstance.QueuePlayerResult.Success)
      {
        embed.WithColor(Color.Green);
        embed.WithDescription($"Successfully queued {Context.User.Mention}.");
      }
      else if(result == GuildInstance.QueuePlayerResult.AlreadyQueuing)
      {
        embed.WithColor(Color.Red);
        embed.WithDescription($"Could not queue {Context.User.Mention}`: user is already queuing.");
      }

      embed.WithCurrentTimestamp();
      
      await ReplyAsync(embed: embed.Build());
    }

    [Command("cancel")]
    [Summary("Removes you from the matchmaking queue.")]
    public async Task Cancel()
    {
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      Player player = Player.GetOrCreate(Context.User as IGuildUser);

      GuildInstance.UnqueuePlayerResult result = guildInst.UnqueuePlayer(player);

      EmbedBuilder embed = new EmbedBuilder();

      if(result == GuildInstance.UnqueuePlayerResult.Success)
      {
        embed.WithColor(Color.Green);
        embed.WithDescription($"Successfully removed {Context.User.Mention}.");
      }
      else if(result == GuildInstance.UnqueuePlayerResult.NotQueuing)
      {
        embed.WithColor(Color.Red);
        embed.WithDescription($"Could not queue {Context.User.Mention}: user was not queuing.");
      }

      embed.WithCurrentTimestamp();
      
      await ReplyAsync(embed: embed.Build());
    }

    [Command("remove")]
    [Summary("Removes a user from the matchmaking queue. (admin-only)")]
    public async Task Remove(IGuildUser InUser)
    {
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      IGuildUser guildUser = Context.User as IGuildUser;

      if(!guildInst.IsAdmin(guildUser))
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
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      IGuildUser guildUser = Context.User as IGuildUser;

      if(!guildInst.IsAdmin(guildUser))
      {
        await ReplyAsync(";hurryup is an admin-only command.");
        return;
      }

      IMatch matchToHurry = null;

      foreach(IMatch match in guildInst.Matches)
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
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      EmbedBuilder builder = new EmbedBuilder();

      builder.WithColor(Color.DarkTeal);
      builder.WithCurrentTimestamp();
      builder.WithTitle($"Queue info for {guildInst.Name}:");

      if(guildInst.QueuingPlayers.Players.Count != 0)
      {
        string playersString = "";
        foreach(Player player in guildInst.QueuingPlayers.Players)
        {
          playersString += player.Name + " ";
        }

        builder.AddField($"{guildInst.QueuingPlayers.Players.Count} players queuing:", playersString);
      }
      else
      {
        builder.AddField($"0 players queuing.", ":(");
      }

      await ReplyAsync(embed: builder.Build());
    }

    [Command("matchinfo")]
    [Summary("Displays information about the current matches")]
    public async Task MatchInfo()
    {
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }
      
      string message = "Match info for " + guildInst.Name + ":\n";

      foreach(IMatch match in guildInst.Matches)
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
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }
      
      IMatch matchToAnnounce = null;

      foreach(IMatch match in guildInst.Matches)
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
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }
      
      Player player = Player.GetOrCreate(InUser);

      EmbedBuilder builder = new EmbedBuilder();
      builder.WithAuthor(player.GuildUser);

      string modesString = "";
      foreach(string mode in player.Modes)
      {
        modesString += $"`{mode}` ";
      }
      if(modesString == "")
      {
        modesString = "None";
      }
      builder.AddField("Modes", modesString);

      string regionsString = "";
      foreach(string region in player.Regions)
      {
        regionsString += $"`{region}` ";
      }
      if(regionsString == "")
      {
        regionsString = "None";
      }
      builder.AddField("Regions", regionsString);

      await ReplyAsync(embed: builder.Build());
    }

    [Command("ping")]
    [Summary("Checks if Rattletrap is running.")]
    public async Task Ping()
    {
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }
      
      await ReplyAsync($"My gears turn!");
    }

    [Command("enable")]
    public async Task Enable()
    {
      GuildInstance guildInstance = GuildInstance.Get(Context.Guild);

      if(guildInstance != null)
      {
        guildInstance.Enabled = true;
      }

      await ReplyAsync($"Rattletrap enabled for {Context.Guild.Name}.");
    }

    [Command("disable")]
    public async Task Disable()
    {
      GuildInstance guildInstance = GuildInstance.Get(Context.Guild);

      if(guildInstance != null)
      {
        guildInstance.Enabled = false;
      }

      await ReplyAsync($"Rattletrap disabled for {Context.Guild.Name}.");
    }

    [Command("setmodes")]
    public async Task SetModes(params string[] InModes)
    {
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      
      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      Player player = Player.GetOrCreate(Context.User as IGuildUser);

      player.Modes.Clear();
      foreach(string mode in InModes)
      {
        player.Modes.Add(mode);
      }

      player.SaveToFile();

      EmbedBuilder embed = new EmbedBuilder();
      embed.WithCurrentTimestamp();

      string modesString = "";
      foreach(string mode in player.Modes)
      {
        modesString += $"`{mode}` ";
      }
      embed.WithDescription($"Successfully set modes for {Context.User.Mention}: {modesString}");

      await ReplyAsync(embed: embed.Build());
    }

    [Command("setregions")]
    public async Task SetRegions(params string[] InRegions)
    {
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      
      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }

      Player player = Player.GetOrCreate(Context.User as IGuildUser);

      player.Regions.Clear();
      foreach(string region in InRegions)
      {
        player.Regions.Add(region);
      }

      player.SaveToFile();

      EmbedBuilder embed = new EmbedBuilder();
      embed.WithCurrentTimestamp();

      string regionsString = "";
      foreach(string region in player.Regions)
      {
        regionsString += $"`{region}` ";
      }
      embed.WithDescription($"Successfully set regions for {Context.User.Mention}: {regionsString}");

      await ReplyAsync(embed: embed.Build());
    }

    [Command("help")]
    [Summary("Displays the list of commands Rattletrap can run.")]
    public async Task Help()
    {
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      if(!MatchService.IsAllowedChannel(Context.Guild, Context.Channel as ITextChannel))
      {
        await ReplyAsync($"Please use {guildInst.MainBotChannel.Mention} for Rattletrap commands.");
        return;
      }
      
      string messageText =
          "**;queue <queue-name>** - Adds you to a matchmaking queue. Available queues: ";

      foreach(KeyValuePair<string, IQueue> queue in guildInst.Queues)
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

      if(guildInst.IsAdmin(guildUser))
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
      GuildInstance guildInst = GuildInstance.Get(Context.Guild);
      if(!guildInst.Enabled)
      {
        return;
      }

      await ReplyAsync("Rattletrap v0.6 prototype");
    }
  }
}