using HarmonyLib;
using UnityEngine;

namespace BoplMoreColors.Patches;

/// <summary>
/// Expands the game's TeamColors palette so each of 8 players can have a unique team.
/// Patches CharacterSelectHandler.Awake (postfix) to inject new TeamColor entries.
///
/// TeamSelector cycles through TeamColors.teamColors.Length and sets PlayerInit.team.
/// The vanilla game already has enough PlayerColors (slime materials), so we only
/// expand TeamColors (fill/border/saturated UI colors for team identity).
/// </summary>
[HarmonyPatch(typeof(CharacterSelectHandler), "Awake")]
internal static class ColorExpansionPatch
{
    // Extra colors chosen to be visually distinct from the vanilla palette.
    private static readonly ExtraColorDef[] ExtraColors = new[]
    {
        new ExtraColorDef("Orange",    new Color(1.00f, 0.55f, 0.05f)),
        new ExtraColorDef("Pink",      new Color(1.00f, 0.30f, 0.60f)),
        new ExtraColorDef("Cyan",      new Color(0.00f, 0.85f, 0.85f)),
        new ExtraColorDef("Lime",      new Color(0.50f, 1.00f, 0.15f)),
        new ExtraColorDef("Maroon",    new Color(0.55f, 0.05f, 0.10f)),
        new ExtraColorDef("Teal",      new Color(0.10f, 0.55f, 0.55f)),
        new ExtraColorDef("Gold",      new Color(1.00f, 0.84f, 0.00f)),
        new ExtraColorDef("Lavender",  new Color(0.70f, 0.50f, 0.90f)),
        new ExtraColorDef("Coral",     new Color(1.00f, 0.50f, 0.31f)),
        new ExtraColorDef("Mint",      new Color(0.40f, 1.00f, 0.70f)),
        new ExtraColorDef("Navy",      new Color(0.05f, 0.10f, 0.45f)),
        new ExtraColorDef("Peach",     new Color(1.00f, 0.75f, 0.55f)),
    };

    [HarmonyPostfix]
    private static void Postfix(CharacterSelectHandler __instance)
    {
        var extraCount = Plugin.ExtraColorCount.Value;
        if (extraCount <= 0)
        {
            return;
        }

        // Clamp to available definitions
        var toAdd = Mathf.Min(extraCount, ExtraColors.Length);

        ExpandTeamColors(__instance, toAdd);
    }

    /// <summary>
    /// Expands the TeamColors ScriptableObject used by TeamSelector to determine
    /// how many teams are available and their UI fill/border/saturated colors.
    /// This is the primary thing that controls how many distinct teams players can pick.
    /// Since TeamColors is a ScriptableObject, all references across the game
    /// (GameSessionHandler, AbilitySelectCircle, etc.) share the same instance —
    /// expanding it once covers all consumers.
    /// </summary>
    private static void ExpandTeamColors(CharacterSelectHandler handler, int toAdd)
    {
        // Find TeamColors via any TeamSelector component on the character select boxes
        var boxes = Traverse.Create(handler).Field<CharacterSelectBox[]>("characterSelectBoxes").Value;
        if (boxes == null || boxes.Length == 0)
        {
            Plugin.Log.LogWarning("characterSelectBoxes is null/empty — cannot expand TeamColors.");
            return;
        }

        TeamColors? teamColorsObj = null;
        foreach (var box in boxes)
        {
            if (box == null) continue;
            var selector = box.GetComponentInChildren<TeamSelector>(includeInactive: true);
            if (selector != null && selector.teams != null)
            {
                teamColorsObj = selector.teams;
                break;
            }
        }

        if (teamColorsObj == null)
        {
            Plugin.Log.LogWarning("Could not find TeamColors via TeamSelector — cannot expand teams.");
            return;
        }

        var existing = teamColorsObj.teamColors;
        if (existing == null || existing.Length == 0)
        {
            Plugin.Log.LogWarning("TeamColors.teamColors array is null/empty.");
            return;
        }

        Plugin.LogDiag($"Vanilla TeamColors has {existing.Length} teams. Adding {toAdd} extras.");

        var expanded = new TeamColor[existing.Length + toAdd];
        System.Array.Copy(existing, expanded, existing.Length);

        for (var i = 0; i < toAdd; i++)
        {
            var def = ExtraColors[i];
            var newIndex = existing.Length + i;

            // Generate fill, border, and saturated variants from the base color
            var fill = def.Color;
            var border = DarkenColor(def.Color, 0.5f);
            var saturated = SaturateColor(def.Color, 1.3f);

            expanded[newIndex] = new TeamColor
            {
                team = newIndex,
                fill = fill,
                border = border,
                saturated = saturated,
            };

            Plugin.LogDiag($"  Team[{newIndex}] {def.Name}: fill=({fill.r:F2},{fill.g:F2},{fill.b:F2}) border=({border.r:F2},{border.g:F2},{border.b:F2})");
        }

        // Write back — since it's a ScriptableObject, all references update automatically
        teamColorsObj.teamColors = expanded;

        Plugin.LogDiag($"TeamColors expanded to {teamColorsObj.Length} entries.");
    }

    /// <summary>
    /// Darkens a color by multiplying RGB channels, keeping alpha at 1.
    /// Used to generate border colors from fill colors.
    /// </summary>
    private static Color DarkenColor(Color c, float factor)
    {
        return new Color(
            Mathf.Clamp01(c.r * factor),
            Mathf.Clamp01(c.g * factor),
            Mathf.Clamp01(c.b * factor),
            1f);
    }

    /// <summary>
    /// Boosts saturation of a color. Used for the "saturated" variant of team colors.
    /// </summary>
    private static Color SaturateColor(Color c, float factor)
    {
        Color.RGBToHSV(c, out var h, out var s, out var v);
        s = Mathf.Clamp01(s * factor);
        v = Mathf.Clamp01(v * 1.1f);
        var result = Color.HSVToRGB(h, s, v);
        result.a = 1f;
        return result;
    }

    private readonly struct ExtraColorDef
    {
        public readonly string Name;
        public readonly Color Color;

        public ExtraColorDef(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }
}
