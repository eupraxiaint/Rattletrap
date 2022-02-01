using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using Discord;
using System.Net;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.CompilerServices;
using System.IO;

namespace Rattletrap
{
  public enum ELobbyGameMode
  {
    AllPick,
    CaptainsMode,
    OneVOne,
  }

  public enum ELobbyCmPick
  {
    Random,
    Radiant,
    Dire
  }

  public struct LobbyCreateInfo
  {
    public string Name;
    public string Password;
    public ELobbyGameMode GameMode;
    public string Region;
    public ELobbyCmPick CmPick;
  }

  public class LobbyRunner
  {
    public int Id;
    public IMatch2 CurrentMatch = null;
    private Thread LobbyHosterThread;
    private Process LobbyHosterProcess;
    private Socket ListenerSocket = null;
    private Socket HandlerSocket = null;
    private bool LobbyHostIsReady = false;
    private static LobbyRunner[] LobbyRunners;
    private StartMatchResult StartResult;
    private bool PendingMatchStart = false;

    public enum StartMatchResult
    {
      Success,
      NoPlayers
    }

    public static void CreateLobbyRunners(int InNumRunners)
    {
      LobbyRunners = new LobbyRunner[InNumRunners];

      for(int runnerIdx = 0; runnerIdx < InNumRunners; ++runnerIdx)
      {
        LobbyRunners[runnerIdx] = new LobbyRunner();
        LobbyRunners[runnerIdx].Initialize(runnerIdx);
      }
    }

    public static LobbyRunner GetAvailableLobbyRunner()
    {
      for(int runnerIdx = 0; runnerIdx < LobbyRunners.Length; ++runnerIdx)
      {
        if(LobbyRunners[runnerIdx].CurrentMatch == null)
        {
          return LobbyRunners[runnerIdx];
        }
      }

      return null;
    }

    public static int GetNumAvailableLobbyRunners()
    {
      int result = 0;

      for(int runnerIdx = 0; runnerIdx < LobbyRunners.Length; ++runnerIdx)
      {
        if(LobbyRunners[runnerIdx].CurrentMatch == null)
        {
          ++result;
        }
      }

      return result;
    }

    public void Initialize(int InId)
    {
      Id = InId;

      LobbyHosterThread = new Thread(new ThreadStart(RunLobbyHost));
      LobbyHosterThread.Start();
      while(!LobbyHostIsReady){ }
    }

    public void AssignMatch(IMatch2 InMatch)
    {
      CurrentMatch = InMatch;
    }

    private void OnOutputDataReceived(object InSender, DataReceivedEventArgs InArgs)
    {
      Console.WriteLine(InArgs.Data);
    }

    private void OnErrorDataReceived(object InSender, DataReceivedEventArgs InArgs)
    {
      Console.WriteLine(InArgs.Data);
    }

    private void RunLobbyHost()
    {
      IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
      IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
      IPEndPoint localEP = new IPEndPoint(ipAddress, 42069);

      ListenerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
      ListenerSocket.Bind(localEP);
      ListenerSocket.Listen();

      LobbyHosterProcess = new Process();

      LobbyHosterProcess.StartInfo.FileName = MatchService.Config["python"];
      LobbyHosterProcess.StartInfo.Arguments = "Dependencies/DotaHoster/dota_hoster.py";
      LobbyHosterProcess.StartInfo.UseShellExecute = false;
      LobbyHosterProcess.StartInfo.RedirectStandardOutput = true;
      LobbyHosterProcess.StartInfo.RedirectStandardError = true;
      LobbyHosterProcess.StartInfo.RedirectStandardInput = true;
      LobbyHosterProcess.OutputDataReceived += OnOutputDataReceived;
      LobbyHosterProcess.ErrorDataReceived += OnErrorDataReceived;
      LobbyHosterProcess.EnableRaisingEvents = true;
      
      LobbyHosterProcess.Start();
      LobbyHosterProcess.BeginOutputReadLine();
      LobbyHosterProcess.BeginErrorReadLine();

      HandlerSocket = ListenerSocket.Accept();

      byte[] bytes = new byte[1024];
      int bytesRec = HandlerSocket.Receive(bytes);
      Console.WriteLine($"Received response: {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");

      LobbyHostIsReady = true;

      while(!LobbyHosterProcess.HasExited)
      {
        byte[] msgBytes = new byte[1024];
        string msg = "";
        try
        {
          int msgBytesRec = HandlerSocket.Receive(msgBytes);
          msg = Encoding.ASCII.GetString(msgBytes, 0, msgBytesRec);
        }
        catch(IOException e)
        {

        }

        if(msg != "")
        {
          if(msg == "draftstarted")
          {
            OnDraftStarted?.Invoke();
            if(PendingMatchStart)
            {
              StartResult = StartMatchResult.Success;
              PendingMatchStart = false;
            }
          }
          else if(msg == "matchended")
          {
            OnMatchEnded?.Invoke();
          }
          else if(msg == "noplayers")
          {
            if(PendingMatchStart)
            {
              StartResult = StartMatchResult.NoPlayers;
              PendingMatchStart = false;
            }
          }
        }
        Console.WriteLine($"Received response: {msg}");
      }

      HandlerSocket.Shutdown(SocketShutdown.Both);
      HandlerSocket.Close();
    }

