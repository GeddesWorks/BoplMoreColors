using BoplFixedMath;
using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoplMoreColors.Patches;

/// <summary>
/// Fixes GameSessionHandler.SpawnPlayers to handle more than 4 teams.
///
/// The vanilla method has three hardcoded [4] arrays:
///   1. teamSpawns (Vec2[4]) — spawn positions per team
///   2. int[] array = new int[4] — teammate-within-team counter
///   3. array[list[i].Team] — indexes by RAW team index into that int[4]
///
/// When players pick expanded team indices (6, 7, 8, ...), all three overflow
/// and players silently fail to spawn. This prefix replaces the method with
/// a version that sizes arrays dynamically.
/// </summary>
[HarmonyPatch(typeof(GameSessionHandler), "SpawnPlayers")]
internal static class SpawnPlayersExpandedTeamsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(GameSessionHandler __instance)
    {
        var players = PlayerHandler.Get().PlayerList();
        if (players.Count <= 4)
        {
            // Vanilla can handle 4 or fewer — let the original run
            return true;
        }

        SpawnPlayersFixed(__instance, players);
        return false; // skip original
    }

    private static void SpawnPlayersFixed(GameSessionHandler handler, List<Player> players)
    {
        // Mark game as in progress
        Traverse.Create(handler).Field<bool>("gameInProgress").Value = true;

        var slimeControllers = new SlimeController[players.Count];
        Traverse.Create(handler).Field<SlimeController[]>("slimeControllers").Value = slimeControllers;

        // Get unique sorted team indices
        var uniqueTeams = GetUniqueTeams(players);
        var teamCount = uniqueTeams.Length;

        // Find the highest raw team index for the per-team counter
        var maxTeamIndex = 0;
        foreach (var p in players)
        {
            if (p.Team > maxTeamIndex) maxTeamIndex = p.Team;
        }

        // Dynamically sized teammate counter (vanilla hardcodes int[4])
        var teammateCounter = new int[maxTeamIndex + 1];

        // Expand spawn positions to cover all teams by cycling vanilla positions
        var vanillaSpawns = handler.teamSpawns;
        var teamSpawns = new Vec2[teamCount];
        for (var i = 0; i < teamCount; i++)
        {
            teamSpawns[i] = vanillaSpawns[i % vanillaSpawns.Length];
        }

        var teammateSpawnSpacing = handler.teammateSpawnSpacing;
        var teamColors = handler.teamColors;
        var playerPrefab = handler.PlayerPrefab;
        var inputUpdaterPrefab = handler.InputUpdaterPrefab;
        var abilityReadyIndicators = handler.AbilityReadyIndicators;
        var playerSpawnsRoot = handler.playerSpawnsRoot;

        var inputUpdaters = new List<InputUpdater>();

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var teamPosIndex = System.Array.IndexOf(uniqueTeams, player.Team);

            player.playersAndClonesStillAlive = 1;

            // Calculate spawn position — same logic as vanilla but with expanded arrays
            Vec2 pos;
            if (teammateCounter[player.Team] == 0)
            {
                pos = teamSpawns[teamPosIndex];
            }
            else if (teammateCounter[player.Team] != 1)
            {
                pos = teamSpawns[teamPosIndex] + new Vec2(teammateSpawnSpacing, -teammateSpawnSpacing * (Fix)0.5);
            }
            else
            {
                pos = teamSpawns[teamPosIndex] - new Vec2(teammateSpawnSpacing, teammateSpawnSpacing * (Fix)0.5);
            }
            teammateCounter[player.Team]++;

            // Instantiate slime controller
            slimeControllers[i] = FixTransform.InstantiateFixed(playerPrefab, pos);
            slimeControllers[i].playerNumber = player.Id;
            slimeControllers[i].transform.SetParent(playerSpawnsRoot.transform);
            slimeControllers[i].GetPlayerSprite().sprite = null;
            slimeControllers[i].GetPlayerSprite().material = player.Color;

            // Set up abilities
            var abilityBehaviours = new List<AbilityMonoBehaviour>();
            player.CurrentAbilities = new List<GameObject>();
            var hasOffensive = false;
            var allRandom = true;

            for (var j = 0; j < player.Abilities.Count; j++)
            {
                if (player.Abilities[j].GetComponent<RandomAbility>() == null)
                {
                    allRandom = false;
                }
            }

            for (var k = 0; k < player.Abilities.Count; k++)
            {
                var randomAbility = player.Abilities[k].GetComponent<RandomAbility>();
                if (randomAbility != null)
                {
                    var randomPrefab = RandomAbility.GetRandomAbilityPrefab(
                        randomAbility.abilityIcons, randomAbility.abilityIcons_demo);
                    hasOffensive |= randomPrefab.isOffensiveAbility;

                    if (allRandom && !hasOffensive && k == player.Abilities.Count - 1)
                    {
                        randomPrefab = RandomAbility.GetRandomAbilityPrefab(
                            randomAbility.abilityIcons, randomAbility.abilityIcons_demo);
                    }

                    player.AbilityIcons[k] = randomPrefab.sprite;
                    var abilityGo = FixTransform.InstantiateFixed(randomPrefab.associatedGameObject, Vec2.zero);
                    abilityGo.SetActive(false);
                    player.CurrentAbilities.Add(abilityGo);
                    abilityBehaviours.Add(abilityGo.GetComponent<AbilityMonoBehaviour>());
                }
                else
                {
                    var abilityGo = FixTransform.InstantiateFixed(player.Abilities[k], Vec2.zero);
                    abilityGo.SetActive(false);
                    player.CurrentAbilities.Add(abilityGo);
                    abilityBehaviours.Add(abilityGo.GetComponent<AbilityMonoBehaviour>());
                }
            }

            slimeControllers[i].abilities = abilityBehaviours;

            // Set up ability ready indicators
            var indicators = new AbilityReadyIndicator[3];
            for (var l = 0; l < 3; l++)
            {
                indicators[l] = UnityEngine.Object.Instantiate(abilityReadyIndicators[l], playerSpawnsRoot.transform)
                    .GetComponent<AbilityReadyIndicator>();
                indicators[l].Init();
                indicators[l].SetColor(teamColors.teamColors[player.Team].fill);
                indicators[l].GetComponent<FollowTransform>().Leader = slimeControllers[i].transform;
                indicators[l].gameObject.SetActive(false);
                if (l < player.AbilityIcons.Count)
                {
                    indicators[l].SetSprite(player.AbilityIcons[l]);
                }
            }
            slimeControllers[i].AbilityReadyIndicators = indicators;

            // Reset game over flag
            Traverse.Create(handler).Field<bool>("gameOver").Value = false;

            // Set up input for local players
            if (player.IsLocalPlayer)
            {
                var inputUpdater = UnityEngine.Object.Instantiate(inputUpdaterPrefab);
                var playerInput = inputUpdater.GetComponent<PlayerInput>();
                inputUpdater.Claim(player.Id);
                inputUpdater.Init(player.Id);
                playerInput.neverAutoSwitchControlSchemes = true;
                playerInput.ActivateInput();
                inputUpdaters.Add(inputUpdater);
            }
        }

        Plugin.LogDiag($"SpawnPlayersFixed: spawned {players.Count} players across {teamCount} teams.");
    }

    private static int[] GetUniqueTeams(List<Player> players)
    {
        var teamSet = new HashSet<int>();
        foreach (var p in players)
        {
            teamSet.Add(p.Team);
        }
        var teams = new int[teamSet.Count];
        teamSet.CopyTo(teams);
        System.Array.Sort(teams);
        return teams;
    }
}
