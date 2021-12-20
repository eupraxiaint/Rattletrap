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
  }

  public abstract class IMatchGeneratorBase
  {
    public abstract List<MatchCandidate> CheckForMatches(PlayerCollection InPlayers);
    public abstract IMatch2 GenerateMatch(MatchCandidate InCandidate);
  }

  public class MatchGenerator<MatchType> : IMatchGeneratorBase where MatchType : IMatch2, new()
  {
    public GuildInstance GuildInstance;

    public MatchGenerator(GuildInstance InGuildInst)
    {
      GuildInstance = InGuildInst;
    }

    public override List<MatchCandidate> CheckForMatches(PlayerCollection InPlayers)
    {
      return (List<MatchCandidate>)typeof(MatchType).GetMethod("CheckForMatches").Invoke(null, new object[]{InPlayers});
    }

    public override IMatch2 GenerateMatch(MatchCandidate InCandidate)
    {
      MatchType match = new MatchType();
      match.Players = InCandidate.Players;
      match.GuildInstance = GuildInstance;
      match.Initialize();
      return match;
    }
  }

  public abstract class IMatch2
  {
    public PlayerCollection Players;
    public GuildInstance GuildInstance;

    public abstract void Initialize();
  }
}