    private void SendMessage(string InMessage)
    {
      HandlerSocket.Send(Encoding.ASCII.GetBytes(InMessage));
    }

    public void CreateLobby(LobbyCreateInfo InCreateInfo)
    {
      string region = InCreateInfo.Region == "" ? "eu" : InCreateInfo.Region;
      
      string cmPick = "0";
      switch(InCreateInfo.CmPick)
      {
      case ELobbyCmPick.Random:
        cmPick = "0";
        break;
      case ELobbyCmPick.Radiant:
        cmPick = "1";
        break;
      case ELobbyCmPick.Dire:
        cmPick = "2";
        break;
      }

      string message = $"createlobby \"{InCreateInfo.Name}\" \"{InCreateInfo.Password}\" {InCreateInfo.GameMode.ToString()} {InCreateInfo.Region}"
        + $" {cmPick}";
      SendMessage(message);
    }

    public void Reset()
    {
      CurrentMatch = null;
      SendMessage("reset");
    }

    public async Task<StartMatchResult> StartMatch()
    {
      SendMessage("startmatch");
      PendingMatchStart = true;
      while(PendingMatchStart)
      {
        await Task.Delay(TimeSpan.FromSeconds(1));
      }

      return StartResult;
    }

    public delegate void OnDraftStartedDelegate();
    public OnDraftStartedDelegate OnDraftStarted;

    public delegate void OnMatchStartedDelegate();
    public OnMatchStartedDelegate OnMatchEnded;
  }

  public struct LobbyAnnounceWidgetInitInfo
  {
    public string Name;
    public string Password;
    public PlayerCollection Players;
    public bool MentionUsers;
  }

  public class LobbyAnnounceWidget : PermaPoll
  {
    public async void Initialize(LobbyAnnounceWidgetInitInfo InInitInfo)
    {
      PermaPollInitInfo pollInitInfo;
      pollInitInfo.Embed = new EmbedBuilder();
      pollInitInfo.Embed.Title = "Lobby Ready";
      pollInitInfo.Embed.Color = Color.Green;
      pollInitInfo.Embed.AddField("Lobby Name", InInitInfo.Name);
      pollInitInfo.Embed.AddField("Lobby Password", InInitInfo.Password);
      pollInitInfo.Embed.AddField("Join and ready up!", 
        $"Click the {StaticEmotes.WhiteHeavyCheckMark} below when you are in the lobby and ready, or click "
        + $"{StaticEmotes.StopSign} to cancel the match. You have 15 minutes to join the match.");
      pollInitInfo.Embed.Timestamp = DateTime.Now;
      pollInitInfo.Exclusive = true;
      pollInitInfo.Reactions = new List<IEmote> { StaticEmotes.WhiteHeavyCheckMark, StaticEmotes.StopSign };
      pollInitInfo.ShowResponses = true;
      pollInitInfo.Timeout = TimeSpan.FromMinutes(15);
      pollInitInfo.MentionUsers = InInitInfo.MentionUsers;
      pollInitInfo.Users = new List<IGuildUser>();
      foreach(Player player in InInitInfo.Players.Players)
      {
        pollInitInfo.Users.Add(player.GuildUser);
      }

      await base.Initialize(pollInitInfo);

      base.OnReactionModified += ReactionModified;
    }

    private async Task ReactionModified(IEmote InEmote, IGuildUser InUser, bool InAdded)
    {
      Player player = Player.GetOrCreate(InUser);

      if(InAdded && Users.Contains(InUser))
      {
        if(InEmote.Name == StaticEmotes.StopSign.Name)
        {
          Close();
          OnCanceled.Invoke(player);
        }
        else if(InEmote.Name == StaticEmotes.WhiteHeavyCheckMark.Name)
        {
          List<IGuildUser> users = GetUsersForEmote(InEmote);
          if(users.Count == Users.Count)
          {
            OnAllPlayersReady.Invoke();
          }
        }
      }
    }

    public delegate void OnCanceledDelegate(Player InCancelingPlayer);
    public OnCanceledDelegate OnCanceled;

    public delegate void OnAllPlayersReadyDelegate();
    public OnAllPlayersReadyDelegate OnAllPlayersReady;
  }
}