using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.IO;
using System.Configuration;
using System;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Newtonsoft.Json;
using Microsoft.Scripting.Runtime;
using IronPython.Runtime.Exceptions;

namespace Rattletrap
{
  public class PlayerCollection
  {
    public List<Player> Players;

    public PlayerCollection()
    {
      Players = new List<Player>();
    }

    public PlayerCollection(List<Player> InPlayers)
    {
      Players = InPlayers;
    }

    public PlayerCollection(PlayerCollection InPlayerCollection)
    {
      Players = InPlayerCollection.Players;
    }

    public PlayerCollection FilterByRegion(string InRegion)
    {
      PlayerCollection result = new PlayerCollection();

      foreach(Player player in Players)
      {
        if(player.Regions.Contains(InRegion))
        {
          result.Players.Add(player);
        }
      }

      return result;
    }

    public PlayerCollection FilterByMode(string InMode)
    {
      PlayerCollection result = new PlayerCollection();

      foreach(Player player in Players)
      {
        if(player.Modes.Contains(InMode))
        {
          result.Players.Add(player);
        }
      }

      return result;
    }

    public PlayerCollection FilterByActive(bool InActive)
    {
      PlayerCollection result = new PlayerCollection();

      foreach(Player player in Players)
      {
        if(player.IsActive == InActive)
        {
          result.Players.Add(player);
        }
      }

      return result;
    }

    public PlayerCollection FilterByQueued(bool InQueued)
    {
      PlayerCollection result = new PlayerCollection();

      foreach(Player player in Players)
      {
        if(player.IsInQueue == InQueued)
        {
          result.Players.Add(player);
        }
      }

      return result;
    }

    public static PlayerCollection operator +(PlayerCollection InLhs, PlayerCollection InRhs)
    {
      PlayerCollection result = new PlayerCollection(InLhs);
      foreach(Player player in InRhs.Players)
      {
        result.Players.Add(player);
      }
      return result;
    }

    public static PlayerCollection operator -(PlayerCollection InLhs, PlayerCollection InRhs)
    {
      PlayerCollection result = new PlayerCollection(InLhs);
      foreach(Player player in InRhs.Players)
      {
        result.Players.Remove(player);
      }
      return result;
    }
  };

  public enum EPlayerRankMedal
  {
    Unranked,
    Herald,
    Guardian,
    Crusader,
    Archon,
    Legend,
    Ancient,
    Divine1To3,
    Divine4To5,
    ImmortalUnranked,
    ImmortalTop1000
  };

  public enum EPlayerRole
  {
    Support,
    SoftSupport,
    Offlane,
    Midlane,
    Safelane
  };

  public class Player
  {
    private static Dictionary<IGuildUser, Player> GuildUsersToPlayers = new Dictionary<IGuildUser, Player>();

    public string Name { get; private set; }
    public IGuildUser GuildUser { get; private set; }
    public string Filepath { get; private set; }
    public List<string> Modes = new List<string>();
    public List<string> Regions = new List<string>();
    public EPlayerRankMedal RankMedal;
    public List<EPlayerRole> PlayerRoles = new List<EPlayerRole>();
    public DateTime LastActiveTime = DateTime.UnixEpoch;
    public bool IsActive { get { return DateTime.Now - LastActiveTime < TimeSpan.FromHours(2); }}
    public bool IsInQueue = false;
    public IEmote Flair;

    public void UpdateRegionsFromPoll()
    {
      Regions.Clear();

      GuildInstance guildInst = GuildInstance.Get(GuildUser.Guild);

      if(guildInst.RegionsPoll == null)
      {
        return;
      }

      List<IEmote> reactions = guildInst.RegionsPoll.GetReactionsForUser(GuildUser);

      if(reactions.Count == 0)
      {
        Regions.AddRange(new List<string> {"eu", "na", "sa", "cis", "sea"});
      }
      else
      {
        foreach(IEmote emote in reactions)
        {
          if(emote.Name == StaticEmotes.NaRegion.Name)
          {
            Regions.Add("na");
          }
          else if(emote.Name == StaticEmotes.SaRegion.Name)
          {
            Regions.Add("sa");
          }
          else if(emote.Name == StaticEmotes.EuRegion.Name)
          {
            Regions.Add("eu");
          }
          else if(emote.Name == StaticEmotes.SeaRegion.Name)
          {
            Regions.Add("sea");
          }
          else if(emote.Name == StaticEmotes.CisRegion.Name)
          {
            Regions.Add("cis");
          }
        }
      }
    }

