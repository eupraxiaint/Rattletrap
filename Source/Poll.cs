using System;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using IronPython.Runtime;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Rest;
using YamlDotNet.Serialization;
using System.ComponentModel;
using System.Net.Http;
using System.Threading;

namespace Rattletrap
{
  public struct PollCreateInfo
  {
    public string Message;
    public string Title;
    public PlayerCollection Players;
    public TimeSpan Timeout;
    public List<IEmote> Responses;
    public ITextChannel Channel;
  };

  public struct PollResponse
  {
    public PlayerCollection Players;
    public IEmote Emote;
  }

  public class Poll
  {
    public PlayerCollection Players;
    public List<PollResponse> Responses = new List<PollResponse>();
    public Dictionary<string, int> EmoteNameToResponseIdx = new Dictionary<string, int>();
    public HashSet<Player> RespondedPlayers = new HashSet<Player>();
    public IUserMessage PollMessage = null;
    public string Message;
    public string Title;
    public DateTime Timestamp;
    public TimeSpan Timeout;
    public bool Complete;

    private Poll()
    {

    }

    public static async Task<Poll> Create(PollCreateInfo InCreateInfo)
    {
      Poll result = new Poll();

      result.Players = new PlayerCollection(InCreateInfo.Players);
      result.Message = InCreateInfo.Message;
      result.Title = InCreateInfo.Title;
      result.Timestamp = DateTime.Now;
      result.Timeout = InCreateInfo.Timeout;

      for(int responseIdx = 0; responseIdx < InCreateInfo.Responses.Count; ++responseIdx)
      {
        IEmote emote = InCreateInfo.Responses[responseIdx];
        result.EmoteNameToResponseIdx[emote.Name] = responseIdx;

        PollResponse response = new PollResponse();
        response.Players = new PlayerCollection();
        response.Emote = emote;

        result.Responses.Add(response);
      }

      EmbedBuilder builder = result.CreateEmbed();

      Task<IUserMessage> messageTask = InCreateInfo.Channel.SendMessageAsync(embed: builder.Build());

      await messageTask;

      result.PollMessage = messageTask.Result;

      for(int responseIdx = 0; responseIdx < InCreateInfo.Responses.Count; ++responseIdx)
      {
        IEmote emote = InCreateInfo.Responses[responseIdx];
        messageTask.Result.AddReactionAsync(emote);
      }

      MatchService.DiscordSocket.ReactionAdded += result.OnReactionAdded;
      MatchService.DiscordSocket.ReactionRemoved += result.OnReactionRemoved;

      Task.Run(() => result.RunTimeout());

      return result; 
    }

    private async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cachedMessage, 
      ISocketMessageChannel originChannel, SocketReaction reaction)
    {
      if(reaction.User.Value.Id != MatchService.DiscordSocket.CurrentUser.Id && reaction.User.IsSpecified
        && cachedMessage.HasValue && cachedMessage.Value.Id == PollMessage.Id && !Complete)
      {
        IEmote emote = reaction.Emote;
        if(EmoteNameToResponseIdx.ContainsKey(emote.Name))
        {
          int responseIdx = EmoteNameToResponseIdx[emote.Name];
          Player player = Player.GetOrCreate(reaction.User.Value as IGuildUser);
          Responses[responseIdx].Players.Players.Add(player);
          RespondedPlayers.Add(player);
          UpdatePollMessage();

          if(RespondedPlayers.Count == Players.Players.Count)
          {
            Complete = true;
            OnAllPlayersResponded.Invoke();
          }
        }
      }
    }

    private async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cachedMessage, 
      ISocketMessageChannel originChannel, SocketReaction reaction)
    {
      if(reaction.User.Value.Id != MatchService.DiscordSocket.CurrentUser.Id && reaction.User.IsSpecified
        && cachedMessage.HasValue && cachedMessage.Value.Id == PollMessage.Id && !Complete)
      {
        IEmote emote = reaction.Emote;
        if(EmoteNameToResponseIdx.ContainsKey(emote.Name))
        {
          int responseIdx = EmoteNameToResponseIdx[emote.Name];
          Player player = Player.GetOrCreate(reaction.User.Value as IGuildUser);
          Responses[responseIdx].Players.Players.Remove(player);
          RespondedPlayers.Remove(player);
          UpdatePollMessage();
        }
      }
    }

    private EmbedBuilder CreateEmbed()
    {
      EmbedBuilder builder = new EmbedBuilder();

      string description = Message + "\n**Responses:**\n";

      foreach(PollResponse response in Responses)
      {
        description += response.Emote + ": ";
        foreach(Player player in response.Players.Players)
        {
          description += player.GuildUser.Mention + " ";
        }
        description += "\n";
      }

      builder.Description = description;
      builder.WithTimestamp(Timestamp);
      builder.WithColor(Color.Purple);
      builder.WithTitle(Title);

      return builder;
    }

    private void UpdatePollMessage()
    {
      EmbedBuilder embed = CreateEmbed();

      PollMessage.ModifyAsync(x => x.Embed = embed.Build());
    }

    private async Task RunTimeout()
    {
      await Task.Delay(Timeout);
      if(!Complete)
      {
        OnTimeout.Invoke();
        Complete = true;
      }
    }

    public delegate void OnAllPlayersRespondedDelegate();

    public OnAllPlayersRespondedDelegate OnAllPlayersResponded;

    public delegate void OnTimeoutDelegate();

    public OnTimeoutDelegate OnTimeout;
  };
}