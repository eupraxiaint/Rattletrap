using IronPython.Runtime;
using System.Threading;
using System.ComponentModel;
using System.Net.Http;
using System;
using IronPython.Hosting;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Rattletrap
{    
  class LobbyHoster
  {
    private Thread Thread = null;
    private Process Process = null;
    private Socket Listener = null;
    private Socket Handler = null;
    private bool IsReady = false;

    public LobbyHoster()
    {
      Thread = new Thread(new ThreadStart(RunLobbyHoster));
      Thread.Start();
      while(!IsReady) { }
    }

    ~LobbyHoster()
    {
      Process.Close();
      Thread.Join();
    }

    private void OnOutputDataReceived(object InSender, DataReceivedEventArgs InArgs)
    {
      Console.WriteLine(InArgs.Data);
    }

    private void OnErrorDataReceived(object InSender, DataReceivedEventArgs InArgs)
    {
      Console.WriteLine(InArgs.Data);
    }

    private void RunLobbyHoster()
    {
      IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
      IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
      IPEndPoint localEP = new IPEndPoint(ipAddress, 42069);

      Listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
      Listener.Bind(localEP);
      Listener.Listen();

      Process = new Process();

      Process.StartInfo.FileName = MatchService.Config["python"];
      Process.StartInfo.Arguments = "Dependencies/DotaHoster/dota_hoster.py";
      Process.StartInfo.UseShellExecute = false;
      Process.StartInfo.RedirectStandardOutput = true;
      Process.StartInfo.RedirectStandardError = true;
      Process.StartInfo.RedirectStandardInput = true;
      Process.OutputDataReceived += OnOutputDataReceived;
      Process.ErrorDataReceived += OnErrorDataReceived;
      Process.EnableRaisingEvents = true;
      
      Process.Start();
      Process.BeginOutputReadLine();
      Process.BeginErrorReadLine();

      Handler = Listener.Accept();

      byte[] bytes = new byte[1024];
      int bytesRec = Handler.Receive(bytes);
      Console.WriteLine($"Received response: {Encoding.ASCII.GetString(bytes, 0, bytesRec)}");

      IsReady = true;

      Process.WaitForExit();

      Handler.Shutdown(SocketShutdown.Both);
      Handler.Close();
    }

    public void CreateLobby(LobbyCreateInfo InCreateInfo)
    {
      string message = $"createlobby \"{InCreateInfo.Name}\" \"{InCreateInfo.Password}\"";
      Handler.Send(Encoding.ASCII.GetBytes(message));
    }
  };

  public struct LobbyCreateInfo
  {
    public string Name;
    public string Password;
  }

  public class Lobby
  {
    private static LobbyHoster hoster = null;

    public Lobby(LobbyCreateInfo InCreateInfo)
    {
      if(hoster == null)
      {
        hoster = new LobbyHoster();
      }

      hoster.CreateLobby(InCreateInfo);
    }
  }
}