using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Discord;
using Newtonsoft.Json;
using YamlDotNet.Core.Tokens;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using IronPython.Runtime;
using System.Threading.Tasks;
using Discord.Rest;
using System.CodeDom;
using IronPython.Compiler.Ast;

namespace Rattletrap
{
#region MatchChannelSet definition
  public class MatchChannelSet
  {
    public int Id;
    public ICategoryChannel CategoryChannel;
    public ITextChannel AnnouncementsChannel;
    public ITextChannel AllChatChannel;
    public IVoiceChannel RadiantVoiceChannel;
    public IVoiceChannel DireVoiceChannel;
    public IVoiceChannel LobbyVoiceChannel;
  }
#endregion
#region GuildInstance definition
  // manages all of the data Rattletrap associates with a particular guild
  public class GuildInstance
  {
#region Member definitions

    // the guild this GuildInstance serves
    public IGuild Guild { get; private set; }

    // the name of the guild this GuildInstance serves
    public string Name { get; private set; }

    // the guild ID this GuildInstance serves
    public ulong Id { get; private set; }

    // he filepath of the file we use to store data about this guild
    public string Filepath { get; private set; }

    // whether or not Rattletrap is enabled for this guild
    private bool _enabled = true;
    public bool Enabled { get { return _enabled; } set { _enabled = value; SaveToFile(); } }

    public PlayerCollection QueuingPlayers { get; private set; } = new PlayerCollection();

    // the list of currently active matches
    public List<IMatch2> Matches = new List<IMatch2>();

    // the channel for admin bot commands
    public ITextChannel AdminBotChannel;

    // the channel for public bot commands
    public ITextChannel MainBotChannel;

    // the channel for match announcements
    public ITextChannel AnnouncementChannel;

    public ITextChannel ReadyUpChannel;

    public ITextChannel PlayChannel;
    public ITextChannel SettingsChannel;

    // the list of guilds mapped to their associated instances
    private static Dictionary<IGuild, GuildInstance> Instances = new Dictionary<IGuild, GuildInstance>();

    public List<string> AvailableModes = new List<string> { "inhouse", "practice", "casual" };

    public List<string> AvailableRegions = new List<string> { "na", "sa", "eu", "cis", "sea" };

    private List<IMatchGeneratorBase> MatchGenerators = new List<IMatchGeneratorBase>();

    public PermaPoll ModesPoll;
    public PermaPoll RegionsPoll;
    public PermaPoll RolesPoll;
    public PermaPoll RanksPoll;
    public PermaPoll FlairPoll;
    public ButtonWidget QueueButton;

    private SavedData CachedSaveData;

    public List<IWidget> Widgets = new List<IWidget>();

  #endregion
#region SavedData definition
    // data stored to file
    private struct SavedData
    {
      // the name of the guild
      public string Name;

      // the guild ID from Discord
      public ulong Id;

      // whether or not Rattletrap is enabled for this guild
      public bool Enabled;

      public ulong ModesPollMessageId;
      public ulong RegionsPollMessageId;
      public ulong RolesPollMessageId;
      public ulong RanksPollMessageId;
      public ulong QueueButtonMessageId;
      public ulong FlairPollMessageId;
    }
#endregion
#region FindRole, FindEmote, FindTextChannel
    // finds a role by name in a guild
    private static IRole FindRole(IGuild InGuild, string InName)
    {
      foreach(IRole role in InGuild.Roles)
      {
        if(role.Name == InName)
        {
          return role;
        }
      }

      return null;
    }

    // finds an emote by name in a guild
    private static IEmote FindEmote(IGuild InGuild, string InName)
    {
      foreach(IEmote emote in InGuild.Emotes)
      {
        if(emote.Name == InName)
        {
          return emote;
        }
      }

      return null;
    }

