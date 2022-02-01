using System;
using Discord;
using System.Collections.Generic;

namespace Rattletrap
{
  public struct ButtonWidgetInitInfo
  {
    public EmbedBuilder Embed;
    public List<IEmote> Reactions;
  }

  public class ButtonWidget : IWidget
  {
    public async void Initialize(ButtonWidgetInitInfo InInitInfo)
    {
      await CreateOrEditMessage(InInitInfo.Embed);
      await SetReactions(InInitInfo.Reactions);

      OnReactionAdded += ReactionAdded;
    }

    async void ReactionAdded(IEmote InEmote, IGuildUser InUser)
    {
      OnButtonClicked?.Invoke(InEmote, InUser);
      await RemoveReaction(InEmote, InUser);
    }

    public delegate void OnButtonClickedDelegate(IEmote InEmote, IGuildUser InUser);

    public OnButtonClickedDelegate OnButtonClicked;
  }
}