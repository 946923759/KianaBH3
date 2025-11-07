using KianaBH.Configuration;
using KianaBH.Internationalization;
using Newtonsoft.Json;
using KianaBH.Util.Extensions;
using System.Text.Json;

namespace KianaBH.Util;

public static class ConfigManager
{
    public static readonly Logger Logger = new("ConfigManager");
    public static ConfigContainer Config { get; private set; } = new();
    private static readonly string ConfigFilePath = Config.Path.ConfigPath + "/Config.json";
    public static HotfixContainer Hotfix { get; private set; } = new();
    private static readonly string HotfixFilePath = Config.Path.ConfigPath + "/Hotfix.json";

    public static void LoadConfig()
    {
        LoadConfigData();
        LoadHotfixData();
    }

    private static void LoadConfigData()
    {
        var file = new FileInfo(ConfigFilePath);
        if (!file.Exists)
        {
            Config = new()
            {
                ServerOption =
                {
                    Language = Extensions.Extensions.GetCurrentLanguage()
                }
            };

            Logger.Info("Current Language is " + Config.ServerOption.Language);
            SaveData(Config, ConfigFilePath);
        }

        using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            var json = reader.ReadToEnd();
            Config = JsonConvert.DeserializeObject<ConfigContainer>(json)!;
        }

        SaveData(Config, ConfigFilePath);
    }

    private static void LoadHotfixData()
    {
        var file = new FileInfo(HotfixFilePath);

        // Generate all necessary versions
        var verList = Extensions.Extensions.GetSupportVersions();

        Logger.Info(I18NManager.Translate("Server.ServerInfo.CurrentVersion",
            verList.Aggregate((current, next) => $"{current}, {next}")));

        if (!file.Exists)
        {
            Hotfix = new HotfixContainer();
            SaveData(Hotfix, HotfixFilePath);
            file.Refresh();
        }

        using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            var json = reader.ReadToEnd();
            Hotfix = JsonConvert.DeserializeObject<HotfixContainer>(json)!;
        }

        foreach (var version in verList)
            if (!Hotfix.Hotfixes.TryGetValue(version, out var _))
                Hotfix.Hotfixes[version] = new();

        SaveData(Hotfix, HotfixFilePath);
    }

    public static void SaveHotfixData(string version, string decryptedText)
    {
        LoadHotfixData();

        try
        {
            using var doc = JsonDocument.Parse(decryptedText);
            if (!doc.RootElement.TryGetProperty("manifest", out var manifestElement))
            {
                Logger.Warn($"[AUTO-HOTFIX] Manifest not found in decrypted hotfix for version {version}");
                return;
            }

            var manifestJson = manifestElement.GetRawText();
            var manifestData = System.Text.Json.JsonSerializer.Deserialize<HotfixManfiset>(manifestJson);

            if (manifestData == null)
            {
                Logger.Warn($"[AUTO-HOTFIX] Failed to parse manifest for version {version}");
                return;
            }

            Hotfix.Hotfixes[version] = manifestData;

            SaveData(Hotfix, HotfixFilePath);

            Logger.Info($"[AUTO-HOTFIX] Saved hotfix manifest for version {version}");
        }
        catch (Exception ex)
        {
            Logger.Error($"[AUTO-HOTFIX] Failed to save hotfix data: {ex.Message}");
        }
    }

    private static void SaveData(object data, string path)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using var writer = new StreamWriter(stream);
        writer.Write(json);
    }

    public static void InitDirectories()
    {
        foreach (var property in Config.Path.GetType().GetProperties())
        {
            var dir = property.GetValue(Config.Path)?.ToString();

            if (!string.IsNullOrEmpty(dir))
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
        }
    }
}