    // finds a text channel by name in a guild
    public ITextChannel FindTextChannel(string InName)
    {
      IReadOnlyCollection<ITextChannel> textChannels = Guild.GetTextChannelsAsync().Result;
      foreach(ITextChannel textChannel in textChannels)
      {
        if(textChannel.Name == InName)
        {
          return textChannel;
        }
      }

      return null;
    }
#endregion
#region GetRoleNameByRegionName
    public string GetRoleNameByRegionName(string InRegionName)
    {
      switch(InRegionName)
      {
      case "cis":
        return "CIS";
      case "na":
        return "NA";
      case "sea":
        return "SEA";
      case "eu":
        return "EU";
      default:
        return "";
      }
    }
#endregion
#region GetRegionRole
    public IRole GetRegionRole(string InRegion)
    {
      return FindRole(Guild, GetRoleNameByRegionName(InRegion));
    }
#endregion
#region Create, Get
    // creates an instance for a guild
    public static GuildInstance Create(IGuild InGuild)
    {
      // if we don't already have an instance for the guild, create it
      if(!Instances.ContainsKey(InGuild))
      {
        GuildInstance result = new GuildInstance();
        Instances.Add(InGuild, result);

        // build the filepath for the guild file
        result.Filepath = Path.Combine(AppContext.BaseDirectory, $"../Saved/Guilds/{InGuild.Id}.json");

        result.Guild = InGuild;

        // initialize non-saved data - channels, queues, roles, emotes
        result.AnnouncementChannel = result.FindTextChannel("\U0001f514inhouse-announcement");
        result.PlayChannel = result.FindTextChannel($"{StaticEmotes.CrystalBall.ToString()}play");
        result.SettingsChannel = result.FindTextChannel($"{StaticEmotes.Toolbox.ToString()}settings");

        // create the file if it doesn't exist, and save it
        if(!result.LoadFromFile())
        {
          result.Name = InGuild.Name;
          result.Id = InGuild.Id;
          result.Enabled = true;
          result.SaveToFile();
        }

        result.MainBotChannel = result.FindTextChannel($"{StaticEmotes.Robot}bot-commands");
        result.AdminBotChannel = result.FindTextChannel("admin-bot-commands");
        result.ReadyUpChannel = result.FindTextChannel($"{StaticEmotes.WhiteHeavyCheckMark}ready-up");

        result.MatchGenerators = new List<IMatchGeneratorBase>();
        result.MatchGenerators.Add(new MatchGenerator<InhouseMatch2>(result));
        result.MatchGenerators.Add(new MatchGenerator<DuelMatch>(result));

        result.ClearMatchChannelSets();
        result.InitPermaPolls();

        Task.Run((Action)result.RunUpdate);

        return result;
      }
      else
      {
        // todo: add a warning log here (we shouldn't attempt to create an instance if it already exists)
        return Instances[InGuild];
      }
    }

    // gets the instance associated with a particular guild
    public static GuildInstance Get(IGuild InGuild)
    {
      if(Instances.ContainsKey(InGuild))
      {
        return Instances[InGuild];
      }

      return null;
    }
#endregion
#region IsAdmin
    // determines whether or not the given user is a guild admin
    public bool IsAdmin(IGuildUser InUser)
    {
      return InUser.RoleIds.Contains(FindRole(InUser.Guild, "Admin").Id);
    }
#endregion
#region SaveToFile, LoadFromFile
    // saves guild data to file
    private void SaveToFile()
    {
      string guildsDir = Path.Combine(AppContext.BaseDirectory, "../Saved/Guilds");

      if(!Directory.Exists(guildsDir))
      {
        Directory.CreateDirectory(guildsDir);
      }

      CachedSaveData.Name = Name;
      CachedSaveData.Id = Id;
      CachedSaveData.Enabled = _enabled;
      CachedSaveData.ModesPollMessageId = ModesPoll == null ? 0 : ModesPoll.Message.Id;
      CachedSaveData.QueueButtonMessageId = QueueButton == null ? 0 : QueueButton.Message.Id;
      CachedSaveData.RanksPollMessageId = RanksPoll == null ? 0 : RanksPoll.Message.Id;
      CachedSaveData.RegionsPollMessageId = RegionsPoll == null ? 0 : RegionsPoll.Message.Id;
      CachedSaveData.RolesPollMessageId = RolesPoll == null ? 0 : RolesPoll.Message.Id;
      CachedSaveData.FlairPollMessageId = FlairPoll == null ? 0 : FlairPoll.Message.Id;
      TextWriter textWriter = new StreamWriter(Filepath);
      string textToWrite = JsonConvert.SerializeObject(CachedSaveData);
      textWriter.Write(textToWrite);
      textWriter.Close();
    }

