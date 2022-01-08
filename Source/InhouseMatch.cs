using Discord;
using IronPython.Runtime;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;

namespace Rattletrap
{
  public class MatchShuffler
  {
    private Player[] BestShuffle = null;
    private PlayerCollection Players = null;
    private static Random Rand = new Random();
    public int Score = 0;
    public int[] TeamRankValues = new int[2] {0, 0};
    public int OffRolePenalty = 0;
    public bool[] PlayersOnRole = null;
    public int RankBalanceScore = 0;

    public MatchShuffler(PlayerCollection InPlayers)
    {
      Players = InPlayers;

      foreach(Player player in Players.Players)
      {
        player.FillPlayerRolesFromRoles();
        player.FillRankMedalFromRoles();
      }
    }

    public void ShuffleTeams(int InNumIterations)
    {
      Score = -1;
      for(int iterationIdx = 0; iterationIdx < InNumIterations; ++iterationIdx)
      {
        Player[] shuffle = Players.Players.OrderBy(a => Rand.Next()).ToArray();

        int shuffOffRolePenalty = 0;

        int[] shuffTeamRankValues = new int[2] {0, 0};

        bool[] shuffPlayersOnRole = new bool[Players.Players.Count];

        for(int playerIdx = 0; playerIdx < Players.Players.Count; ++playerIdx)
        {
          Player player = shuffle[playerIdx];

          EPlayerRole shuffledRole = Enum.GetValues<EPlayerRole>()[playerIdx % 5];
          bool isOnRole = player.PlayerRoles.Contains(shuffledRole);
          if(!isOnRole)
          {
            int[] offRolePenalties = new int[9] {6, 10, 8, 6, 5, 4, 3, 2, 1 };

            shuffOffRolePenalty += offRolePenalties[(int)player.RankMedal];
          }

          shuffPlayersOnRole[playerIdx] = isOnRole;

          int[] rankValues = new int[9] {3, 1, 2, 3, 4, 5, 6, 8, 10};

          int teamIdx = playerIdx >= Players.Players.Count / 2 ? 1 : 0;
          shuffTeamRankValues[teamIdx] += rankValues[(int)player.RankMedal];
        }

        int rankBalanceDiff = Math.Abs(TeamRankValues[0] - TeamRankValues[1]);
        int shuffRankBalanceScore = 100 - Math.Clamp(rankBalanceDiff * 4, 0, 60);

        shuffOffRolePenalty = Math.Clamp(shuffOffRolePenalty, 0, 40);

        int finalScore = Math.Clamp(shuffRankBalanceScore - shuffOffRolePenalty, 0, 100);

        if(finalScore > Score)
        {
          BestShuffle = shuffle;
          Score = finalScore;
          OffRolePenalty = shuffOffRolePenalty;
          TeamRankValues[0] = shuffTeamRankValues[0];
          TeamRankValues[1] = shuffTeamRankValues[1];
          PlayersOnRole = shuffPlayersOnRole;
          RankBalanceScore = shuffRankBalanceScore;
        }
      }
    }

    public Player GetPlayer(int InTeamIdx, EPlayerRole InPlayerRole)
    {
      if(BestShuffle == null)
      {
        return null;
      }

      if(InTeamIdx < 0 || InTeamIdx > 1)
      {
        return null;
      }

      return BestShuffle[InTeamIdx * 5 + (int)InPlayerRole];
    }
  };

  public struct InhouseMatchCandidateData
  {
    public bool IsPracticeMatch;
    public string Region;
  };

  public class InhouseMatch2 : IMatch2
  {
    public Poll ReadyupPoll = null;

    public Lobby MatchLobby = null;

    public MatchShuffler MatchShuffler {get; private set;}

    public static List<MatchCandidate> CheckForMatches(PlayerCollection InPlayers)
    {
      PlayerCollection practicePlayers = InPlayers.FilterByMode("practice");
      List<MatchCandidate> result = new List<MatchCandidate>();
      if(practicePlayers.Players.Count >= 10)
      {
        MatchCandidate resultMatch = new MatchCandidate();
        resultMatch.Players = new PlayerCollection(InPlayers.Players.GetRange(0, 10));
        resultMatch.UserData = new InhouseMatchCandidateData {IsPracticeMatch = true};
        result.Add(resultMatch);
      }

      PlayerCollection inhousePlayers = InPlayers.FilterByMode("inhouse");
      foreach(string region in GuildInstance.AvailableRegions)
      {
        inhousePlayers.FilterByRegion(region);
        if(inhousePlayers.Players.Count >= 10)
        {
          MatchCandidate resultMatch = new MatchCandidate();
          resultMatch.Players = new PlayerCollection(InPlayers.Players.GetRange(0, 10));
          resultMatch.UserData = new InhouseMatchCandidateData {IsPracticeMatch = false};
          result.Add(resultMatch);
        }
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
        MatchShuffler = new MatchShuffler(Players);
        MatchShuffler.ShuffleTeams(100000);
        
        string description = "Recommended teams: \n\n";
        for(int teamIdx = 0; teamIdx < 2; ++teamIdx)
        {
          description += $"Team {teamIdx + 1}:\n";
          for(int roleIdx = 0; roleIdx < 5; ++roleIdx)
          {
            EPlayerRole role = Enum.GetValues<EPlayerRole>()[roleIdx];
            Player player = MatchShuffler.GetPlayer(teamIdx, role);
            description += $"{role.ToString()}: {GuildInstance.RanksToEmotes[player.RankMedal]} {player.GuildUser.Mention}";
            description += MatchShuffler.PlayersOnRole[teamIdx * 5 + roleIdx] ? "\n" : " (off-role)\n";
          }
          description += "\n";
        }

        LobbyCreateInfo lobbyCreateInfo = new LobbyCreateInfo();
        lobbyCreateInfo.Name = $"DIHM Match {Id}";
        lobbyCreateInfo.Password = "butts";
        Lobby lobby = new Lobby(lobbyCreateInfo);

        description += $"Lobby name: *{lobbyCreateInfo.Name}*\n";
        description += $"Lobby password: *{lobbyCreateInfo.Password}*\n";

        EmbedBuilder matchReadyEmbed = new EmbedBuilder();
        matchReadyEmbed.WithColor(Color.Green);
        matchReadyEmbed.WithTitle("Match Ready");
        matchReadyEmbed.WithDescription(description);
        matchReadyEmbed.WithFooter($"Match ID: {Id}");
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

    public override string GetMatchInfo() 
    {
      string result = "";

      foreach(Player player in Players.Players)
      {
        result += player.GuildUser.Nickname == null ? player.GuildUser.Username 
          : player.GuildUser.Nickname + " ";
      }

      return result;
    }
  };
}