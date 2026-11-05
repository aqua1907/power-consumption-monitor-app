namespace PowerMonitor.Core.Wow;

public static class WowConfigPatcher
{
    /// <summary>
    /// Low-power CVars for WoW: Forever while the house is on battery.
    /// graphicsQuality / RAIDgraphicsQuality are UI presets (1 ≈ Low).
    /// </summary>
    public static IReadOnlyDictionary<string, string> BackupCvars { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["maxFPS"] = "40",
            ["maxFPSBk"] = "20",
            ["vsync"] = "1",
            ["graphicsQuality"] = "1",
            ["RAIDgraphicsQuality"] = "1",
            ["graphicsViewDistance"] = "1",
            ["raidGraphicsViewDistance"] = "1",
            ["farclip"] = "400",
            ["RAIDfarclip"] = "400",
            ["horizonClip"] = "400",
            ["RAIDhorizonClip"] = "400",
            ["horizonStart"] = "100",
            ["RAIDhorizonStart"] = "100",
            ["graphicsEnvironmentDetail"] = "1",
            ["raidGraphicsEnvironmentDetail"] = "1",
            ["graphicsGroundClutter"] = "1",
            ["raidGraphicsGroundClutter"] = "1",
            ["groundEffectDensity"] = "16",
            ["RAIDgroundEffectDensity"] = "16",
            ["groundEffectDist"] = "70",
            ["RAIDgroundEffectDist"] = "70",
            ["graphicsParticleDensity"] = "1",
            ["raidGraphicsParticleDensity"] = "1",
            ["particleDensity"] = "20",
            ["RAIDparticleDensity"] = "20",
            ["graphicsShadowQuality"] = "0",
            ["raidGraphicsShadowQuality"] = "0",
            ["shadowMode"] = "0",
            ["RAIDshadowMode"] = "0",
            ["shadowTextureSize"] = "1024",
            ["RAIDshadowTextureSize"] = "1024",
            ["shadowNumCascades"] = "1",
            ["RAIDshadowNumCascades"] = "1",
            ["shadowSoft"] = "0",
            ["RAIDshadowSoft"] = "0",
            ["shadowBlendCascades"] = "0",
            ["RAIDshadowBlendCascades"] = "0",
            ["graphicsSSAO"] = "0",
            ["raidGraphicsSSAO"] = "0",
            ["SSAO"] = "0",
            ["RAIDSSAO"] = "0",
            ["sunShafts"] = "0",
            ["RAIDsunShafts"] = "0",
            ["graphicsDepthEffects"] = "0",
            ["raidGraphicsDepthEffects"] = "0",
            ["graphicsComputeEffects"] = "0",
            ["raidGraphicsComputeEffects"] = "0",
            ["giQuality"] = "0",
            ["RAIDgiQuality"] = "0",
            ["waterDetail"] = "0",
            ["RAIDWaterDetail"] = "0",
            ["graphicsLiquidDetail"] = "0",
            ["raidGraphicsLiquidDetail"] = "0",
            ["graphicsPBRLiquidDetail"] = "0",
            ["raidGraphicsPBRLiquidDetail"] = "0",
            ["pbrLiquidDetail"] = "0",
            ["RAIDpbrLiquidDetail"] = "0",
            ["rippleDetail"] = "0",
            ["RAIDrippleDetail"] = "0",
            ["refraction"] = "0",
            ["RAIDrefraction"] = "0",
            ["reflectionMode"] = "0",
            ["RAIDreflectionMode"] = "0",
            ["weatherDensity"] = "0",
            ["RAIDweatherDensity"] = "0",
            ["graphicsSpellDensity"] = "0",
            ["raidGraphicsSpellDensity"] = "0",
            ["spellClutter"] = "0",
            ["spellVisualDensityFilterSetting"] = "0",
            ["graphicsOutlineMode"] = "0",
            ["raidGraphicsOutlineMode"] = "0",
            ["OutlineEngineMode"] = "0",
            ["RAIDOutlineEngineMode"] = "0",
            ["graphicsProjectedTextures"] = "0",
            ["raidGraphicsProjectedTextures"] = "0",
            ["projectedTextures"] = "0",
            ["RAIDprojectedTextures"] = "0",
            ["graphicsLightMode"] = "0",
            ["raidGraphicsLightMode"] = "0",
            ["graphicsTextureResolution"] = "0",
            ["raidGraphicsTextureResolution"] = "0",
            ["terrainLodDist"] = "200",
            ["RAIDterrainLodDist"] = "200",
            ["wmoLodDist"] = "200",
            ["RAIDwmoLodDist"] = "200",
            ["lodObjectCullSize"] = "30",
            ["RAIDlodObjectCullSize"] = "30",
            ["entityShadowFadeScale"] = "50",
            ["RAIDentityShadowFadeScale"] = "50"
        };

    public static void Snapshot(string configPath, string snapshotPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException("WoW Config.wtf was not found.", configPath);

        var directory = Path.GetDirectoryName(snapshotPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.Copy(configPath, snapshotPath, overwrite: true);
    }

    /// <summary>
    /// Saves the current Config.wtf as the grid snapshot unless it already looks like the backup preset.
    /// </summary>
    public static void SnapshotIfQuality(string configPath, string snapshotPath)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException("WoW Config.wtf was not found.", configPath);

        if (LooksLikeBackup(configPath) && File.Exists(snapshotPath))
            return;

        Snapshot(configPath, snapshotPath);
    }

    public static bool LooksLikeBackup(string configPath)
    {
        foreach (var line in File.ReadLines(configPath))
        {
            if (!TryParse(line, out var key, out var value))
                continue;
            if (!key.Equals("graphicsQuality", StringComparison.OrdinalIgnoreCase))
                continue;
            return value == "1";
        }

        return false;
    }

    public static void RestoreSnapshot(string configPath, string snapshotPath)
    {
        if (!File.Exists(snapshotPath))
            throw new FileNotFoundException("Saved grid graphics snapshot was not found.", snapshotPath);

        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.Copy(snapshotPath, configPath, overwrite: true);
    }

    public static void ApplyCvars(string configPath, IReadOnlyDictionary<string, string> cvars)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException("WoW Config.wtf was not found.", configPath);

        var lines = File.ReadAllLines(configPath).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Count; i++)
        {
            if (!TryParse(lines[i], out var key, out _))
                continue;
            if (!cvars.TryGetValue(key, out var value))
                continue;

            lines[i] = Format(key, value);
            seen.Add(key);
        }

        foreach (var (key, value) in cvars)
        {
            if (seen.Contains(key))
                continue;
            lines.Add(Format(key, value));
        }

        var temporary = configPath + ".tmp";
        File.WriteAllLines(temporary, lines);
        File.Move(temporary, configPath, overwrite: true);
    }

    public static bool TryParse(string line, out string key, out string value)
    {
        key = "";
        value = "";
        var trimmed = line.Trim();
        if (!trimmed.StartsWith("SET ", StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = trimmed[4..].Trim();
        var space = rest.IndexOf(' ');
        if (space <= 0)
            return false;

        key = rest[..space];
        value = rest[(space + 1)..].Trim().Trim('"');
        return key.Length > 0;
    }

    public static string Format(string key, string value) => $"SET {key} \"{value}\"";
}