    // loads guild data from file
    private bool LoadFromFile()
    {
      if(!File.Exists(Filepath))
      {
        return false;
      }

      TextReader textReader = new StreamReader(Filepath);
      string fileContents = textReader.ReadToEnd();
      CachedSaveData = JsonConvert.DeserializeObject<SavedData>(fileContents);
      Id = CachedSaveData.Id;
      Name = CachedSaveData.Name;
      _enabled = CachedSaveData.Enabled;
      textReader.Close();
      return true;
    }
#endregion
#region QueuePlayer, UnqueuePlayer
    public enum QueuePlayerResult
    {
      Success,
      AlreadyQueuing
    }

    public QueuePlayerResult QueuePlayer(Player InPlayer, bool InResetQueueTime = true, bool InCheckForMatches = true)
    {
      if(QueuingPlayers.Players.Contains(InPlayer))
      {
        return QueuePlayerResult.AlreadyQueuing;
      }
      else
      {
        QueuingPlayers.Players.Add(InPlayer);
        InPlayer.IsInQueue = true;
        QueueButtonIsDirty = true;
        if(InCheckForMatches)
        {
          CheckForMatches();
        }
        return QueuePlayerResult.Success;
      }
    }

    public enum UnqueuePlayerResult
    {
      Success,
      NotQueuing
    }

    public UnqueuePlayerResult UnqueuePlayer(Player InPlayer)
    {
      if(QueuingPlayers.Players.Contains(InPlayer))
      {
        QueuingPlayers.Players.Remove(InPlayer);
        InPlayer.IsInQueue = false;
        CheckForMatches();
        QueueButtonIsDirty = true;
        return UnqueuePlayerResult.Success;
      }
      else
      {
        return UnqueuePlayerResult.NotQueuing;
      }
    }
#endregion
#region RunUpdate
    private bool QueueButtonIsDirty = false;

    private async void RunUpdate()
    {
      while(true)
      {
        if(QueueButtonIsDirty)
        {
          UpdateQueueButton();
          QueueButtonIsDirty = false;
        }
        CheckForMatches();
        await Task.Delay(TimeSpan.FromSeconds(1));
      }
    }
#endregion
#region CheckForMatches
    public void CheckForMatches()
    {
      if(LobbyRunner.GetNumAvailableLobbyRunners() == 0)
      {
        return;
      }

      List<MatchmakingPoke> pokes = new List<MatchmakingPoke>();
      foreach(IMatchGeneratorBase matchGenerator in MatchGenerators)
      {
        List<MatchCandidate> candidates = matchGenerator.CheckForMatches(QueuingPlayers, pokes);

        if(candidates.Count > 0)
        {
          QueuingPlayers = QueuingPlayers - candidates[0].Players;
          IMatch2 match = matchGenerator.GenerateMatch(candidates[0]);
          Matches.Add(match);
        }
      }

      foreach(MatchmakingPoke poke in pokes)
      {
        EmbedBuilder embed = new EmbedBuilder();
        embed.WithColor(Color.Orange);
        embed.WithDescription(poke.Message);
        poke.Player.GuildUser.SendMessageAsync(embed: embed.Build());
      }
    }
#endregion
#region GetAllActivePlayers
    public PlayerCollection GetAllActivePlayers()
    {
      PlayerCollection players = new PlayerCollection();
      foreach(IGuildUser user in Guild.GetUsersAsync().Result)
      {
        Player player = Player.GetOrCreate(user);
        if(player.IsActive)
        {
          players.Players.Add(player);
        }
      }

      return players;
    }
#endregion
#region GetWidgetMessage
    private async Task<IUserMessage> GetWidgetMessage(ITextChannel InChannel, ulong InMessageId)
    {
      if(InMessageId == 0)
      {
        return null;
      }

      return InChannel.GetMessageAsync(InMessageId).Result as IUserMessage;
    }
#endregion
#region OnQueueButtonClicked
    private void OnQueueButtonClicked(IEmote InEmote, IGuildUser InUser)
    {
      Player player = Player.GetOrCreate(InUser);
      if(player != null)
      {
        if(InEmote.Name == StaticEmotes.CrystalBall.Name)
        {
          QueuePlayer(player);
        }
        else if(InEmote.Name == StaticEmotes.StopSign.Name)
        {
          UnqueuePlayer(player);
        }
      }
    }
#endregion
#region UpdateQueueButton
    private async void UpdateQueueButton()
    {
      EmbedBuilder embed = new EmbedBuilder();
      embed.WithColor(Color.Purple);
      embed.WithTitle($"{StaticEmotes.CrystalBall} Queue");

      embed.WithDescription("Use the buttons below to enter or leave matchmaking.");

      string queuingPlayersText = "";

      if(QueuingPlayers.Players.Count == 0)
      {
        queuingPlayersText = "Nobody queuing right now. :(";
      }
      else
      {
        foreach(Player player in QueuingPlayers.Players)
        {
          queuingPlayersText += player.Flair + " " + player.GuildUser.Mention + " ";
          foreach(string mode in player.Modes)
          {
            queuingPlayersText += StaticEmotes.GetModeEmojiFromName(mode);
          }
          foreach(string region in player.Regions)
          {
            queuingPlayersText += StaticEmotes.GetRegionEmoteFromName(region);
          }
          queuingPlayersText += "\n";
        }
      }

      embed.AddField("Queuing Players", queuingPlayersText);

      await QueueButton.CreateOrEditMessage(embed);
    }
#endregion
  private async Task OnRegionsPollModified(IEmote InEmote, IGuildUser InUser, bool InAdded)
  {
    Player player = Player.GetOrCreate(InUser);
    player.UpdateRegionsFromPoll();
    QueueButtonIsDirty = true;
  }

