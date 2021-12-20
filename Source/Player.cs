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

  public class Player
  {
    private static Dictionary<IGuildUser, Player> GuildUsersToPlayers = new Dictionary<IGuildUser, Player>();

    public string Name { get; private set; }
    public IGuildUser GuildUser { get; private set; }
    public string Filepath { get; private set; }
    public List<string> Modes = new List<string>();
    public List<string> Regions = new List<string>();

    private void FillRegionsFromRoles()
    {
      Regions.Clear();
      foreach(ulong roleId in GuildUser.RoleIds)
      {
        IRole role = GuildUser.Guild.GetRole(roleId);
        if(role.Name == "NA")
        {
          Regions.Add("na");
        }
        else if(role.Name == "EU")
        {
          Regions.Add("eu");
        }
        else if(role.Name == "CIS")
        {
          Regions.Add("cis");
        }
        else if(role.Name == "SEA")
        {
          Regions.Add("sea");
        }
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
        player.Modes = new List<string> {"inhouse", "practice", "casual"};
        player.FillRegionsFromRoles();
        player.SaveToFile();
      }

      return player;
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
      savedData.Modes = Modes;
      savedData.Regions = Regions;
      
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
      Modes = data.Modes;
      Regions = data.Regions;

      return true;
    }

    private class SavedData
    {
      public string Name;
      public List<string> Modes = new List<string>();
      public List<string> Regions = new List<string>();
    }
  }
}