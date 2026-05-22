using HarmonyLib;
using UnityEngine;

namespace BoplMoreColors.Patches;

/// <summary>
/// Expands the game's PlayerColors palette with additional colors so each of 8 players
/// can have a unique team color. Patches CharacterSelectHandler.Awake (postfix) to inject
/// new PlayerColor entries after the vanilla ones are loaded.
/// </summary>
[HarmonyPatch(typeof(CharacterSelectHandler), "Awake")]
internal static class ColorExpansionPatch
{
    // Extra colors chosen to be visually distinct from vanilla palette.
    // Vanilla typically has: blue, red, green, yellow, purple, grey/dark (~6-8).
    // These fill the gaps with hues the vanilla palette doesn't cover.
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

        var playerColorsObj = Traverse.Create(__instance).Field<PlayerColors>("playerColors").Value;
        if (playerColorsObj == null)
        {
            Plugin.Log.LogWarning("playerColors field is null on CharacterSelectHandler.");
            return;
        }

        var existingColors = playerColorsObj.playerColors;
        if (existingColors == null || existingColors.Length == 0)
        {
            Plugin.Log.LogWarning("playerColors.playerColors array is null/empty.");
            return;
        }

        // Clamp to available definitions
        var toAdd = Mathf.Min(extraCount, ExtraColors.Length);

        Plugin.LogDiag($"Vanilla palette has {existingColors.Length} colors. Adding {toAdd} extras.");

        // Clone a template material from the first existing color
        var templatePlayerMat = existingColors[0].playerMaterial;
        var templateUiMat = existingColors[0].uiMaterial;

        if (templatePlayerMat == null)
        {
            Plugin.Log.LogWarning("Template playerMaterial is null — cannot create extra colors.");
            return;
        }

        // Build expanded array
        var expanded = new PlayerColor[existingColors.Length + toAdd];
        System.Array.Copy(existingColors, expanded, existingColors.Length);

        for (var i = 0; i < toAdd; i++)
        {
            var def = ExtraColors[i];
            var newIndex = existingColors.Length + i;

            // Clone player material and set color
            var playerMat = new Material(templatePlayerMat);
            playerMat.name = $"PlayerMat_{def.Name}";
            SetMaterialColor(playerMat, def.Color);

            // Clone UI material and set color
            Material? uiMat = null;
            if (templateUiMat != null)
            {
                uiMat = new Material(templateUiMat);
                uiMat.name = $"UiMat_{def.Name}";
                SetMaterialColor(uiMat, def.Color);
            }

            expanded[newIndex] = new PlayerColor
            {
                colorIndex = newIndex,
                playerMaterial = playerMat,
                uiMaterial = uiMat!,
            };

            Plugin.LogDiag($"  [{newIndex}] {def.Name} = ({def.Color.r:F2}, {def.Color.g:F2}, {def.Color.b:F2})");
        }

        // Write expanded array back to the ScriptableObject
        playerColorsObj.playerColors = expanded;

        Plugin.LogDiag($"PlayerColors expanded to {playerColorsObj.Length} entries.");

        // Update every SelectColor component so the color picker sees the full palette
        UpdateSelectColorReferences(__instance, playerColorsObj);
    }

    /// <summary>
    /// Sets the color on a material. Tries common shader property names that Unity/Bopl Battle uses.
    /// </summary>
    private static void SetMaterialColor(Material mat, Color color)
    {
        // Try the standard Unity color property first
        if (mat.HasProperty("_Color"))
        {
            mat.color = color;
            return;
        }

        // Try other common property names
        string[] candidates = { "_BaseColor", "_TintColor", "_MainColor", "_PlayerColor" };
        foreach (var prop in candidates)
        {
            if (mat.HasProperty(prop))
            {
                mat.SetColor(prop, color);
                return;
            }
        }

        // Fallback: set .color anyway (works for most shaders even without _Color declared)
        mat.color = color;
        Plugin.LogDiag($"  Material '{mat.name}' has no known color property — used fallback .color setter.");
    }

    /// <summary>
    /// Finds all SelectColor components across character select boxes and updates their
    /// playerColors reference so the color picker cycles through the expanded palette.
    /// </summary>
    private static void UpdateSelectColorReferences(CharacterSelectHandler handler, PlayerColors playerColorsObj)
    {
        var boxes = Traverse.Create(handler).Field<CharacterSelectBox[]>("characterSelectBoxes").Value;
        if (boxes == null)
        {
            return;
        }

        var updated = 0;
        foreach (var box in boxes)
        {
            if (box == null)
            {
                continue;
            }

            var selectColors = box.GetComponentsInChildren<SelectColor>(includeInactive: true);
            foreach (var sc in selectColors)
            {
                sc.playerColors = playerColorsObj;
                updated++;
            }
        }

        Plugin.LogDiag($"Updated {updated} SelectColor component(s) with expanded palette.");
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