  private async Task OnRanksPollModified(IEmote InEmote, IGuildUser InUser, bool InAdded)
  {
    Player player = Player.GetOrCreate(InUser);
    player.UpdateRankMedalFromPoll();
  }

  private async Task OnRolesPollModified(IEmote InEmote, IGuildUser InUser, bool InAdded)
  {
    Player player = Player.GetOrCreate(InUser);
    player.UpdatePlayerRolesFromPoll();
  }

  private async Task OnModesPollModified(IEmote InEmote, IGuildUser InUser, bool InAdded)
  {
    Player player = Player.GetOrCreate(InUser);
    player.UpdateModesFromPoll();
    QueueButtonIsDirty = true;
  }

  private async Task OnFlairPollModified(IEmote InEmote, IGuildUser InUser, bool InAdded)
  {
    Player player = Player.GetOrCreate(InUser);
    player.UpdateFlairFromPoll();
    QueueButtonIsDirty = true;
  }
#region InitPermaPolls
    public async void InitPermaPolls()
    {
      if(SettingsChannel != null)
      {
        PermaPollInitInfo modesPollInitInfo = new PermaPollInitInfo();
        modesPollInitInfo.Embed = new EmbedBuilder();
        modesPollInitInfo.Embed.WithColor(Color.DarkRed);
        modesPollInitInfo.Embed.WithTitle("Modes");
        modesPollInitInfo.Embed.WithDescription($"Use the reactions below to select the modes you are interested in playing.\n"
          + $"{StaticEmotes.House.ToString()}: `inhouse`. A semi-competitive mode available to all, "
            + "filtered by region.\n"
          + $"{StaticEmotes.Books.ToString()}: `practice`. A mode designed for casual play and learning the game."
            + " Divine and Immortal players must coach in this mode.\n"
          + $"{StaticEmotes.PersonJuggling.ToString()}: `casual`. A random selection of lesser-played modes. Wackiness"
            + " abounds.\n"
          + $"{StaticEmotes.CrossedSwords.ToString()}: `1v1`. First to two kills or tower.");
        modesPollInitInfo.Reactions = new List<IEmote> { StaticEmotes.House, StaticEmotes.Books, 
          StaticEmotes.PersonJuggling, StaticEmotes.CrossedSwords };

        ModesPoll = CreateWidget<PermaPoll>(SettingsChannel, 
          GetWidgetMessage(SettingsChannel, CachedSaveData.ModesPollMessageId).Result);
        await ModesPoll.Initialize(modesPollInitInfo);
        ModesPoll.OnReactionModified += OnModesPollModified;

        PermaPollInitInfo regionsPollInitInfo = new PermaPollInitInfo();
        regionsPollInitInfo.Embed = new EmbedBuilder();
        regionsPollInitInfo.Embed.Color = Color.DarkRed;
        regionsPollInitInfo.Embed.Title = "Regions";
        regionsPollInitInfo.Embed.Description = 
          "Use the reactions below to select the server regions you are willing to play on.";
        regionsPollInitInfo.Reactions = new List<IEmote> { StaticEmotes.EuRegion, StaticEmotes.NaRegion, 
          StaticEmotes.SaRegion, StaticEmotes.CisRegion, StaticEmotes.SeaRegion };
        RegionsPoll = CreateWidget<PermaPoll>(SettingsChannel, 
          GetWidgetMessage(SettingsChannel, CachedSaveData.RegionsPollMessageId).Result);
        await RegionsPoll.Initialize(regionsPollInitInfo);
        RegionsPoll.OnReactionModified += OnRegionsPollModified;

        PermaPollInitInfo rankPollInitInfo = new PermaPollInitInfo();
        rankPollInitInfo.Embed = new EmbedBuilder();
        rankPollInitInfo.Embed.Color = Color.DarkRed;
        rankPollInitInfo.Embed.Title = "Rank";
        rankPollInitInfo.Embed.Description = "Use the reactions below to select your rank medal.";
        rankPollInitInfo.Reactions = new List<IEmote> { StaticEmotes.UncalibratedRank, StaticEmotes.HeraldRank, 
          StaticEmotes.GuardianRank, StaticEmotes.CrusaderRank, StaticEmotes.ArchonRank, StaticEmotes.LegendRank, 
          StaticEmotes.AncientRank, StaticEmotes.Divine1To3Rank, StaticEmotes.Divine4To5Rank, 
          StaticEmotes.ImmortalUnrankedRank, StaticEmotes.ImmortalTop1000Rank };
        rankPollInitInfo.Exclusive = true;
        RanksPoll = CreateWidget<PermaPoll>(SettingsChannel, 
          GetWidgetMessage(SettingsChannel, CachedSaveData.RanksPollMessageId).Result);
        await RanksPoll.Initialize(rankPollInitInfo);
        RanksPoll.OnReactionModified += OnRanksPollModified;

        PermaPollInitInfo rolePollInitInfo = new PermaPollInitInfo();
        rolePollInitInfo.Embed = new EmbedBuilder();
        rolePollInitInfo.Embed.Color = Color.DarkRed;
        rolePollInitInfo.Embed.Title = "Roles";
        rolePollInitInfo.Embed.Description = "Use the reactions below to select your preferred roles.";
        rolePollInitInfo.Reactions = new List<IEmote> { StaticEmotes.SupportRole, StaticEmotes.SoftSupportRole, 
          StaticEmotes.OfflaneRole, StaticEmotes.MidlaneRole, StaticEmotes.SafelaneRole };  
        RolesPoll = CreateWidget<PermaPoll>(SettingsChannel, 
          GetWidgetMessage(SettingsChannel, CachedSaveData.RolesPollMessageId).Result);
        await RolesPoll.Initialize(rolePollInitInfo);
        RolesPoll.OnReactionModified += OnRolesPollModified;

        PermaPollInitInfo flairPollInitInfo = new PermaPollInitInfo();
        flairPollInitInfo.Embed = new EmbedBuilder();
        flairPollInitInfo.Embed.Color = Color.DarkRed;
        flairPollInitInfo.Embed.Title = "Flair";
        flairPollInitInfo.Embed.Description = "Use the reactions below to select a flair emoji to show next to your name.";
        flairPollInitInfo.Reactions = new List<IEmote> { StaticEmotes.Sunglasses, StaticEmotes.ZanyFace, 
          StaticEmotes.Clown, StaticEmotes.Robot, StaticEmotes.Cowboy, StaticEmotes.Smirk, StaticEmotes.Boar,
          StaticEmotes.TigerFace, StaticEmotes.DogFace, StaticEmotes.Detective, StaticEmotes.Facepalm,
          StaticEmotes.Snowman };
        flairPollInitInfo.Exclusive = true;
        FlairPoll = CreateWidget<PermaPoll>(SettingsChannel, 
          GetWidgetMessage(SettingsChannel, CachedSaveData.FlairPollMessageId).Result);
        await FlairPoll.Initialize(flairPollInitInfo);
        FlairPoll.OnReactionModified += OnFlairPollModified;
      }

      if(PlayChannel != null)
      {
        ButtonWidgetInitInfo queueButtonInitInfo = new ButtonWidgetInitInfo();
        queueButtonInitInfo.Embed = new EmbedBuilder();
        queueButtonInitInfo.Reactions = new List<IEmote> { StaticEmotes.CrystalBall, StaticEmotes.StopSign };
        QueueButton = CreateWidget<ButtonWidget>(PlayChannel, 
          GetWidgetMessage(PlayChannel, CachedSaveData.QueueButtonMessageId).Result);
        QueueButton.Initialize(queueButtonInitInfo);
        UpdateQueueButton();
        QueueButton.OnButtonClicked += OnQueueButtonClicked;
      }

      SaveToFile();
    }
#endregion
#region CreateWidget
    public WidgetType CreateWidget<WidgetType>(ITextChannel InChannel, IUserMessage InExistingMessage)
      where WidgetType : IWidget, new()
    {
      WidgetType widget = new WidgetType();
      widget.Id = Widgets.Count;
      widget.Channel = InChannel;
      widget.Message = InExistingMessage;
      widget.GuildInstance = this;
      Widgets.Add(widget);
      return widget;
    }
#endregion
#region CreateMatchChannelSet, DestroyMatchChannelSet
    public MatchChannelSet CreateMatchChannelSet(IMatch2 InMatch)
    {
      MatchChannelSet result = new MatchChannelSet();
      result.Id = InMatch.Id;
      result.CategoryChannel = Guild.CreateCategoryAsync($"{StaticEmotes.DiamondWithADot} Match {InMatch.Id}").Result;

      result.AnnouncementsChannel = Guild.CreateTextChannelAsync(
        $"{StaticEmotes.HighVoltage}match-{InMatch.Id}-announcements", 
        (x) => { x.CategoryId = result.CategoryChannel.Id; }).Result;

      result.AllChatChannel = Guild.CreateTextChannelAsync(
        $"{StaticEmotes.RevolvingHearts}match-{InMatch.Id}-all-chat",
        (x) => { x.CategoryId = result.CategoryChannel.Id; }).Result;

      result.RadiantVoiceChannel = Guild.CreateVoiceChannelAsync(
        $"{StaticEmotes.Tent}match-{InMatch.Id}-radiant-vc",
        (x) => { x.CategoryId = result.CategoryChannel.Id; }).Result;

      result.DireVoiceChannel = Guild.CreateVoiceChannelAsync(
        $"{StaticEmotes.CityscapeAtDusk}match-{InMatch.Id}-dire-vc",
        (x) => { x.CategoryId = result.CategoryChannel.Id; }).Result;

      result.LobbyVoiceChannel = Guild.CreateVoiceChannelAsync(
        $"{StaticEmotes.DiamondWithADot}match-{InMatch.Id}-lobby-vc",
        (x) => { x.CategoryId = result.CategoryChannel.Id; }).Result;

      return result;
    }

