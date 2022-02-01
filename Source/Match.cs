using System.Collections.Generic;
using IronPython.Compiler.Ast;
using IronPython.Runtime;
using Microsoft.Scripting.Utils;
using System;

namespace Rattletrap
{
  public class MatchCandidate
  {
    public PlayerCollection Players = new PlayerCollection();
    public object UserData;
  }

  public class MatchmakingPoke
  {
    public Player Player;
    public string Message;
  }

  public abstract class IMatchGeneratorBase
  {
    public abstract List<MatchCandidate> CheckForMatches(PlayerCollection InPlayers, List<MatchmakingPoke> OutPokes);
    public abstract IMatch2 GenerateMatch(MatchCandidate InCandidate);
  }

  public class MatchGenerator<MatchType> : IMatchGeneratorBase where MatchType : IMatch2, new()
  {
    public GuildInstance GuildInstance;

    public MatchGenerator(GuildInstance InGuildInst)
    {
      GuildInstance = InGuildInst;
    }

    public override List<MatchCandidate> CheckForMatches(PlayerCollection InPlayers, List<MatchmakingPoke> OutPokes)
    {
      return (List<MatchCandidate>)typeof(MatchType).GetMethod("CheckForMatches").Invoke(null, new object[]{InPlayers, OutPokes});
    }

    private static int NextId = 0;

    public override IMatch2 GenerateMatch(MatchCandidate InCandidate)
    {
      MatchType match = new MatchType();
      match.Players = InCandidate.Players;
      match.GuildInstance = GuildInstance;
      match.Id = NextId++;
      foreach(Player player in match.Players.Players)
      {
        player.IsInQueue = false;
      }
      IMatch2.RegisterMatch(match.Id, match);
      match.Initialize(InCandidate.UserData);
      return match;
    }
  }

  public abstract class IMatch2
  {
    public PlayerCollection Players;
    public GuildInstance GuildInstance;
    public int Id;
  
    protected static Dictionary<int, IMatch2> Matches = new Dictionary<int, IMatch2>();

    public static void RegisterMatch(int InId, IMatch2 InMatch)
    {
      Matches[InId] = InMatch;
    }

    public static IMatch2 GetMatchById(int InId)
    {
      if(Matches.ContainsKey(InId))
      {
        return Matches[InId];
      }

      return null;
    }

    public abstract void Initialize(object InUserData);

    public virtual string GetMatchInfo()
    {
      return "";
    }
  }
}