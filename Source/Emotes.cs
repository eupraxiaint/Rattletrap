using System.Collections.Generic;
using System.Net.Security;
using System.Reflection.Metadata;
using Discord;

namespace Rattletrap
{
  public class StaticEmotes
  {
    public static Emoji CrystalBall = new Emoji("\U0001F52E");
    public static Emoji Toolbox = new Emoji("\U0001F9F0");
    public static Emoji CrossedSwords = new Emoji("\U00002694");
    public static Emoji Books = new Emoji("\U0001F4DA");
    public static Emoji PersonJuggling = new Emoji("\U0001F939");
    public static Emoji House = new Emoji("\U0001F3E0");
    public static Emoji DiamondWithADot = new Emoji("\U0001F4A0");
    public static Emoji StopSign = new Emoji("\U0001F6D1");
    public static Emoji WhiteHeavyCheckMark = new Emoji("\u2705");
    public static Emoji CrossMark = new Emoji("\u274C");
    public static Emoji Sunglasses = new Emoji("\U0001F60E");
    public static Emoji ZanyFace = new Emoji("\U0001F92A");
    public static Emoji Clown = new Emoji("\U0001F921");
    public static Emoji Robot = new Emoji("\U0001F916");
    public static Emoji Cowboy = new Emoji("\U0001F920");
    public static Emoji Smirk = new Emoji("\U0001F60F");
    public static Emoji Boar = new Emoji("\U0001F417");
    public static Emoji TigerFace = new Emoji("\U0001F42F");
    public static Emoji DogFace = new Emoji("\U0001F436");
    public static Emoji Detective = new Emoji("\U0001F575");
    public static Emoji Facepalm = new Emoji("\U0001F926");
    public static Emoji Snowman = new Emoji("\U00002603");
    public static Emoji WhiteMediumSquare = new Emoji("\U000025FB");
    public static Emoji HighVoltage = new Emoji("\U000026A1");
    public static Emoji RevolvingHearts = new Emoji("\U0001F49E");
    public static Emoji Tent = new Emoji("\U000026FA");
    public static Emoji CityscapeAtDusk = new Emoji("\U0001F306");
    public static Emoji TriangularFlag = new Emoji("\U0001F6A9");
    public static Emoji CounterclockwiseArrows = new Emoji("\U0001F504");
    public static Emoji PointUp = new Emoji("\u261D");
    public static Emoji VictoryHand = new Emoji("\u270C");

    public static string GetModeNameFromEmote(IEmote InEmote)
    {
      if(InEmote.Name == House.Name)
      {
        return "inhouse";
      }
      else if(InEmote.Name == Books.Name)
      {
        return "practice";
      }
      else if(InEmote.Name == PersonJuggling.Name)
      {
        return "casual";
      }
      else if(InEmote.Name == CrossedSwords.Name)
      {
        return "duel";
      }

      return "";
    }

    public static Emoji GetModeEmojiFromName(string InName)
    {
      if(InName == "inhouse")
      {
        return House;
      }
      else if(InName == "practice")
      {
        return Books;
      }
      else if(InName == "casual")
      {
        return PersonJuggling;
      }
      else if(InName == "duel")
      {
        return CrossedSwords;
      }

      return null;
    }

    public static Emote NaRegion = Emote.Parse("<:na:929951746606891070>");
    public static Emote EuRegion = Emote.Parse("<:eu:929951672673910886>");
    public static Emote CisRegion = Emote.Parse("<:cis:929951672824901703>");
    public static Emote SeaRegion = Emote.Parse("<:sea:929951672791347231>");
    public static Emote SaRegion = Emote.Parse("<:sa:929975964933447680>");

    public static Emote GetRegionEmoteFromName(string InName)
    {
      switch(InName)
      {
      case "na":
        return NaRegion;
      case "sa":
        return SaRegion;
      case "cis":
        return CisRegion;
      case "sea":
        return SeaRegion;
      case "eu":
        return EuRegion;
      default:
        return NaRegion;
      }
    }

    public static Emote UncalibratedRank = Emote.Parse("<:uncalibrated:929953979775995914>");
    public static Emote HeraldRank = Emote.Parse("<:herald:929953979759202384>");
    public static Emote GuardianRank = Emote.Parse("<:guardian:929953979834720317>");
    public static Emote CrusaderRank = Emote.Parse("<:crusader:929953979427860481>");
    public static Emote ArchonRank = Emote.Parse("<:archon:929953979511734343>");
    public static Emote LegendRank = Emote.Parse("<:legend:929953979775995984>");
    public static Emote AncientRank = Emote.Parse("<:ancient:929953979532726283>");
    public static Emote Divine1To3Rank = Emote.Parse("<:divine1to3:932412175224553512>");
    public static Emote Divine4To5Rank = Emote.Parse("<:divine4to5:932412174876409896>");
    public static Emote ImmortalUnrankedRank = Emote.Parse("<:immortalunranked:932412174624763925>");
    public static Emote ImmortalTop1000Rank = Emote.Parse("<:immortaltop1000:932412174926753802>");