    public async void DestroyMatchChannelSet(MatchChannelSet InMatchChannelSet)
    {
      await InMatchChannelSet.AnnouncementsChannel.DeleteAsync();
      await InMatchChannelSet.AllChatChannel.DeleteAsync();
      await InMatchChannelSet.RadiantVoiceChannel.DeleteAsync();
      await InMatchChannelSet.DireVoiceChannel.DeleteAsync();
      await InMatchChannelSet.LobbyVoiceChannel.DeleteAsync();
      await InMatchChannelSet.CategoryChannel.DeleteAsync();
    }
#endregion
#region ClearMatchChannelSets
    private async void ClearMatchChannelSets()
    {
      List<ICategoryChannel> categories = Guild.GetCategoriesAsync().Result.ToList();
      HashSet<ulong> categoryIds = new HashSet<ulong>();
      List<ICategoryChannel> categoriesToDelete = new List<ICategoryChannel>();
      foreach(ICategoryChannel channel in categories)
      {
        if(channel.Name.StartsWith(StaticEmotes.DiamondWithADot.ToString()))
        {
          categoryIds.Add(channel.Id);
          categoriesToDelete.Add(channel);
        }
      }

      List<ITextChannel> textChannels = Guild.GetTextChannelsAsync().Result.ToList();
      foreach(ITextChannel channel in textChannels)
      {
        if(channel.CategoryId.HasValue && categoryIds.Contains(channel.CategoryId.Value))
        {
          await channel.DeleteAsync();
        }
      }

      List<IVoiceChannel> voiceChannels = Guild.GetVoiceChannelsAsync().Result.ToList();
      foreach(IVoiceChannel channel in voiceChannels)
      {
        if(channel.CategoryId.HasValue && categoryIds.Contains(channel.CategoryId.Value))
        {
          await channel.DeleteAsync();
        }
      }

      foreach(ICategoryChannel channel in categoriesToDelete)
      {
        await channel.DeleteAsync();
      }
    }
#endregion
  };
#endregion
}