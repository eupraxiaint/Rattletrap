/*

using Discord;
using System;
using System.Collections.Generic;

namespace Rattletrap
{
  public enum QueueResult
  {
    Success,
    AlreadyQueuing,
    AlreadyInMatch
  }

  public enum UnqueueResult
  {
    Success,
    NotQueuing
  }

  public abstract class IQueue
  {
    public String Name;
    public ITextChannel AnnouncementChannel;
    public IGuild Guild;

    public IQueue(String InName, ITextChannel InAnnouncementChannel)
    {
      Name = InName;
      AnnouncementChannel = InAnnouncementChannel;
      Guild = InAnnouncementChannel.Guild;
    }

    public abstract bool IsUserInQueue(IGuildUser InUser);
    public abstract QueueResult Queue(IGuildUser InUser, IGuildUser InTriggeringUser, IMessage InTriggeringMessage);
    public abstract UnqueueResult Unqueue(IGuildUser InUser, IGuildUser InTriggeringUser, IMessage InTriggeringMessage);
    public abstract void Requeue(List<IGuildUser> InUsers);
    public abstract string GetMatchInfo();
  }
}

*/