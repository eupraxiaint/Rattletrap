using System;
using System.Collections.Generic;
using Discord;
using System.Threading.Tasks;
using System.Linq;

namespace Rattletrap
{
  class MatchShuffle
  {
    public int BalanceScore;
    public int RadiantRankBalanceScore = 0;
    public int DireRankBalanceScore = 0;
    public bool RadiantPositionsAreFilled;
    public bool DirePositionsAreFilled;
    public int FinalRankBalanceScore = 0;
    public List<PlayerInfo> Radiant = new List<PlayerInfo>();
    public List<PlayerInfo> Dire = new List<PlayerInfo>();
    private static Random Rand = new Random();

    private bool ArePositionsFilled(List<PlayerInfo> InPlayers, HashSet<PlayerPosition> InPositions)
    {
      if(InPlayers.Count == 0 && InPositions.Count == 0)
      {
        return true;
      }

      foreach(PlayerInfo player in InPlayers)
      {
        List<PlayerInfo> playersCopy = new List<PlayerInfo>(InPlayers);
        playersCopy.Remove(player);

        foreach(PlayerPosition position in InPositions)
        {
          if(player.Positions.Contains(position))
          {
            HashSet<PlayerPosition> positionsCopy = new HashSet<PlayerPosition>(InPositions);
            positionsCopy.Remove(position);

            if(ArePositionsFilled(playersCopy, positionsCopy))
            {
              return true;
            }
          }
        }
      }

      return false;
    }

    private int ComputeRankBalanceScore()
    {
      Dictionary<PlayerRank, int> rankValues = new Dictionary<PlayerRank, int>();
      rankValues.Add(PlayerRank.Uncalibrated, 3);
      rankValues.Add(PlayerRank.Herald, 1);
      rankValues.Add(PlayerRank.Guardian, 2);
      rankValues.Add(PlayerRank.Crusader, 3);
      rankValues.Add(PlayerRank.Archon, 4);
      rankValues.Add(PlayerRank.Legend, 5);
      rankValues.Add(PlayerRank.Ancient, 6);
      rankValues.Add(PlayerRank.Divine, 8);
      rankValues.Add(PlayerRank.Immortal, 10);

      foreach(PlayerInfo player in Radiant)
      {
        RadiantRankBalanceScore += rankValues[player.Rank];
      }

      foreach(PlayerInfo player in Dire)
      {
        DireRankBalanceScore += rankValues[player.Rank];
      }

      FinalRankBalanceScore = Math.Clamp(40 - 2 * Math.Abs(RadiantRankBalanceScore - DireRankBalanceScore), 0, 40);

      return FinalRankBalanceScore;
    }

    public MatchShuffle(IGuild InGuild, List<IGuildUser> Players)
    {
      List<IGuildUser> shuffledPlayers = Players.OrderBy(a => Rand.Next()).ToList();
      for(int i = 0; i < Players.Count; ++i)
      {
        (i >= Players.Count / 2 ? Dire : Radiant).Add(MatchService.GetPlayerInfo(InGuild, shuffledPlayers[i]));
      }

      HashSet<PlayerPosition> allPositions = new HashSet<PlayerPosition>();
      allPositions.Add(PlayerPosition.Safelane);
      allPositions.Add(PlayerPosition.Midlane);
      allPositions.Add(PlayerPosition.Offlane);
      allPositions.Add(PlayerPosition.SoftSupport);
      allPositions.Add(PlayerPosition.Support);

      RadiantPositionsAreFilled = ArePositionsFilled(Radiant, allPositions);
      DirePositionsAreFilled = ArePositionsFilled(Dire, allPositions);

      BalanceScore = 0;
      BalanceScore += RadiantPositionsAreFilled ? 30 : 0;
      BalanceScore += DirePositionsAreFilled ? 30 : 0;
      BalanceScore += ComputeRankBalanceScore();
    }
  }

  public class InhouseMatch : IMatch
  {
    public InhouseMatch(IGuild InGuild, IQueue InSourceQueue, List<IGuildUser> InPlayers)
      : base(InGuild, InSourceQueue, InPlayers)
    {

    }

    public override async void Announce()
    {
      string messageText = $"**RATTLE AND ROLL!** Found a match (id: {Id}) in queue `{SourceQueue.Name}`.\n"
        + "Please ready up or decline using the emoji reactions below. You have 5 minutes to ready up before automatically declining.\n";

      foreach(IGuildUser user in Players)
      {
        messageText += user.Mention + " ";
      }

      Task<IUserMessage> messageTask = SourceQueue.AnnouncementChannel.SendMessageAsync(messageText);
      await messageTask;
      AnnounceMessage = messageTask.Result;
      await AnnounceMessage.AddReactionAsync(MatchService.CheckmarkEmoji);
      await AnnounceMessage.AddReactionAsync(MatchService.XEmoji);
    }

