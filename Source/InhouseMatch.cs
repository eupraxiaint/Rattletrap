using Discord;
using IronPython.Runtime;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Scripting.Utils;
using Discord.Rest;
using System.Reflection.Metadata;

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
        player.UpdatePlayerRolesFromPoll();
        player.UpdateRankMedalFromPoll();
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
            int[] offRolePenalties = new int[11] {6, 10, 8, 6, 5, 4, 3, 2, 2, 1, 1 };

            shuffOffRolePenalty += offRolePenalties[(int)player.RankMedal];
          }

          shuffPlayersOnRole[playerIdx] = isOnRole;

          int[] rankValues = new int[11] {3, 1, 2, 3, 4, 5, 6, 8, 9, 10, 11};

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

    public void SwapPlayers(Player InA, Player InB)
    {
      int aIdx = Array.FindIndex(BestShuffle, 0, 10, x => x == InA);
      int bIdx = Array.FindIndex(BestShuffle, 0, 10, x => x == InB);

      Player swap = BestShuffle[aIdx];
      BestShuffle[aIdx] = BestShuffle[bIdx];
      BestShuffle[bIdx] = swap;
    }
  };

  public class InhouseMatchCandidateData
  {
    public bool IsPracticeMatch;
    public string Region;
  };

  public class InhouseMatch2 : IMatch2
  {
    public PermaPoll ReadyupPoll = null;
    public Lobby MatchLobby = null;
    public MatchShuffler MatchShuffler {get; private set;}
    public PermaPoll MatchAnnounceWidget = null;
    private bool MatchAnnounceWidgetIsDirty = true;
    private Player NominatedSwapPlayer = null;
    public Lobby lobby = null;
    public LobbyRunner LobbyRunner;
    private bool TeamAssignmentActive = false;
    public MatchChannelSet MatchChannelSet = null;
    public LobbyAnnounceWidget LobbyAnnounceWidget = null;
    public string Region = "";
    public PermaPoll CoinFlipStage1 = null;
    public PermaPoll CoinFlipStage2 = null;
    private bool CoinFlipTeam1WasFirst;
    private bool CoinFlipTeam1Faction;
    private bool CoinFlipTeam1PicksFirst;
    public bool IsPracticeMatch;

    public static List<MatchCandidate> CheckForMatches(PlayerCollection InPlayers, List<MatchmakingPoke> OutPokes)
    {
      if(InPlayers.Players.Count == 0)
      {
        return new List<MatchCandidate>();
      }

      GuildInstance guildInst = GuildInstance.Get(InPlayers.Players[0].GuildUser.Guild);

      PlayerCollection practicePlayers = InPlayers.FilterByMode("practice");
      List<MatchCandidate> result = new List<MatchCandidate>();
      if(practicePlayers.Players.Count >= 10)
      {
        MatchCandidate resultMatch = new MatchCandidate();
        resultMatch.Players = new PlayerCollection(InPlayers.Players.GetRange(0, 10));

        int[] playerCountPerRegion = new int[guildInst.AvailableRegions.Count];

        foreach(Player player in resultMatch.Players.Players)
        {
          foreach(string region in player.Regions)
          {
            playerCountPerRegion[guildInst.AvailableRegions.IndexOf(region)]++;
          }
        }

        string selectedRegion = guildInst.AvailableRegions[0];
        int selectedRegionPlayers = playerCountPerRegion[0];
        for(int i = 1; i < guildInst.AvailableRegions.Count; ++i)
        {
          if(playerCountPerRegion[i] > selectedRegionPlayers)
          {
            selectedRegion = guildInst.AvailableRegions[i];
            selectedRegionPlayers = playerCountPerRegion[i];
          }
        }

        resultMatch.UserData = new InhouseMatchCandidateData {IsPracticeMatch = true, Region = selectedRegion};
        result.Add(resultMatch);
      }
      else if(practicePlayers.Players.Count >= 7)
      {
        PlayerCollection activeUnqueuedPlayers = 
          guildInst.GetAllActivePlayers().FilterByActive(true).FilterByQueued(false).FilterByMode("practice");
        foreach(Player player in activeUnqueuedPlayers.Players)
        {
          MatchmakingPoke poke = new MatchmakingPoke();
          poke.Player = player;
          poke.Message = $"A `practice` match has {practicePlayers.Players.Count}/10 players.";
          OutPokes.Add(poke);
        }
      }

      PlayerCollection inhousePlayers = InPlayers.FilterByMode("inhouse");
      foreach(string region in guildInst.AvailableRegions)
      {
        inhousePlayers.FilterByRegion(region);
        if(inhousePlayers.Players.Count >= 10)
        {
          MatchCandidate resultMatch = new MatchCandidate();
          resultMatch.Players = new PlayerCollection(InPlayers.Players.GetRange(0, 10));
          resultMatch.UserData = new InhouseMatchCandidateData {IsPracticeMatch = false, Region = region};
          result.Add(resultMatch);
        }
      }

      return result;
    }

    public override async void Initialize(object InUserData)
    {
      ReadyupPoll = GuildInstance.CreateWidget<PermaPoll>(GuildInstance.ReadyUpChannel, null);

      PermaPollInitInfo readyUpPollCreateInfo = new PermaPollInitInfo();
      readyUpPollCreateInfo.Embed = new EmbedBuilder();
      readyUpPollCreateInfo.Embed.Color = Color.Orange;
      readyUpPollCreateInfo.Embed.Title = "Match Ready";
      readyUpPollCreateInfo.Embed.Description = "An `inhouse` match has been found. Please use the emoji reactions below to ready up " 
        + "or decline.";
      readyUpPollCreateInfo.Exclusive = true;
      readyUpPollCreateInfo.Timeout = TimeSpan.FromMinutes(5);
      readyUpPollCreateInfo.ShowResponses = true;
      readyUpPollCreateInfo.MentionUsers = true;

      List<IGuildUser> users = new List<IGuildUser>();
      foreach(Player player in Players.Players)
      {
        users.Add(player.GuildUser);
      }

      readyUpPollCreateInfo.Users = users;

      readyUpPollCreateInfo.Reactions = new List<IEmote> { StaticEmotes.WhiteHeavyCheckMark, StaticEmotes.CrossMark };

      await ReadyupPoll.Initialize(readyUpPollCreateInfo);

      ReadyupPoll.OnAllUsersResponded += OnAllUsersResponded;
      ReadyupPoll.OnTimeout += OnReadyupTimeout;

      InhouseMatchCandidateData userData = InUserData as InhouseMatchCandidateData;
      Region = userData.Region;
      IsPracticeMatch = userData.IsPracticeMatch;
    }

    private async void OnReadyupTimeout()
    {
      PermaPollReactionEntry acceptPollEntry = ReadyupPoll.ReactionEntries[0];

      EmbedBuilder embed = new EmbedBuilder();
      embed.WithColor(Color.Red);
      embed.WithTitle("Match Timed Out");

      string description = "Returning to queue:\n";
      foreach(IGuildUser user in acceptPollEntry.Users)
      {
        description += user.Mention + " ";
      }

      embed.WithDescription(description);

      await ReadyupPoll.CreateOrEditMessage(embed);

      foreach(IGuildUser user in acceptPollEntry.Users)
      {
        Player player = Player.GetOrCreate(user);
        GuildInstance.QueuePlayer(player, false, false);
      }

      GuildInstance.CheckForMatches();
    }

    private async void OnAllUsersResponded()
    {
      ReadyupPoll.Close();

      PermaPollReactionEntry acceptPollEntry = ReadyupPoll.ReactionEntries[0];
      if(acceptPollEntry.Users.Count == Players.Players.Count)
      {
        LobbyRunner = LobbyRunner.GetAvailableLobbyRunner();
        LobbyRunner.AssignMatch(this);

        LobbyRunner.OnMatchEnded += OnMatchEnded;

        MatchChannelSet = GuildInstance.CreateMatchChannelSet(this);

        EmbedBuilder readyupEmbed = new EmbedBuilder();
        readyupEmbed.WithTitle("Match Accepted");
        readyupEmbed.WithColor(Color.Green);

        string description = $"The match will take place in {MatchChannelSet.AnnouncementsChannel.Mention}.\n\n";
        foreach(IGuildUser user in acceptPollEntry.Users)
        {
          description += user.Mention + " ";
        }

        readyupEmbed.WithDescription(description);
        await ReadyupPoll.CreateOrEditMessage(readyupEmbed);

        MatchShuffler = new MatchShuffler(Players);
        MatchShuffler.ShuffleTeams(100);
        
        MatchAnnounceWidget = GuildInstance.CreateWidget<PermaPoll>(MatchChannelSet.AnnouncementsChannel, null);

        EmbedBuilder matchAnnounceEmbed = new EmbedBuilder();
        PermaPollInitInfo matchAnnounceWidgetInitInfo = new PermaPollInitInfo();
        matchAnnounceWidgetInitInfo.Embed = matchAnnounceEmbed;
        matchAnnounceWidgetInitInfo.Reactions = new List<IEmote> { StaticEmotes.TriangularFlag, 
          StaticEmotes.CounterclockwiseArrows, StaticEmotes.WhiteHeavyCheckMark };
        matchAnnounceWidgetInitInfo.Exclusive = true;
        matchAnnounceWidgetInitInfo.Timeout = TimeSpan.FromMinutes(2);
        matchAnnounceWidgetInitInfo.MentionUsers = true;

        await MatchAnnounceWidget.Initialize(matchAnnounceWidgetInitInfo);
        MatchAnnounceWidget.OnReactionModified += MatchAnnounceReactionModified;
        MatchAnnounceWidget.OnTimeout += MatchAnnounceTimeout;
        
        TeamAssignmentActive = true;

        Task.Run(RunTeamAssignment);
      }
      else
      {
        EmbedBuilder embed = new EmbedBuilder();
        embed.WithTitle("Match Declined");
        embed.WithColor(Color.Red);

        string description = "Returning to queue:\n";
        foreach(IGuildUser user in acceptPollEntry.Users)
        {
          description += user.Mention + " ";
        }

        embed.WithDescription(description);

        await ReadyupPoll.CreateOrEditMessage(embed);

        foreach(IGuildUser user in acceptPollEntry.Users)
        {
          Player player = Player.GetOrCreate(user);
          GuildInstance.QueuePlayer(player, false, false);
        }

        GuildInstance.CheckForMatches();
      }
    }

    private void MatchAnnounceTimeout()
    {
      TeamAssignmentActive = false;
    }

    private async void UpdateMatchAnnouncementWidget()
    {
      EmbedBuilder matchAnnounceEmbed = new EmbedBuilder();
      matchAnnounceEmbed.WithTitle($"Match {Id} - Team Assignment");
      matchAnnounceEmbed.WithColor(Color.DarkBlue);
      matchAnnounceEmbed.WithCurrentTimestamp();

      for(int teamIdx = 0; teamIdx < 2; ++teamIdx)
      {
        string teamText = "";
        for(int roleIdx = 0; roleIdx < 5; ++roleIdx)
        {
          EPlayerRole role = Enum.GetValues<EPlayerRole>()[roleIdx];
          Player player = MatchShuffler.GetPlayer(teamIdx, role);
          IEmote roleEmote = StaticEmotes.GetPlayerRoleEmoteFromEnum(role);
          IEmote flairEmote = player.Flair;
          IEmote rankEmote = StaticEmotes.GetRankMedalEmoteFromEnum(player.RankMedal);

          string pollEmote = "";
          List<IEmote> reactions = MatchAnnounceWidget.GetReactionsForUser(player.GuildUser);
          if(reactions.Count == 1)
          {
            pollEmote = reactions[0].ToString();
          }
          teamText += $"{roleEmote} {flairEmote}{player.GuildUser.Mention}{rankEmote}  {pollEmote}\n";
        }
        matchAnnounceEmbed.AddField($"Team {teamIdx + 1}", teamText);
      }

      matchAnnounceEmbed.AddField("Swap, Shuffle, Or Continue", $"Click {StaticEmotes.TriangularFlag} to nominate yourself"
        + $" to swap, click {StaticEmotes.CounterclockwiseArrows} to vote to reshuffle, or click "
        + $"{StaticEmotes.WhiteHeavyCheckMark} to vote to continue. 7 votes are required to pass. You have two minutes "
        + $"before teams are locked.");

      string mentions = "";
      foreach(Player player in Players.Players)
      {
        mentions += player.GuildUser.Mention + " ";
      }

      await MatchAnnounceWidget.CreateOrEditMessage(matchAnnounceEmbed, mentions);
    }

    private async void RunTeamAssignment()
    {
      while(TeamAssignmentActive)
      {
        if(MatchAnnounceWidgetIsDirty)
        {
          UpdateMatchAnnouncementWidget();
          MatchAnnounceWidgetIsDirty = false;
        }
        await Task.Delay(TimeSpan.FromSeconds(1));
      }
    }

    private async Task MatchAnnounceReactionModified(IEmote InEmote, IGuildUser InUser, bool InAdded)
    {
      if(!TeamAssignmentActive)
      {
        return;
      }

      if(InAdded)
      {
        if(InEmote.Name == StaticEmotes.TriangularFlag.Name)
        {
          if(NominatedSwapPlayer == null)
          {
            NominatedSwapPlayer = Player.GetOrCreate(InUser);
          }
          else
          {
            Player player = Player.GetOrCreate(InUser);
            MatchShuffler.SwapPlayers(player, NominatedSwapPlayer);
            MatchAnnounceWidget.RemoveResponse(StaticEmotes.TriangularFlag, InUser);
            MatchAnnounceWidget.RemoveResponse(StaticEmotes.TriangularFlag, NominatedSwapPlayer.GuildUser);
            NominatedSwapPlayer = null;
          }
        }
        else if(InEmote.Name == StaticEmotes.CounterclockwiseArrows.Name)
        {
          List<IGuildUser> reactedUsers = new List<IGuildUser>(MatchAnnounceWidget.GetUsersForEmote(StaticEmotes.CounterclockwiseArrows));
          if(reactedUsers.Count >= 7)
          {
            MatchShuffler.ShuffleTeams(100);
            foreach(IGuildUser user in reactedUsers)
            {
              MatchAnnounceWidget.RemoveResponse(StaticEmotes.CounterclockwiseArrows, user);
            }
          }
        }
        else if(InEmote.Name == StaticEmotes.WhiteHeavyCheckMark.Name)
        {
          List<IGuildUser> reactedUsers = new List<IGuildUser>(MatchAnnounceWidget.GetUsersForEmote(StaticEmotes.WhiteHeavyCheckMark));
          if(reactedUsers.Count >= 7)
          {
            MatchAnnounceWidget.Close();

            TeamAssignmentActive = false;

            CoinFlipTeam1WasFirst = Random.Shared.Next() % 2 == 0;

            PermaPollInitInfo stage1CoinFlipPollInitInfo = new PermaPollInitInfo();
            stage1CoinFlipPollInitInfo.Embed = new EmbedBuilder();
            stage1CoinFlipPollInitInfo.Embed.Title = "Draft Order / Faction Assignment - Stage 1";
            stage1CoinFlipPollInitInfo.Embed.Color = Color.Green;
            stage1CoinFlipPollInitInfo.Embed.Description = "Please make a selection. You have 1 minute to respond.\n"
              + $"{StaticEmotes.Tent}: Radiant\n{StaticEmotes.CityscapeAtDusk}: Dire\n"
              + $"{StaticEmotes.PointUp}: First pick\n{StaticEmotes.VictoryHand}: Second pick";
            stage1CoinFlipPollInitInfo.MentionUsers = true;
            stage1CoinFlipPollInitInfo.Users = new List<IGuildUser>();
            stage1CoinFlipPollInitInfo.Reactions = new List<IEmote>{StaticEmotes.Tent, StaticEmotes.CityscapeAtDusk, 
              StaticEmotes.PointUp, StaticEmotes.VictoryHand};
            stage1CoinFlipPollInitInfo.Timeout = TimeSpan.FromMinutes(1);
            stage1CoinFlipPollInitInfo.ShowResponses = true;

            for(int i = 0; i < 5; ++i)
            {
              Player player = MatchShuffler.GetPlayer(CoinFlipTeam1WasFirst ? 0 : 1, 
                Enum.GetValues<EPlayerRole>()[i]);
              stage1CoinFlipPollInitInfo.Users.Add(player.GuildUser);
            }

            CoinFlipStage1 = GuildInstance.CreateWidget<PermaPoll>(MatchChannelSet.AnnouncementsChannel, null);
            await CoinFlipStage1.Initialize(stage1CoinFlipPollInitInfo);

            CoinFlipStage1.OnAllUsersResponded += AdvanceCoinFlipStage1;
            CoinFlipStage1.OnTimeout += AdvanceCoinFlipStage1;
          }
        }
      }
      if(!InAdded)
      {
        if(InEmote.Name == StaticEmotes.TriangularFlag.Name)
        {
          if(NominatedSwapPlayer != null && NominatedSwapPlayer.GuildUser == InUser)
          {
            NominatedSwapPlayer = null;
          }
        }
      }

      MatchAnnounceWidgetIsDirty = true;
    }

    private async void AdvanceCoinFlipStage1()
    {
      int maxResponsesIdx = 0;
      for(int i = 1; i < CoinFlipStage1.ReactionEntries.Count; ++i)
      {
        if(CoinFlipStage1.ReactionEntries[i].Users.Count > 
          CoinFlipStage1.ReactionEntries[maxResponsesIdx].Users.Count)
        {
          maxResponsesIdx = i;
        }
      }

      PermaPollInitInfo stage2CoinFlipPollInitInfo = new PermaPollInitInfo();
      stage2CoinFlipPollInitInfo.Embed = new EmbedBuilder();
      stage2CoinFlipPollInitInfo.Embed.Title = "Draft Order / Faction Assignment - Stage 2";
      stage2CoinFlipPollInitInfo.Embed.Color = Color.Green;

      if(maxResponsesIdx < 2)
      {
        CoinFlipTeam1Faction = CoinFlipTeam1PicksFirst ? maxResponsesIdx == 0 : maxResponsesIdx == 1;
        stage2CoinFlipPollInitInfo.Embed.Description = "Please make a selection. You have 1 minute to respond.\n"
        + $"{StaticEmotes.PointUp}: First pick\n{StaticEmotes.VictoryHand}: Second pick";
        stage2CoinFlipPollInitInfo.Reactions = new List<IEmote>{StaticEmotes.PointUp, StaticEmotes.VictoryHand};

        EmbedBuilder stage1Embed = new EmbedBuilder();
        stage1Embed.Title = "Draft Order / Faction Assignment - Stage 1";
        stage1Embed.Color = Color.Green;
        stage1Embed.Description = $"Team {(CoinFlipTeam1WasFirst ? 1 : 2)} selected **{(maxResponsesIdx == 1 ? "Dire" : "Radiant")}**.\n";

        string radiantTeamText = "";
        for(int i = 0; i < 5; ++i)
        {
          Player player = MatchShuffler.GetPlayer(CoinFlipTeam1Faction ? 1 : 0, Enum.GetValues<EPlayerRole>()[i]);
          radiantTeamText += player.Flair + player.GuildUser.Mention + "\n";
        }

        stage1Embed.AddField("Radiant", radiantTeamText);

        string direTeamText = "";
        for(int i = 0; i < 5; ++i)
        {
          Player player = MatchShuffler.GetPlayer(CoinFlipTeam1Faction ? 0 : 1, Enum.GetValues<EPlayerRole>()[i]);
          direTeamText += player.Flair + player.GuildUser.Mention + "\n";
        }

        stage1Embed.AddField("Dire", direTeamText);

        await CoinFlipStage1.CreateOrEditMessage(stage1Embed);
      }
      else
      {
        CoinFlipTeam1PicksFirst = CoinFlipTeam1PicksFirst ? maxResponsesIdx == 2 : maxResponsesIdx == 3;
        stage2CoinFlipPollInitInfo.Embed.Description = "Please make a selection. You have 1 minute to respond.\n"
        + $"{StaticEmotes.Tent}: Radiant\n{StaticEmotes.CityscapeAtDusk}: Dire";
        stage2CoinFlipPollInitInfo.Reactions = new List<IEmote>{StaticEmotes.Tent, StaticEmotes.CityscapeAtDusk};

        EmbedBuilder stage1Embed = new EmbedBuilder();
        stage1Embed.Title = "Draft Order / Faction Assignment - Stage 1";
        stage1Embed.Color = Color.Green;
        stage1Embed.Description = $"Team {(CoinFlipTeam1WasFirst ? 1 : 2)} selected **{(CoinFlipTeam1PicksFirst ? "first pick" : "second pick")}**.\n";
      
        await CoinFlipStage1.CreateOrEditMessage(stage1Embed);
      }
      
      stage2CoinFlipPollInitInfo.MentionUsers = true;
      stage2CoinFlipPollInitInfo.Users = new List<IGuildUser>();
      
      stage2CoinFlipPollInitInfo.Timeout = TimeSpan.FromMinutes(1);
      stage2CoinFlipPollInitInfo.ShowResponses = true;

      for(int i = 0; i < 5; ++i)
      {
        Player player = MatchShuffler.GetPlayer(CoinFlipTeam1WasFirst ? 1 : 0, 
          Enum.GetValues<EPlayerRole>()[i]);
        stage2CoinFlipPollInitInfo.Users.Add(player.GuildUser);
      }

      CoinFlipStage2 = GuildInstance.CreateWidget<PermaPoll>(MatchChannelSet.AnnouncementsChannel, null);
      await CoinFlipStage2.Initialize(stage2CoinFlipPollInitInfo);

      CoinFlipStage2.OnAllUsersResponded += AdvanceCoinFlipStage2;
      CoinFlipStage2.OnTimeout += AdvanceCoinFlipStage2;
    }

    private async void AdvanceCoinFlipStage2()
    {
      bool secondResponseIsHigher = CoinFlipStage2.ReactionEntries[1].Users.Count 
        > CoinFlipStage2.ReactionEntries[0].Users.Count;

      if(CoinFlipStage2.ReactionEntries[0].Emote.Name == StaticEmotes.Tent.Name)
      {
        CoinFlipTeam1Faction = CoinFlipTeam1PicksFirst ? !secondResponseIsHigher : secondResponseIsHigher;

        EmbedBuilder stage2Embed = new EmbedBuilder();
        stage2Embed.Title = "Draft Order / Faction Assignment - Stage 2";
        stage2Embed.Color = Color.Green;
        stage2Embed.Description = $"Team {(CoinFlipTeam1WasFirst ? 2 : 1)} selected **{(secondResponseIsHigher ? "Dire" : "Radiant")}**.\n";

        string radiantTeamText = "";
        for(int i = 0; i < 5; ++i)
        {
          Player player = MatchShuffler.GetPlayer(CoinFlipTeam1Faction ? 1 : 0, Enum.GetValues<EPlayerRole>()[i]);
          radiantTeamText += player.Flair + player.GuildUser.Mention + "\n";
        }

        stage2Embed.AddField("Radiant", radiantTeamText);

        string direTeamText = "";
        for(int i = 0; i < 5; ++i)
        {
          Player player = MatchShuffler.GetPlayer(CoinFlipTeam1Faction ? 0 : 1, Enum.GetValues<EPlayerRole>()[i]);
          direTeamText += player.Flair + player.GuildUser.Mention + "\n";
        }

        stage2Embed.AddField("Dire", direTeamText);

        await CoinFlipStage2.CreateOrEditMessage(stage2Embed);
      }
      else
      {
        EmbedBuilder stage2Embed = new EmbedBuilder();
        stage2Embed.Title = "Draft Order / Faction Assignment - Stage 2";
        stage2Embed.Color = Color.Green;
        stage2Embed.Description = $"Team {(CoinFlipTeam1WasFirst ? 2 : 1)} selected **{(secondResponseIsHigher ? "second pick" : "first pick")}**.\n";
      
        await CoinFlipStage2.CreateOrEditMessage(stage2Embed);
      }

      LobbyCreateInfo lobbyCreateInfo = new LobbyCreateInfo();
      lobbyCreateInfo.Name = $"DIHM Match {Id}";
      lobbyCreateInfo.Password = "butts";
      lobbyCreateInfo.GameMode = IsPracticeMatch ? ELobbyGameMode.AllPick : ELobbyGameMode.CaptainsMode;
      lobbyCreateInfo.Region = Region;

      if(CoinFlipTeam1Faction)
      {
        lobbyCreateInfo.CmPick = CoinFlipTeam1PicksFirst ? ELobbyCmPick.Dire : ELobbyCmPick.Radiant;
      }
      else
      {
        lobbyCreateInfo.CmPick = CoinFlipTeam1PicksFirst ? ELobbyCmPick.Radiant : ELobbyCmPick.Dire;
      }

      LobbyRunner.CreateLobby(lobbyCreateInfo);

      LobbyAnnounceWidgetInitInfo lobbyAnnounceWidgetInitInfo = new LobbyAnnounceWidgetInitInfo();
      lobbyAnnounceWidgetInitInfo.Name = lobbyCreateInfo.Name;
      lobbyAnnounceWidgetInitInfo.Password = lobbyCreateInfo.Password;
      lobbyAnnounceWidgetInitInfo.Players = Players;
    
      LobbyAnnounceWidget = GuildInstance.CreateWidget<LobbyAnnounceWidget>(MatchChannelSet.AnnouncementsChannel, 
        null);
      LobbyAnnounceWidget.Initialize(lobbyAnnounceWidgetInitInfo);

      LobbyAnnounceWidget.OnCanceled += OnLobbyCanceled;
      LobbyAnnounceWidget.OnAllPlayersReady += OnAllPlayersReady;
      LobbyAnnounceWidget.OnTimeout += OnLobbyTimeout;
    }

    private async void OnLobbyCanceled(Player InCancelingPlayer)
    {
      LobbyRunner.Reset();
      LobbyRunner.OnMatchEnded -= OnMatchEnded;

      EmbedBuilder embed = new EmbedBuilder();
      embed.Color = Color.Red;
      embed.Title = "Match Canceled";
      embed.Description = $"Match canceled by {InCancelingPlayer.GuildUser.Mention}. This lobby will be reclaimed in 5 minutes.";
      embed.Timestamp = DateTime.Now;
      await LobbyAnnounceWidget.CreateOrEditMessage(embed);
      LobbyAnnounceWidget.Close();

      await Task.Delay(TimeSpan.FromMinutes(5));
      GuildInstance.DestroyMatchChannelSet(MatchChannelSet);
    }

    private async void OnAllPlayersReady()
    {
      LobbyRunner.StartMatchResult result = LobbyRunner.StartMatch().Result;

      if(result == LobbyRunner.StartMatchResult.Success)
      {
        EmbedBuilder startMessage = new EmbedBuilder();
        startMessage.Color = Color.Green;
        startMessage.Description = "Starting match...";
        startMessage.Timestamp = DateTime.Now;
        await MatchChannelSet.AnnouncementsChannel.SendMessageAsync(embed: startMessage.Build());
        LobbyAnnounceWidget.Close();
      }
      else if(result == LobbyRunner.StartMatchResult.NoPlayers)
      {
        EmbedBuilder startMessage = new EmbedBuilder();
        startMessage.Color = Color.Red;
        startMessage.Description = "Failed to start: no players in lobby.";
        startMessage.Timestamp = DateTime.Now;
        await MatchChannelSet.AnnouncementsChannel.SendMessageAsync(embed: startMessage.Build());

        List<IGuildUser> readyUsers = new List<IGuildUser>(LobbyAnnounceWidget.GetUsersForEmote(StaticEmotes.WhiteHeavyCheckMark));

        foreach(IGuildUser user in readyUsers)
        {
          await LobbyAnnounceWidget.RemoveReaction(StaticEmotes.WhiteHeavyCheckMark, user);
        }
      }
    }

    private async void OnLobbyTimeout()
    {
      LobbyRunner.Reset();
      LobbyRunner.OnMatchEnded -= OnMatchEnded;

      EmbedBuilder embed = new EmbedBuilder();
      embed.Color = Color.Red;
      embed.Title = "Lobby Timed Out";
      embed.Description = $"The lobby timed out. This lobby will be reclaimed in 5 minutes.";
      embed.Timestamp = DateTime.Now;
      await LobbyAnnounceWidget.CreateOrEditMessage(embed);
      LobbyAnnounceWidget.Close();

      await Task.Delay(TimeSpan.FromMinutes(5));
      GuildInstance.DestroyMatchChannelSet(MatchChannelSet);
    }

    private async void OnMatchEnded()
    {
      LobbyRunner.Reset();
      LobbyRunner.OnMatchEnded -= OnMatchEnded;

      EmbedBuilder matchEndedEmbed = new EmbedBuilder();
      matchEndedEmbed.Color = Color.Green;
      matchEndedEmbed.Description = "Match ended. This lobby will be reclaimed in 5 minutes.";

      await MatchChannelSet.AnnouncementsChannel.SendMessageAsync(embed: matchEndedEmbed.Build());

      await Task.Delay(TimeSpan.FromMinutes(5));
      GuildInstance.DestroyMatchChannelSet(MatchChannelSet);
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