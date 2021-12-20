using IronPython.Runtime;
using System.Threading;
using System.ComponentModel;
using System.Net.Http;
using System;
using IronPython.Hosting;
using System.Collections.Generic;

namespace Rattletrap
{
  public struct LobbyCreateInfo
  {

  }

  public class Lobby
  {
    private static void InitializeScript()
    {
      var engine = Python.CreateEngine();
      var scope = engine.CreateScope();
      engine.SetSearchPaths(new List<string>{"C:\\Users\\chase\\AppData\\Local\\Programs\\Python\\Python310\\Lib", 
        "C:\\Users\\chase\\AppData\\Local\\Programs\\Python\\Python310\\Lib\\site-packages"});
      var source = engine.CreateScriptSourceFromFile("DotaHoster/dota_hoster.py");
      var compiled = source.Compile();
      var result = compiled.Execute(scope);

      ScriptInitialized = true;
    }

    private static bool ScriptInitialized = false;

    public Lobby(LobbyCreateInfo InCreateInfo)
    {
      if(!ScriptInitialized)
      {
        InitializeScript();
      }
    }
  }
}