    public override async void OnReady()
    {
      String message = $"All players ready! (match id: {Id}) Please create a lobby and type "
        + $"`;lobby {Id} \"<lobby-name>\" \"<lobby-password>\"`\n";

      foreach(IGuildUser player in Players)
      {
        message += player.Mention + " ";
      }

      State = MatchState.Ready;

      await SourceQueue.AnnouncementChannel.SendMessageAsync(message);
    }

    public override void OnLobby(String InName, String InPassword)
    {
      GuildInfo guildInfo = MatchService.GuildInfos[Guild];

      String message = $"Lobby is up for match id {Id}! Name: **{InName}**, Password: **{InPassword}** \nRecommended teams:\nRadiant:\n";
      
      MatchShuffle bestShuffle = null;

      for(int i = 0; i < 20; ++i)
      {
        MatchShuffle shuffle = new MatchShuffle(Guild, Players);
        if(bestShuffle == null || shuffle.BalanceScore > bestShuffle.BalanceScore)
        {
          bestShuffle = shuffle;
        }
      }

      foreach(PlayerInfo player in bestShuffle.Radiant)
      {
        message += player.User.Mention + " " + guildInfo.RanksToEmotes[player.Rank];

        foreach(PlayerPosition position in player.Positions)
        {
          message += MatchService.PositionsToEmotes[position];
        }

        message += "\n";
      }

      message += "\nDire:\n";
      
      foreach(PlayerInfo player in bestShuffle.Dire)
      {
        message += player.User.Mention + " " + guildInfo.RanksToEmotes[player.Rank];

        foreach(PlayerPosition position in player.Positions)
        {
          message += MatchService.PositionsToEmotes[position];
        }

        message += "\n";
      }

      message += $"\nBalance heuristic info (for debugging):\n";
      
      if(bestShuffle.RadiantPositionsAreFilled)
      {
        message += "All Radiant positions are filled. (+30 pts)\n";
      }

      if(bestShuffle.DirePositionsAreFilled)
      {
        message += "All Dire positions are filled. (+30 pts)\n";
      }

      message += $"Radiant's rank score: {bestShuffle.RadiantRankBalanceScore}\n";
      message += $"Dire's rank score: {bestShuffle.DireRankBalanceScore}\n";
      message += $"Final rank score: {bestShuffle.FinalRankBalanceScore}\n";

      message += $"Total: {bestShuffle.BalanceScore}";

      SourceQueue.AnnouncementChannel.SendMessageAsync(message);
    }
  }

  public class InhouseQueue : IQueue
  {
    public const int PlayersToKickOff = 10;
    public List<IGuildUser> QueuingUsers = new List<IGuildUser>();

    public InhouseQueue(String InName, ITextChannel InAnnouncementChannel) : base(InName, InAnnouncementChannel)
    {

    }

    public override bool IsUserInQueue(IGuildUser InUser)
    {
      return QueuingUsers.Contains(InUser);
    }

    public override QueueResult Queue(IGuildUser InUser, IGuildUser InTriggeringUser, IMessage InTriggeringMessage)
    {
      QueuingUsers.Add(InUser);
      CheckForGame();

      return QueueResult.Success;
    }

    public override UnqueueResult Unqueue(IGuildUser InUser, IGuildUser InTriggeringUser, IMessage InTriggeringMessage)
    {
      if(!QueuingUsers.Contains(InUser))
      {
        return UnqueueResult.NotQueuing;
      }
      QueuingUsers.Remove(InUser);
      return UnqueueResult.Success;
    }

    public override void Requeue(List<IGuildUser> InUsers)
    {
      QueuingUsers.InsertRange(0, InUsers);
      CheckForGame();
    }

    private void CheckForGame()
    {
      if(QueuingUsers.Count >= PlayersToKickOff)
      {
        InhouseMatch match = new InhouseMatch(Guild, this, QueuingUsers.GetRange(0, PlayersToKickOff));
        QueuingUsers.RemoveRange(0, PlayersToKickOff);
        MatchService.RunMatch(match);
      }
    }

    public override string GetMatchInfo()
    {
      string result = $"({QueuingUsers.Count}/{PlayersToKickOff}) ";
      foreach(IGuildUser user in QueuingUsers)
      {
        result += (user.Nickname == null ? user.Username : user.Nickname);
        if(user != QueuingUsers.Last())
        {
          result += ", ";
        }
      }
      return result;
    }
  }
}