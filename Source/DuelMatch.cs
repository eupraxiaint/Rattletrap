using Discord;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Formats.Asn1;

namespace Rattletrap
{
  class DuelMatchUserData
  {
    public string Region;
  }

  public class DuelMatch : IMatch2
  {
    public PermaPoll ReadyupPoll = null;

    public LobbyRunner LobbyRunner = null;
    public LobbyAnnounceWidget LobbyAnnounceWidget = null;
    private HashSet<Player> ReadiedPlayers = new HashSet<Player>();
    public MatchChannelSet MatchChannelSet = null;
    public string Region;

    public static List<MatchCandidate> CheckForMatches(PlayerCollection InPlayers, List<MatchmakingPoke> OutPokes)
    {
      if(InPlayers.Players.Count == 0)
      {
        return new List<MatchCandidate>();
      }

      List<MatchCandidate> result = new List<MatchCandidate>();

      GuildInstance guildInst = GuildInstance.Get(InPlayers.Players[0].GuildUser.Guild);

      PlayerCollection duelPlayers = InPlayers.FilterByMode("duel");

      foreach(string region in guildInst.AvailableRegions)
      {
        PlayerCollection regionPlayers = duelPlayers.FilterByRegion(region);

        if(regionPlayers.Players.Count >= 2)
        {
          MatchCandidate resultMatch = new MatchCandidate();
          resultMatch.Players = new PlayerCollection(InPlayers.Players.GetRange(0, 2));
          resultMatch.UserData = new DuelMatchUserData { Region = region };
          result.Add(resultMatch);  
        }
      }

      return result;
    }

    public override async void Initialize(object InUserData)
    {
      ReadyupPoll = GuildInstance.CreateWidget<PermaPoll>(
        GuildInstance.ReadyUpChannel, null);

      DuelMatchUserData userData = InUserData as DuelMatchUserData;
      Region = userData.Region;

      PermaPollInitInfo readyupPollInitInfo = new PermaPollInitInfo();
      readyupPollInitInfo.Embed = new EmbedBuilder();
      readyupPollInitInfo.Embed.Color = Color.Orange;
      readyupPollInitInfo.Embed.Title = "Match Ready";
      readyupPollInitInfo.Embed.Description = "A `duel` match has been found. Please use the emoji reactions below to ready up " 
        + "or decline.";
      readyupPollInitInfo.Exclusive = true;
      readyupPollInitInfo.Timeout = TimeSpan.FromMinutes(5);
      readyupPollInitInfo.ShowResponses = true;
      readyupPollInitInfo.MentionUsers = true;

      readyupPollInitInfo.Users = new List<IGuildUser>();

      foreach(Player player in Players.Players)
      {
        readyupPollInitInfo.Users.Add(player.GuildUser);
      }

      readyupPollInitInfo.Reactions = new List<IEmote> { 
        StaticEmotes.WhiteHeavyCheckMark, StaticEmotes.CrossMark };

      await ReadyupPoll.Initialize(readyupPollInitInfo);

      ReadyupPoll.OnAllUsersResponded += OnReadyupAllUsersResponded;
      ReadyupPoll.OnTimeout += OnReadyupTimeout;
    }

    private async void OnReadyupAllUsersResponded()
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

        LobbyCreateInfo lobbyCreateInfo;
        lobbyCreateInfo.Name = $"DIHM Match {Id}";
        lobbyCreateInfo.Password = "butts";
        lobbyCreateInfo.GameMode = ELobbyGameMode.OneVOne;
        lobbyCreateInfo.Region = Region;
        lobbyCreateInfo.CmPick = ELobbyCmPick.Random;
        LobbyRunner.CreateLobby(lobbyCreateInfo);

        LobbyAnnounceWidgetInitInfo lobbyAnnounceWidgetInitInfo = new LobbyAnnounceWidgetInitInfo();
        lobbyAnnounceWidgetInitInfo.Name = lobbyCreateInfo.Name;
        lobbyAnnounceWidgetInitInfo.Password = lobbyCreateInfo.Password;
        lobbyAnnounceWidgetInitInfo.Players = Players;
        lobbyAnnounceWidgetInitInfo.MentionUsers = true;

        LobbyAnnounceWidget = GuildInstance.CreateWidget<LobbyAnnounceWidget>(
          MatchChannelSet.AnnouncementsChannel, null);
        LobbyAnnounceWidget.Initialize(lobbyAnnounceWidgetInitInfo);
        LobbyAnnounceWidget.OnCanceled += OnLobbyCanceled;
        LobbyAnnounceWidget.OnAllPlayersReady += LobbyAllPlayersReady;
        LobbyAnnounceWidget.OnTimeout += OnLobbyTimeout;
      }
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

    private async void OnMatchEnded()
    {
      EmbedBuilder matchEndedEmbed = new EmbedBuilder();
      matchEndedEmbed.Color = Color.Green;
      matchEndedEmbed.Description = "Match ended. This lobby will be reclaimed in 5 minutes.";

      LobbyRunner.Reset();
      LobbyRunner.OnMatchEnded -= OnMatchEnded;

      await MatchChannelSet.AnnouncementsChannel.SendMessageAsync(embed: matchEndedEmbed.Build());

      await Task.Delay(TimeSpan.FromMinutes(5));
      GuildInstance.DestroyMatchChannelSet(MatchChannelSet);
    }

    private async void LobbyAllPlayersReady()
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
  }
}