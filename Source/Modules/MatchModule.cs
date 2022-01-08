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

    [Command("queue")]
    [Summary("Adds you to a matchmaking queue.")]
    public async Task Queue(IGuildUser InUser)
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

      if(!guildInst.IsAdmin(Context.User as IGuildUser))
      {
        await ReplyAsync("Only admins can queue other users.");
        return;
      }

      Player player = Player.GetOrCreate(InUser);

      GuildInstance.QueuePlayerResult result = guildInst.QueuePlayer(player);

      EmbedBuilder embed = new EmbedBuilder();

      if(result == GuildInstance.QueuePlayerResult.Success)
      {
        embed.WithColor(Color.Green);
        embed.WithDescription($"Successfully queued {InUser.Mention}.");
      }
      else if(result == GuildInstance.QueuePlayerResult.AlreadyQueuing)
      {
        embed.WithColor(Color.Red);
        embed.WithDescription($"Could not queue {InUser.Mention}`: user is already queuing.");
      }

      embed.WithCurrentTimestamp();
      
      await ReplyAsync(embed: embed.Build());
    }

    [Command("queue")]
    public async Task Queue(IRole InRole)
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

      if(!guildInst.IsAdmin(Context.User as IGuildUser))
      {
        await ReplyAsync("Only admins can queue other users.");
        return;
      }

      List<Player> queuedPlayers = new List<Player>();

      foreach(IGuildUser user in Context.Guild.Users)
      {
        if(user.RoleIds.Contains(InRole.Id))
        {
          Player player = Player.GetOrCreate(user);
          GuildInstance.QueuePlayerResult result = guildInst.QueuePlayer(player);
          queuedPlayers.Add(player);
        }
      }

      if(queuedPlayers.Count == 0)
      {
        EmbedBuilder embed = new EmbedBuilder();
        embed.WithColor(Color.Red);
        embed.WithDescription("Failed to queue any users.");
        await ReplyAsync(embed: embed.Build());
      }
      else
      {
        string mentions = "";
        foreach(Player player in queuedPlayers)
        {
          mentions += $"{player.GuildUser.Mention} ";
        }

        EmbedBuilder embed = new EmbedBuilder();
        embed.WithColor(Color.Green);
        embed.WithDescription($"Successfully queued the following users:\n{mentions}");
        await ReplyAsync(embed: embed.Build());
      }
      
    }

    [Command("respond")]
    public async Task Respond(int InPollId, IGuildUser InUser, int InResponseIdx)
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

      if(!guildInst.IsAdmin(Context.User as IGuildUser))
      {
        await ReplyAsync(";respond is an admin-only command.");
        return;
      }

      Poll poll = Poll.GetPollById(InPollId);

      if(poll == null)
      {
        await ReplyAsync("Invalid poll ID.");
        return;
      }

      if(poll.Responses.Count <= InResponseIdx || InResponseIdx < 0)
      {
        await ReplyAsync("Invalid response idx.");
        return;
      }

      Player player = Player.GetOrCreate(InUser);

      poll.AddResponse(player, poll.Responses[InResponseIdx].Emote);
    }

    [Command("respondall")]
    public async Task RespondAll(int InPollId, int InResponseIdx)
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

      if(!guildInst.IsAdmin(Context.User as IGuildUser))
      {
        await ReplyAsync(";respondall is an admin-only command.");
        return;
      }

      Poll poll = Poll.GetPollById(InPollId);

      if(poll == null)
      {
        await ReplyAsync("Invalid poll ID.");
        return;
      }

      if(poll.Responses.Count <= InResponseIdx || InResponseIdx < 0)
      {
        await ReplyAsync("Invalid response idx.");
        return;
      }

      foreach(Player player in poll.Players.Players)
      {
        poll.AddResponse(player, poll.Responses[InResponseIdx].Emote);
      }
    }

    [Command("lobbytest")]
    public async Task LobbyTest()
    {
      LobbyCreateInfo lobbyCreateInfo = new LobbyCreateInfo();
      lobbyCreateInfo.Name = "SKIBADEE, SKIBADANGER, I AM THE REARRANGER";
      lobbyCreateInfo.Password = "1234";
      Lobby lobby = new Lobby(lobbyCreateInfo);
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

      EmbedBuilder builder = new EmbedBuilder();

      builder.WithColor(Color.DarkBlue);
      builder.WithTitle($"Match info for {guildInst.Name}");
      builder.WithCurrentTimestamp();      

      foreach(IMatch2 match in guildInst.Matches)
      {
        builder.AddField($"Match {match.Id}:", match.GetMatchInfo());
      }

      await ReplyAsync(embed: builder.Build());
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

    [Command("balanceinfo")]
    public async Task BalanceInfo(int InMatchId)
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

      IMatch2 match = IMatch2.GetMatchById(InMatchId);

      if(match == null)
      {
        EmbedBuilder embed = new EmbedBuilder();
        embed.WithColor(Color.Red);
        embed.WithDescription($"{InMatchId} is not a valid match ID.");

        await ReplyAsync(embed: embed.Build());
        return;
      }

      InhouseMatch2 inhouseMatch = match as InhouseMatch2;

      if(inhouseMatch == null)
      {
        EmbedBuilder embed = new EmbedBuilder();
        embed.WithColor(Color.Red);
        embed.WithDescription($"{InMatchId} is not an `inhouse` or `practice` match.");

        await ReplyAsync(embed: embed.Build());
        return;
      }

      {
        EmbedBuilder embed = new EmbedBuilder();
        embed.WithColor(Color.Teal);
        embed.WithTitle($"Match {InMatchId} Balance Info");
        embed.AddField("Team 1 Rank Value", $"{inhouseMatch.MatchShuffler.TeamRankValues[0]}");
        embed.AddField("Team 2 Rank Value", $"{inhouseMatch.MatchShuffler.TeamRankValues[1]}");
        embed.AddField("Rank Balance Score", $"{inhouseMatch.MatchShuffler.RankBalanceScore}");
        embed.AddField("Off-Role Penalty", $"{inhouseMatch.MatchShuffler.OffRolePenalty}");
        embed.AddField("Final Score", $"{inhouseMatch.MatchShuffler.Score}");

        await ReplyAsync(embed: embed.Build());
        return;
      }
    }

    [Command()]
  }
}