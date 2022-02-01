using System;
using Discord;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Threading;
using IronPython.Runtime.Exceptions;

namespace Rattletrap
{
  public struct PermaPollInitInfo
  {
    public EmbedBuilder Embed;
    public List<IEmote> Reactions;
    public bool Exclusive;
    public TimeSpan Timeout;
    public List<IGuildUser> Users;
    public bool ShowResponses;
    public bool MentionUsers;
  }

  public struct PermaPollReactionEntry
  {
    public IEmote Emote;
    public List<IGuildUser> Users;
  }

  public class PermaPoll : IWidget
  {
    public List<PermaPollReactionEntry> ReactionEntries = new List<PermaPollReactionEntry>();

    public bool Exclusive;
    private TimeSpan Timeout;
    public EmbedBuilder Embed;
    public bool ShowResponses;
    private bool IsDirty;
    private HashSet<IGuildUser> RespondedUsers = new HashSet<IGuildUser>();
    public List<IGuildUser> Users;
    private bool Closed = false;
    private bool MentionUsers = false;

    public async void Close()
    {
      Closed = true;
      OnReactionAdded -= ReactionAdded;
      OnReactionRemoved -= ReactionRemoved;
      await RemoveAllReactions();
    }

    private async void RunUpdate()
    {
      DateTime PollStart = DateTime.Now;
      while(DateTime.Now < PollStart + Timeout && !Closed)
      {
        if(IsDirty)
        {
          UpdateMessage();
          IsDirty = false;
        }

        await Task.Delay(TimeSpan.FromSeconds(1));
      }

      if(Timeout != TimeSpan.Zero && !Closed)
      {
        OnTimeout?.Invoke();
        Close();  
      }
    }

    public async void UpdateMessage()
    {
      EmbedBuilder embed = new EmbedBuilder();
      if(Embed != null)
      {
        if(Embed.Color.HasValue)
        {
          embed.WithColor(Embed.Color.Value);
        }
        embed.WithTitle(Embed.Title);
        embed.WithDescription(Embed.Description);
        embed.WithFields(Embed.Fields);
      }

      if(ShowResponses && ReactionEntries != null && ReactionEntries.Count != 0)
      {
        string responsesText = "";
        foreach(PermaPollReactionEntry entry in ReactionEntries)
        {
          responsesText += entry.Emote + " | ";
          foreach(IGuildUser user in entry.Users)
          {
            if(user.Id != MatchService.DiscordSocket.CurrentUser.Id)
            {
              responsesText += user.Mention + " ";
            }
          }
          responsesText += "\n";
        }
        embed.AddField("Responses", responsesText);
      }

      string mentionText = "";

      if(MentionUsers && Users != null)
      {
        foreach(IGuildUser user in Users)
        {
          mentionText += user.Mention + " ";
        }
      }

      await CreateOrEditMessage(embed, mentionText);
    }

    public async Task Initialize(PermaPollInitInfo InInitInfo)
    {
      Exclusive = InInitInfo.Exclusive;
      Timeout = InInitInfo.Timeout;
      ShowResponses = InInitInfo.ShowResponses;
      Embed = InInitInfo.Embed;
      Users = InInitInfo.Users;
      MentionUsers = InInitInfo.MentionUsers;

      UpdateMessage();

      await SetReactions(InInitInfo.Reactions);

      foreach(IEmote emote in Reactions)
      {
        PermaPollReactionEntry entry = new PermaPollReactionEntry();
        entry.Users = new List<IGuildUser>();
        entry.Emote = emote;

        ReactionEntries.Add(entry);
      }

      foreach(PermaPollReactionEntry entry in ReactionEntries)
      {
        List<IUser> users = Message.GetReactionUsersAsync(entry.Emote, 100000).Flatten().ToListAsync().Result;

        foreach(IUser user in users)
        {
          IGuildUser guildUser = GuildInstance.Guild.GetUserAsync(user.Id).Result;
          if(guildUser != null && guildUser.Id != MatchService.DiscordSocket.CurrentUser.Id)
          {
            entry.Users.Add(guildUser);
          }
        }
      }

      Task.Run((Action)RunUpdate);

      OnReactionAdded += ReactionAdded;
      OnReactionRemoved += ReactionRemoved;
    }

    private void ReactionAdded(IEmote InEmote, IGuildUser InUser)
    {
      if((Users != null && !Users.Contains(InUser)) || Closed)
      {
        RemoveReaction(InEmote, InUser).Wait();
        return;
      }

      int idx = Reactions.FindIndex(x => x.Name == InEmote.Name);
      if(idx < 0)
      {
        return;
      }

      if(!ReactionEntries[idx].Users.Contains(InUser))
      {
        ReactionEntries[idx].Users.Add(InUser);
      }

      if(Exclusive)
      {
        foreach(PermaPollReactionEntry entry in ReactionEntries)
        {
          if(entry.Emote.Name != InEmote.Name && entry.Users.Contains(InUser))
          {
            entry.Users.Remove(InUser);
            RemoveReaction(entry.Emote, InUser).Wait();
          }
        }
      }

      RespondedUsers.Add(InUser);
      if(Users != null && RespondedUsers.Count == Users.Count)
      {
        OnAllUsersResponded?.Invoke();
      }

      if(ShowResponses)
      {
        IsDirty = true;
      }
    }

    private void ReactionRemoved(IEmote InEmote, IGuildUser InUser)
    {
      if(Closed)
      {
        return;
      }

      int idx = Reactions.IndexOf(InEmote);
      if(ReactionEntries[idx].Users.Contains(InUser))
      {
        ReactionEntries[idx].Users.RemoveAll(x => x == InUser);
      }

      bool foundUser = false;

      for(int entryIdx = 0; entryIdx < ReactionEntries.Count; ++entryIdx)
      {
        if(entryIdx != idx && ReactionEntries[entryIdx].Users.Contains(InUser))
        {
          foundUser = true;
          break;
        }
      }

      if(!foundUser)
      {
        RespondedUsers.Remove(InUser);
      }

      if(ShowResponses)
      {
        IsDirty = true;
      }
    }

    public List<IEmote> GetReactionsForUser(IGuildUser InUser)
    {
      List<IEmote> emotes = new List<IEmote>();

      foreach(PermaPollReactionEntry entry in ReactionEntries)
      {
        if(entry.Users.Contains(InUser))
        {
          emotes.Add(entry.Emote);
        }
      }

      return emotes;
    }

    public List<IGuildUser> GetUsersForEmote(IEmote InEmote)
    {
      int idx = Reactions.IndexOf(InEmote);
      
      if(idx >= 0)
      {
        return ReactionEntries[idx].Users;
      }
      else
      {
        return new List<IGuildUser>();
      }
    }

    public async void RemoveResponse(IEmote InEmote, IGuildUser InUser)
    {
      int idx = Reactions.IndexOf(InEmote);

      if(idx >= 0)
      {
        ReactionEntries[idx].Users.Remove(InUser);
        await RemoveReaction(InEmote, InUser);
      }
    }

    public delegate void OnAllUsersRespondedDelegate();

    public OnAllUsersRespondedDelegate OnAllUsersResponded;

    public delegate void OnTimeoutDelegate();

    public OnTimeoutDelegate OnTimeout;
  }
}