    public void UpdateRankMedalFromPoll()
    {
      RankMedal = EPlayerRankMedal.Unranked;

      GuildInstance guildInst = GuildInstance.Get(GuildUser.Guild);

      if(guildInst.RanksPoll == null)
      {
        return;
      }

      List<IEmote> reactions = guildInst.RanksPoll.GetReactionsForUser(GuildUser);

      if(reactions.Count == 1)
      {
        RankMedal = StaticEmotes.GetRankMedalEnumFromEmote(reactions[0]);
      }
      else 
      {
        RankMedal = EPlayerRankMedal.Unranked;
      }
    }

    public void UpdatePlayerRolesFromPoll()
    {
      PlayerRoles.Clear();

      GuildInstance guildInst = GuildInstance.Get(GuildUser.Guild);

      if(guildInst.RolesPoll == null)
      {
        return;
      }

      List<IEmote> reactions = guildInst.RolesPoll.GetReactionsForUser(GuildUser);

      foreach(IEmote emote in reactions)
      {
        PlayerRoles.Add(StaticEmotes.GetPlayerRoleEnumFromEmote(emote));
      }

      if(PlayerRoles.Count == 0)
      {
        PlayerRoles.Add(EPlayerRole.Support);
        PlayerRoles.Add(EPlayerRole.SoftSupport);
        PlayerRoles.Add(EPlayerRole.Offlane);
        PlayerRoles.Add(EPlayerRole.Midlane);
        PlayerRoles.Add(EPlayerRole.Safelane);
      }
    }

    public void UpdateFlairFromPoll()
    {
      GuildInstance guildInst = GuildInstance.Get(GuildUser.Guild);

      if(guildInst.FlairPoll == null)
      {
        return;
      }

      List<IEmote> reactions = guildInst.FlairPoll.GetReactionsForUser(GuildUser);

      if(reactions.Count == 0)
      {
        Flair = StaticEmotes.WhiteMediumSquare;
      }
      else
      {
        Flair = reactions[0];
      }
    }

    public void UpdateModesFromPoll()
    {
      Modes.Clear();

      GuildInstance guildInst = GuildInstance.Get(GuildUser.Guild);

      if(guildInst.ModesPoll == null)
      {
        return;
      }
      
      List<IEmote> reactions = guildInst.ModesPoll.GetReactionsForUser(GuildUser);

      foreach(IEmote emote in reactions)
      {
        Modes.Add(StaticEmotes.GetModeNameFromEmote(emote));
      }

      if(Modes.Count == 0)
      {
        Modes.Add("inhouse");
        Modes.Add("practice");
        Modes.Add("casual");
      }
    }

    public static Player GetOrCreate(IGuildUser InUser)
    {
      GuildInstance guildInst = GuildInstance.Get(InUser.Guild);

      if(GuildUsersToPlayers.ContainsKey(InUser))
      {
        return GuildUsersToPlayers[InUser];
      }

      Player player = new Player();
      GuildUsersToPlayers[InUser] = player;

      player.Filepath = Path.Combine(AppContext.BaseDirectory, $"../Saved/Players/{InUser.Id}.json");
      player.GuildUser = InUser;

      if(!player.LoadFromFile())
      {
        player.Name = InUser.Username;
        player.SaveToFile();
      }

      player.UpdateRegionsFromPoll();
      player.UpdateRankMedalFromPoll();
      player.UpdatePlayerRolesFromPoll();
      player.UpdateModesFromPoll();
      player.UpdateFlairFromPoll();

      return player;
    }

    public void SetActive()
    {
      LastActiveTime = DateTime.Now;
    }

    public void SaveToFile()
    {
      string playersDir = Path.Combine(AppContext.BaseDirectory, "../Saved/Players");
      if(!Directory.Exists(playersDir))
      {
        Directory.CreateDirectory(playersDir);
      }

      SavedData savedData = new SavedData();
      savedData.Name = Name;
      
      TextWriter textWriter = new StreamWriter(Filepath);
      string textToWrite = JsonConvert.SerializeObject(savedData);
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
      textReader.Close();

      Name = data.Name;

      return true;
    }

    private class SavedData
    {
      public string Name;
    }
  }
}