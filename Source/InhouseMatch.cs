using Discord;
using IronPython.Runtime;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Rattletrap
{
  public class InhouseMatch2 : IMatch2
  {
    public Poll ReadyupPoll = null;

    public Lobby MatchLobby = null;

    public static List<MatchCandidate> CheckForMatches(PlayerCollection InPlayers)
    {
      PlayerCollection modePlayers = InPlayers.FilterByMode("inhouse");
      List<MatchCandidate> result = new List<MatchCandidate>();
      if(modePlayers.Players.Count >= 4)
      {
        MatchCandidate resultMatch = new MatchCandidate();
        resultMatch.Players = new PlayerCollection(InPlayers.Players.GetRange(0, 4));
        result.Add(resultMatch);
      }

      return result;
    }

    public override void Initialize()
    {
      PollCreateInfo pollCreateInfo;
      pollCreateInfo.Title = "Match Found";
      pollCreateInfo.Message = "An `inhouse` match has been found. Please use the emoji reactions below to ready up " 
        + "or decline.\n\n";

      foreach(Player player in Players.Players)
      {
        pollCreateInfo.Message += $"  - {player.GuildUser.Mention}\n";
      }

      pollCreateInfo.Players = Players;
      pollCreateInfo.Responses = new List<IEmote> { new Emoji("\u2705"), new Emoji("\u274C") };
      pollCreateInfo.Timeout = TimeSpan.FromMinutes(5);
      pollCreateInfo.Channel = GuildInstance.AnnouncementChannel;

      Task<Poll> pollTask = Poll.Create(pollCreateInfo);

      pollTask.Wait();

      ReadyupPoll = pollTask.Result;
      pollTask.Result.OnAllPlayersResponded += OnAllPlayersResponded;
      pollTask.Result.OnTimeout += OnTimeout;
    }

    private void OnAllPlayersResponded()
    {
      PollResponse acceptResponse = ReadyupPoll.Responses[0];
      if(acceptResponse.Players.Players.Count == Players.Players.Count)
      {
        EmbedBuilder matchReadyEmbed = new EmbedBuilder();
        matchReadyEmbed.WithColor(Color.Green);
        matchReadyEmbed.WithTitle("Match Ready");
        matchReadyEmbed.WithDescription("let's go motherfuckaaaas");
        matchReadyEmbed.WithCurrentTimestamp();
        GuildInstance.AnnouncementChannel.SendMessageAsync(embed: matchReadyEmbed.Build());
      }
      else
      {
        EmbedBuilder matchDeclinedEmbed = new EmbedBuilder();
        matchDeclinedEmbed.WithColor(Color.Red);
        matchDeclinedEmbed.WithTitle("Match Declined");

        string description = "Returning to queue:\n";
        foreach(Player player in acceptResponse.Players.Players)
        {
          description += player.GuildUser.Mention + " ";
        }

        matchDeclinedEmbed.WithDescription(description);
        matchDeclinedEmbed.WithCurrentTimestamp();
        GuildInstance.AnnouncementChannel.SendMessageAsync(embed: matchDeclinedEmbed.Build());

        foreach(Player player in acceptResponse.Players.Players)
        {
          GuildInstance.QueuePlayer(player, false, false);
        }

        GuildInstance.CheckForMatches();
      }

      ReadyupPoll = null;
    }

    private void OnTimeout()
    {
      PollResponse acceptResponse = ReadyupPoll.Responses[0];
      EmbedBuilder matchTimedOutEmbed = new EmbedBuilder();
      matchTimedOutEmbed.WithColor(Color.Red);
      matchTimedOutEmbed.WithTitle("Match Timed Out");

      string description = "Returning to queue:\n";
      foreach(Player player in acceptResponse.Players.Players)
      {
        description += player.GuildUser.Mention + " ";
      }

      matchTimedOutEmbed.WithDescription(description);
      matchTimedOutEmbed.WithCurrentTimestamp();
      GuildInstance.AnnouncementChannel.SendMessageAsync(embed: matchTimedOutEmbed.Build());

      foreach(Player player in acceptResponse.Players.Players)
      {
        GuildInstance.QueuePlayer(player, false, false);
      }

      GuildInstance.CheckForMatches();

      ReadyupPoll = null;
    }
  };
}