using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace BoplMoreColors;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency("com.geddesworks.fixedmorebopl", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.geddesworks.boplmorecolors";
    public const string PluginName = "MoreTeams";
    public const string PluginVersion = "0.1.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<int> ExtraColorCount = null!;
    internal static ConfigEntry<bool> EnableDiagnostics = null!;

    private Harmony? _harmony;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        BindConfig();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll(Assembly.GetExecutingAssembly());

        var patchedMethods = _harmony.GetPatchedMethods()
            .Where(method => Harmony.GetPatchInfo(method)?.Owners.Contains(PluginGuid) == true)
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .OrderBy(name => name)
            .ToArray();

        Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        Log.LogInfo($"Extra colors to add: {ExtraColorCount.Value}.");
        Log.LogInfo($"Patched {patchedMethods.Length} methods:\n  - {string.Join("\n  - ", patchedMethods)}");
    }

    private void BindConfig()
    {
        ExtraColorCount = Config.Bind(
            "General",
            "ExtraColorCount",
            8,
            new ConfigDescription(
                "Number of extra team colors to add beyond the vanilla palette.",
                new AcceptableValueRange<int>(0, 12)));

        EnableDiagnostics = Config.Bind(
            "Diagnostics",
            "EnableDiagnostics",
            true,
            "Log diagnostic info about color expansion at startup.");
    }

    internal static void LogDiag(string message)
    {
        if (EnableDiagnostics.Value)
        {
            Log.LogInfo($"[Diag] {message}");
        }
    }
}
