using System;
using System.Collections.Generic;
using Discord;
using System.Threading.Tasks;
using System.Linq;

namespace Rattletrap
{
  public class OneVOneMatch : IMatch
  {
    public OneVOneMatch(IGuild InGuild, IQueue InSourceQueue, List<IGuildUser> InPlayers)
      : base(InGuild, InSourceQueue, InPlayers)
    {

    }

    public override async void Announce()
    {
      string messageText = $"**STAND AND DELIVER!** Found a 1v1 (id: {Id}) in queue `{SourceQueue.Name}`.\n"
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
      String message = $"Both players ready! (match id: {Id}) Please create a lobby and type "
        + $"`;lobby {Id} \"<lobby-name>\" \"<lobby-password>\"`\n";

      foreach(IGuildUser player in Players)
      {
        message += player.Mention + " ";
      }

      await SourceQueue.AnnouncementChannel.SendMessageAsync(message);
    }

    public override void OnLobby(String InName, String InPassword)
    {
      Random random = new Random();
      bool playerOrder = random.Next() % 2 == 0;
      GuildInfo guildInfo = MatchService.GuildInfos[Guild];
      PlayerInfo player0Info = MatchService.GetPlayerInfo(Guild, Players[0]);
      PlayerInfo player1Info = MatchService.GetPlayerInfo(Guild, Players[1]);
      string player0String = guildInfo.RanksToEmotes[player0Info.Rank] + " " + player0Info.User.Mention;
      string player1String = guildInfo.RanksToEmotes[player1Info.Rank] + " " + player1Info.User.Mention;
      string radiantString = playerOrder ? player0String : player1String;
      string direString = playerOrder ? player1String : player0String;
      
      String message = $"Lobby is up for the 1v1 between {radiantString} (Radiant) and {direString} (Dire). Name: **{InName}**, Password: **{InPassword}**.";

      SourceQueue.AnnouncementChannel.SendMessageAsync(message);
    }
  }

  public class OneVOneQueue : IQueue
  {
    public IGuildUser QueuedUser = null;

    public OneVOneQueue(String InName, ITextChannel InAnnouncementChannel) : base(InName, InAnnouncementChannel)
    {

    }

    public override bool IsUserInQueue(IGuildUser InUser)
    {
      return QueuedUser == InUser;
    }

    public override QueueResult Queue(IGuildUser InUser, IGuildUser InTriggeringUser, IMessage InTriggeringMessage)
    {
      if(QueuedUser == InUser)
      {
        return QueueResult.AlreadyQueuing;
      }
      else if(QueuedUser == null)
      {
        QueuedUser = InUser;
        return QueueResult.Success;
      }
      else
      {
        List<IGuildUser> players = new List<IGuildUser>();
        players.Add(InUser);
        players.Add(QueuedUser);
        OneVOneMatch match = new OneVOneMatch(InUser.Guild, this, players);
        MatchService.RunMatch(match);
        QueuedUser = null;
        return QueueResult.Success;
      }
    }

    public override UnqueueResult Unqueue(IGuildUser InUser, IGuildUser InTriggeringUser, IMessage InTriggeringMessage)
    {
      QueuedUser = null;
      return UnqueueResult.Success;
    }

    public override void Requeue(List<IGuildUser> InUsers)
    {
      // really should only be one, but we'll loop anyway
      foreach(IGuildUser user in InUsers)
      {
        Queue(user, null, null);
      }
    }

    public override string GetMatchInfo()
    {
      if(QueuedUser == null)
      {
        return "(0/2)";
      }
      else
      {
        return "(1/2) " + (QueuedUser.Nickname == null ? QueuedUser.Username : QueuedUser.Nickname);
      }
    }
  }
}