using System;
using System.Threading.Tasks;
using OrangLauncher.Managers;
public class ExamplePlugin : ILauncherPlugin
{
    public string Name => "Example Plugin";
    public string Version => "1.0";
    public string Description => "A sample plugin that logs game launch and exit events.";
    public void OnLoad()
    {
        Console.WriteLine("[ExamplePlugin] Plugin loaded!");
    }
    public void OnUnload()
    {
        Console.WriteLine("[ExamplePlugin] Plugin unloaded.");
    }
    public Task OnBeforeGameLaunch(string profileName, string version)
    {
        Console.WriteLine($"[ExamplePlugin] Launching profile '{profileName}' on Minecraft {version}");
        return Task.CompletedTask;
    }
    public Task OnAfterGameExit(string profileName, int exitCode)
    {
        Console.WriteLine($"[ExamplePlugin] Game exited for profile '{profileName}' with code {exitCode}");
        return Task.CompletedTask;
    }
}