    public static EPlayerRankMedal GetRankMedalEnumFromEmote(IEmote InEmote)
    {
      if(InEmote.Name == UncalibratedRank.Name)
      {
        return EPlayerRankMedal.Unranked;
      }
      else if(InEmote.Name == HeraldRank.Name)
      {
        return EPlayerRankMedal.Herald;
      }
      else if(InEmote.Name == GuardianRank.Name)
      {
        return EPlayerRankMedal.Guardian;
      }
      else if(InEmote.Name == CrusaderRank.Name)
      {
        return EPlayerRankMedal.Crusader;
      }
      else if(InEmote.Name == ArchonRank.Name)
      {
        return EPlayerRankMedal.Archon;
      }
      else if(InEmote.Name == LegendRank.Name)
      {
        return EPlayerRankMedal.Legend;
      }
      else if(InEmote.Name == AncientRank.Name)
      {
        return EPlayerRankMedal.Herald;
      }
      else if(InEmote.Name == Divine1To3Rank.Name)
      {
        return EPlayerRankMedal.Divine1To3;
      }
      else if(InEmote.Name == Divine4To5Rank.Name)
      {
        return EPlayerRankMedal.Divine4To5;
      }
      else if(InEmote.Name == ImmortalUnrankedRank.Name)
      {
        return EPlayerRankMedal.ImmortalUnranked;
      }
      else if(InEmote.Name == ImmortalTop1000Rank.Name)
      {
        return EPlayerRankMedal.ImmortalTop1000;
      }

      return EPlayerRankMedal.Unranked;
    }

    public static Emote GetRankMedalEmoteFromEnum(EPlayerRankMedal InRank)
    {
      switch(InRank)
      {
      case EPlayerRankMedal.Unranked:
        return UncalibratedRank;
      case EPlayerRankMedal.Herald:
        return HeraldRank;
      case EPlayerRankMedal.Guardian:
        return GuardianRank;
      case EPlayerRankMedal.Crusader:
        return CrusaderRank;
      case EPlayerRankMedal.Archon:
        return ArchonRank;
      case EPlayerRankMedal.Legend:
        return LegendRank;
      case EPlayerRankMedal.Ancient:
        return AncientRank;
      case EPlayerRankMedal.Divine1To3:
        return Divine1To3Rank;
      case EPlayerRankMedal.Divine4To5:
        return Divine4To5Rank;
      case EPlayerRankMedal.ImmortalUnranked:
        return ImmortalUnrankedRank;
      case EPlayerRankMedal.ImmortalTop1000:
        return ImmortalTop1000Rank;
      default:
        return UncalibratedRank;
      }
    }

    public static Emote SupportRole = Emote.Parse("<:support:929956304288645130>");
    public static Emote SoftSupportRole = Emote.Parse("<:softsupport:929956304246669412>");
    public static Emote OfflaneRole = Emote.Parse("<:offlane:929956304238280724>");
    public static Emote MidlaneRole = Emote.Parse("<:midlane:929956304255070318>");
    public static Emote SafelaneRole = Emote.Parse("<:safelane:929956304284450866>");

    public static Emote GetPlayerRoleEmoteFromEnum(EPlayerRole InRole)
    {
      switch(InRole)
      {
      case EPlayerRole.Support:
        return SupportRole;
      case EPlayerRole.SoftSupport:
        return SoftSupportRole;
      case EPlayerRole.Offlane:
        return OfflaneRole;
      case EPlayerRole.Midlane:
        return MidlaneRole;
      case EPlayerRole.Safelane:
        return SafelaneRole;
      default:
        return SupportRole;
      }
    }

    public static EPlayerRole GetPlayerRoleEnumFromEmote(IEmote InEmote)
    {
      if(InEmote.Name == SupportRole.Name)
      {
        return EPlayerRole.Support;
      }
      else if(InEmote.Name == SoftSupportRole.Name)
      {
        return EPlayerRole.SoftSupport;
      }
      else if(InEmote.Name == OfflaneRole.Name)
      {
        return EPlayerRole.Offlane;
      }
      else if(InEmote.Name == MidlaneRole.Name)
      {
        return EPlayerRole.Midlane;
      }
      else if(InEmote.Name == SafelaneRole.Name)
      {
        return EPlayerRole.Safelane;
      }
      return EPlayerRole.Support;
    }
  }
}