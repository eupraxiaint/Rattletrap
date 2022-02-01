using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Scripting.Metadata;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rattletrap
{
  public class IWidget
  {
    public int Id;
    public ITextChannel Channel;
    public IUserMessage Message;
    public List<IEmote> Reactions;
    public GuildInstance GuildInstance;

    public IWidget()
    {
      MatchService.DiscordSocket.ReactionAdded += HandleReactionAdded;
      MatchService.DiscordSocket.ReactionRemoved += HandleReactionRemoved;
    }

    public async Task CreateOrEditMessage(EmbedBuilder InEmbed, string InText = "")
    {
      InEmbed.WithFooter($"WID: {Id}");

      if(Message == null)
      {
        Message = Channel.SendMessageAsync(InText, embed: InEmbed.Build()).Result;
      }
      else
      {
        await Message.ModifyAsync(x => { x.Embed = InEmbed.Build(); x.Content = InText; });
      }
    }

    public async Task RemoveReaction(IEmote InEmote, IGuildUser InUser)
    {
      await Message.RemoveReactionAsync(InEmote, InUser);
    }

    public async Task RemoveAllReactions()
    {
      await Message.RemoveAllReactionsAsync();
    }

    protected async Task SetReactions(List<IEmote> InReactions)
    {
      if(Message != null)
      {
        bool hasAllReactions = true;
        foreach(IEmote emote in InReactions)
        {
          if(!Message.Reactions.ContainsKey(emote))
          {
            hasAllReactions = false;
            break;
          }
        }

        if(!hasAllReactions)
        {
          await Message.RemoveAllReactionsAsync();
          await Message.AddReactionsAsync(InReactions.ToArray());
        }
      }

      Reactions = InReactions;
    }

    private async Task HandleReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, 
      ISocketMessageChannel originChannel, SocketReaction reaction)
    {
      if(cachedMessage.Id != Message.Id)
      {
        return;
      }

      if(reaction.UserId == MatchService.DiscordSocket.CurrentUser.Id)
      {
        return;
      }

      if(Reactions != null && Reactions.Contains(reaction.Emote))
      {
        OnReactionAdded?.Invoke(reaction.Emote, reaction.User.Value as IGuildUser);
        if(OnReactionModified != null)
        {
          await OnReactionModified.Invoke(reaction.Emote, reaction.User.Value as IGuildUser, true);
        }
      }
      else
      {
        await Message.RemoveAllReactionsForEmoteAsync(reaction.Emote);
      }
    }

    private async Task HandleReactionRemoved(Cacheable<IUserMessage, ulong> cachedMessage, 
      ISocketMessageChannel originChannel, SocketReaction reaction)
    {
      if(cachedMessage.Id != Message.Id)
      {
        return;
      }

      if(reaction.UserId == MatchService.DiscordSocket.CurrentUser.Id)
      {
        return;
      }

      if(reaction.UserId != MatchService.DiscordSocket.CurrentUser.Id)
      {
        if(Reactions != null && Reactions.Contains(reaction.Emote))
        {
          OnReactionRemoved?.Invoke(reaction.Emote, reaction.User.Value as IGuildUser);
          if(OnReactionModified != null)
          {
            await OnReactionModified.Invoke(reaction.Emote, reaction.User.Value as IGuildUser, false);
          }
        }
      }
    }

    public delegate void OnReactionAddedDelegate(IEmote InEmote, IGuildUser InUser);

    public OnReactionAddedDelegate OnReactionAdded;

    public delegate void OnReactionRemovedDelegate(IEmote InEmote, IGuildUser InUser);

    public OnReactionRemovedDelegate OnReactionRemoved;

    public delegate Task OnReactionModifiedDelegate(IEmote InEmote, IGuildUser InUser, bool InAdded);

    public OnReactionModifiedDelegate OnReactionModified;
  }
}