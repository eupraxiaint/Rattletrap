using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Discord;
using Newtonsoft.Json;
using YamlDotNet.Core.Tokens;
using System.Linq;

namespace Rattletrap
{
  public class GuildInstance
  {
    private static Dictionary<IGuild, GuildInstance> Instances = new Dictionary<IGuild, GuildInstance>();

    public IGuild Guild { get; private set; }
    public string Name { get; private set; }
    public ulong Id { get; private set; }
    public string Filepath { get; private set; }

    private bool _enabled = true;
    public bool Enabled { get { return _enabled; } set { _enabled = value; SaveToFile(); } }

    // old properties from GuildInfo

    // the list of queues by name
    public Dictionary<string, IQueue> Queues = new Dictionary<string, IQueue>();

    // the list of currently active matches
    public List<IMatch> Matches = new List<IMatch>();

    // the list of player positions mapped to their roles in the guild
    public Dictionary<PlayerPosition, IRole> PositionsToRoles = new Dictionary<PlayerPosition, IRole>();

    // the list of player ranks mapped to their roles in the guild
    public Dictionary<PlayerRank, IRole> RanksToRoles = new Dictionary<PlayerRank, IRole>();

    // the list of player ranks mapped to their emotes in the guild
    public Dictionary<PlayerRank, string> RanksToEmotes = new Dictionary<PlayerRank, string>();

    // the channel for admin bot commands
    public ITextChannel AdminBotChannel;

    // the channel for public bot commands
    public ITextChannel MainBotChannel;

    private struct SavedData
    {
      public string Name;
      public ulong Id;
      public bool Enabled;
    }

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
    private static ITextChannel FindTextChannel(IGuild InGuild, string InName)
    {
      IReadOnlyCollection<ITextChannel> textChannels = InGuild.GetTextChannelsAsync().Result;
      foreach(ITextChannel textChannel in textChannels)
      {
        if(textChannel.Name == InName)
        {
          return textChannel;
        }
      }

      return null;
    }

    public static GuildInstance Create(IGuild InGuild)
    {
      GuildInstance result = new GuildInstance();

      if(!Instances.ContainsKey(InGuild))
      {
        Instances.Add(InGuild, result);

        result.Filepath = Path.Combine(AppContext.BaseDirectory, $"../Saved/Guilds/{InGuild.Id}.json");

        if(!result.LoadFromFile())
        {
          result.Name = InGuild.Name;
          result.Id = InGuild.Id;
          result.Enabled = true;
          result.SaveToFile();
        }

        ITextChannel announcementChannel = FindTextChannel(InGuild, "\U0001f514inhouse-announcement");

        result.Queues["eu"] = new InhouseQueue("eu", announcementChannel);
        result.Queues["na"] = new InhouseQueue("na", announcementChannel);
        result.Queues["sea"] = new InhouseQueue("sea", announcementChannel);
        result.Queues["cis"] = new InhouseQueue("cis", announcementChannel);
        result.Queues["eu-1v1"] = new OneVOneQueue("eu-1v1", announcementChannel);
        result.Queues["na-1v1"] = new OneVOneQueue("na-1v1", announcementChannel);
        result.Queues["sea-1v1"] = new OneVOneQueue("sea-1v1", announcementChannel);
        result.Queues["cis-1v1"] = new OneVOneQueue("cis-1v1", announcementChannel);

        result.PositionsToRoles[PlayerPosition.Safelane] = FindRole(InGuild, "Safelane");
        result.PositionsToRoles[PlayerPosition.Midlane] = FindRole(InGuild, "Midlane");
        result.PositionsToRoles[PlayerPosition.Offlane] = FindRole(InGuild, "Offlane");
        result.PositionsToRoles[PlayerPosition.SoftSupport] = FindRole(InGuild, "Soft Support");
        result.PositionsToRoles[PlayerPosition.Support] = FindRole(InGuild, "Support");

        result.RanksToRoles[PlayerRank.Uncalibrated] = FindRole(InGuild, "Uncalibrated");
        result.RanksToRoles[PlayerRank.Herald] = FindRole(InGuild, "Herald");
        result.RanksToRoles[PlayerRank.Guardian] = FindRole(InGuild, "Guardian");
        result.RanksToRoles[PlayerRank.Crusader] = FindRole(InGuild, "Crusader");
        result.RanksToRoles[PlayerRank.Archon] = FindRole(InGuild, "Archon");
        result.RanksToRoles[PlayerRank.Legend] = FindRole(InGuild, "Legend");
        result.RanksToRoles[PlayerRank.Ancient] = FindRole(InGuild, "Ancient");
        result.RanksToRoles[PlayerRank.Divine] = FindRole(InGuild, "Divine");
        result.RanksToRoles[PlayerRank.Immortal] = FindRole(InGuild, "Immortal");

        result.RanksToEmotes[PlayerRank.Uncalibrated] = "<:Uncalibrated:901649283546234931>";
        result.RanksToEmotes[PlayerRank.Herald] = "<:Herald:901649551230906368>";
        result.RanksToEmotes[PlayerRank.Guardian] = "<:Guardian:901649591580098620>";
        result.RanksToEmotes[PlayerRank.Crusader] = "<:Crusader:901649627437203516>";
        result.RanksToEmotes[PlayerRank.Archon] = "<:Archon:901649670252679248>";
        result.RanksToEmotes[PlayerRank.Legend] = "<:Legend:901649722077491231>";
        result.RanksToEmotes[PlayerRank.Ancient] = "<:Ancient:901649761269063720>";
        result.RanksToEmotes[PlayerRank.Divine] = "<:Divine:901649806559154216>";
        result.RanksToEmotes[PlayerRank.Immortal] = "<:Immortal:901649831582380112>";

        result.MainBotChannel = FindTextChannel(InGuild, "\U0001f50einhouse-queue");
        result.AdminBotChannel = FindTextChannel(InGuild, "admin-bot-commands");
      }
      else
      {
        // todo: add a warning log here
        return Instances[InGuild];
      }

      return result;
    }

    public static GuildInstance Get(IGuild InGuild)
    {
      if(Instances.ContainsKey(InGuild))
      {
        return Instances[InGuild];
      }

      return null;
    }

    public bool IsAdmin(IGuildUser InUser)
    {
      return InUser.RoleIds.Contains(FindRole(InUser.Guild, "Admin").Id);
    }

    private void SaveToFile()
    {
      string guildsDir = Path.Combine(AppContext.BaseDirectory, "../Saved/Guilds");

      if(!Directory.Exists(guildsDir))
      {
        Directory.CreateDirectory(guildsDir);
      }

      SavedData data = new SavedData();
      data.Name = Name;
      data.Id = Id;
      data.Enabled = Enabled;
      TextWriter textWriter = new StreamWriter(Filepath);
      string textToWrite = JsonConvert.SerializeObject(data);
      textWriter.Write(textToWrite);
      textWriter.Close();
    }

    private bool LoadFromFile()
    {
      if(!File.Exists(Filepath))
      {
        return false;
      }

      TextReader textReader = new StreamReader(Filepath);
      string fileContents = textReader.ReadToEnd();
      SavedData data = JsonConvert.DeserializeObject<SavedData>(fileContents);
      Id = data.Id;
      Name = data.Name;
      Enabled = data.Enabled;
      return true;
    }
  };
}