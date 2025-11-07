using System.Text.RegularExpressions;
using KianaBH.Configuration;
using KianaBH.Data.Models.Dispatch;
using KianaBH.Util;
using KianaBH.Util.Crypto;
using Microsoft.AspNetCore.Mvc;

namespace KianaBH.SdkServer.Handlers.Dispatch;

[ApiController]
public class QueryGatewayController : ControllerBase
{
    [HttpGet("/query_gateway")]
    public async Task<IActionResult> QueryGateway([FromQuery] DispatchQuery query, Logger logger)
    {
        var version = HotfixContainer.ExtractVersionNumber(query.Version);
        var hotfix_version = query.Version!;

        if (!ConfigManager.Hotfix.Hotfixes.TryGetValue(hotfix_version, out var hotfix))
        {
            if (ConfigManager.Hotfix.AesKeys.TryGetValue(version, out var aesKey))
            {
                var parts = hotfix_version.Split('_');
                var region = string.Join('_', parts.SkipWhile(p => char.IsDigit(p[0])));

                var domainMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "gf_pc_beta", "outer-dp-beta-release.bh3.com" },
                    { "os_pc", "outer-dp-overseas01.honkaiimpact3.com" },
                    { "global_pc", "outer-dp-usa01.honkaiimpact3.com" },
                    { "gf_pc", "outer-dp-pc01.bh3.com" },
                    { "jp_pc", "outer-dp-jp01.honkaiimpact3.com" },
                    { "kr_pc", "outer-dp-kr01.honkaiimpact3.com" },
                    { "tw_pc", "outer-dp-asia01.honkaiimpact3.com" }
                };

                if (!domainMap.TryGetValue(region, out var domain))
                {
                    logger.Warn($"[AUTO-HOTFIX] Unknown region '{region}' for version {hotfix_version}");
                    return BadRequest();
                }

                var hotfixUrl = $"https://proxy1.neonteam.dev/{domain}/query_gameserver?version={hotfix_version}&t={query.Timestamp}&uid={query.Uid}&token={query.Token}";

                using var http = new HttpClient();
                try
                {
                    var httpResponse = await http.GetAsync(hotfixUrl);
                    if (!httpResponse.IsSuccessStatusCode)
                    {
                        logger.Warn($"[AUTO-HOTFIX] Failed to fetch hotfix from {hotfixUrl}: {httpResponse.StatusCode}");
                        return BadRequest();
                    }

                    var base64 = (await httpResponse.Content.ReadAsStringAsync()).Trim();

                    string? decryptedText = null;
                    try
                    {
                        decryptedText = DispatchEncryption.DecryptDispatchContent(version, base64);
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"[AUTO-HOTFIX] Decrypt error: {ex.Message}");
                    }

                    ConfigManager.SaveHotfixData(hotfix_version, decryptedText!);

                    if (!ConfigManager.Hotfix.Hotfixes.TryGetValue(hotfix_version, out hotfix))
                    {
                        logger.Warn($"[AUTO-HOTFIX] Failed to retrieve hotfix after saving for version {hotfix_version}");
                        return BadRequest();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error($"[AUTO-HOTFIX] Exception while fetching hotfix: {ex.Message}");
                    return BadRequest();
                }
            }
            else
            {
                logger.Warn($"Client sent requesting unsupported game version: {hotfix_version}");
                return BadRequest();
            }
        }

        var serverInfo = new QueryGatewayResponse.ServerInfo
        {
            Ip = ConfigManager.Config.GameServer.PublicAddress,
            Port = ConfigManager.Config.GameServer.Port,
            IsKcp = true,
        };

        var assetBundleUrlList = UrlProvider.GetAssetBundleUrlList(query.Version!);
        var exResourceUrlList = UrlProvider.GetExResourceUrlList(query.Version!);
        var exAudioAndVideoUrlList = UrlProvider.GetExAudioAndVideoUrlList(query.Version!);

        var response = new QueryGatewayResponse
        {
            AccountUrl = $"{ConfigManager.Config.HttpServer.GetDisplayAddress()}/",
            Gameserver = serverInfo,
            Gateway = serverInfo,
            AssetBundleUrlList = assetBundleUrlList,
            ExResourceUrlList = exResourceUrlList,
            ExAudioAndVideoUrlList = exAudioAndVideoUrlList,
            Manifest = hotfix,
            Ext = new Dictionary<string, object>
            {
                { "ex_res_use_http", "0" },
                { "is_xxxx", "0" },
                { "elevator_model_path", "GameEntry/EVA/StartLoading_Model" },
                { "block_error_dialog", "1" },
                { "ex_res_pre_publish", "0" },
                { "ex_resource_url_list", exResourceUrlList },
                { "apm_switch_game_log", "1" },
                { "ex_audio_and_video_url_list", exAudioAndVideoUrlList },
                { "apm_log_dest", "2" },
                { "update_streaming_asb", "1" },
                { "use_multy_cdn", "1" },
                { "show_bulletin_empty_dialog_bg", "0" },
                { "ai_use_asset_boundle", "1" },
                { "res_use_asset_boundle", "1" },
                { "apm_log_level", "0" },
                { "apm_switch_crash", "1" },
                { "network_feedback_enable", "0" },
                { "new_audio_upload", "1" },
                { "apm_switch", "1" }
            }
        };

        return Ok(DispatchEncryption.EncryptDispatchContent(version, response));
    }
}

public static partial class UrlProvider
{
    [GeneratedRegex("^(.*?)_(os|gf|global)_(.*?)$")]
    private static partial Regex VersionRegex();

    public static List<string> GetAssetBundleUrlList(string version)
    {
        var match = VersionRegex().Match(version);
        if (!match.Success) return [];

        var type = match.Groups[2].Value;

        if (ConfigManager.Hotfix.UseLocalCache) return GetLocalUrlList(type, version);

        return type switch
        {
            "os" =>
            [
                "https://autopatchos.honkaiimpact3.com/asset_bundle/overseas01/1.1",
                "https://bundle-aliyun-os.honkaiimpact3.com/asset_bundle/overseas01/1.1"
            ],
            "gf" when version.Contains("beta") =>
            [
                "https://autopatchbeta.bh3.com/asset_bundle/beta_release/1.0",
                "https://bh3rd-beta.bh3.com/asset_bundle/beta_release/1.0"
            ],
            "gf" =>
            [
                "https://autopatchcn.bh3.com/asset_bundle/hun02/1.0",
                "https://bundle.bh3.com/asset_bundle/hun02/1.0"
            ],
            "global" =>
            [
                "https://autopatchglb.honkaiimpact3.com/asset_bundle/usa01/1.1",
                "http://bundle-aliyun-usa.honkaiimpact3.com/asset_bundle/usa01/1.1"
            ],
            "jp" =>
            [
                "https://autopatchjp.honkaiimpact3.com/asset_bundle/jp01/1.1",
                "https://bundle-aliyun-jp.honkaiimpact3.com/asset_bundle/jp01/1.1"
            ],
            "kr" =>
            [
                "https://autopatchkr.honkaiimpact3.com/asset_bundle/kr01/1.1",
                "https://bundle-aliyun-kr.honkaiimpact3.com/asset_bundle/kr01/1.1"
            ],
            _ =>
            [
                "https://autopatchos.honkaiimpact3.com/asset_bundle/overseas01/1.1",
                "https://bundle-aliyun-os.honkaiimpact3.com/asset_bundle/overseas01/1.1"
            ]
        };
    }

    public static List<string> GetExAudioAndVideoUrlList(string version)
    {
        var match = VersionRegex().Match(version);
        if (!match.Success) return [];

        var type = match.Groups[2].Value;

        if (ConfigManager.Hotfix.UseLocalCache) return GetLocalUrlList(type, version);

        return type switch
        {
            "os" =>
            [
                "autopatchos.honkaiimpact3.com/com.miHoYo.bh3oversea",
                "bigfile-aliyun-os.honkaiimpact3.com/com.miHoYo.bh3oversea",
            ],
            "gf" when version.Contains("beta") =>
            [
                "autopatchbeta.bh3.com/tmp/CGAudio",
                "bh3rd-beta.bh3.com/tmp/CGAudio"
            ],
            _ =>
            [
                "bh3rd-beta-qcloud.bh3.com/tmp/CGAudio",
                "bh3rd-beta.bh3.com/tmp/CGAudio",
            ]
        };
    }

    public static List<string> GetExResourceUrlList(string version)
    {
        var match = VersionRegex().Match(version);
        if (!match.Success) return [];

        var type = match.Groups[2].Value;

        if (ConfigManager.Hotfix.UseLocalCache) return GetLocalUrlList(type, version);

        return type switch
        {
            "os" =>
            [
                "autopatchos.honkaiimpact3.com/com.miHoYo.bh3oversea",
                "bigfile-aliyun-os.honkaiimpact3.com/com.miHoYo.bh3oversea"
            ],
            "gf" when version.Contains("beta") =>
            [
                "autopatchbeta.bh3.com/tmp/beta",
                "bh3rd-beta.bh3.com/tmp/beta",
            ],
            "gf" =>
            [
                "autopatchcn.bh3.com/tmp/Original",
                "bundle.bh3.com/tmp/Original",
            ],
            "global" =>
            [
                "autopatchglb.honkaiimpact3.com/tmp/com.miHoYo.bh3global",
                "bigfile-aliyun-usa.honkaiimpact3.com/tmp/com.miHoYo.bh3global"
            ],
            "jp" =>
            [
                "autopatchjp.honkaiimpact3.com/tmp/com.miHoYo.bh3rdJP",
                "bigfile-aliyun-jp.honkaiimpact3.com/tmp/com.miHoYo.bh3rdJP"
            ],
            "kr" =>
            [
                "autopatchkr.honkaiimpact3.com/com.miHoYo.bh3korea",
                "bigfile-aliyun-kr.honkaiimpact3.com/com.miHoYo.bh3korea"
            ],
            _ =>
            [
                "autopatchos.honkaiimpact3.com/com.miHoYo.bh3oversea",
                "bigfile-aliyun-os.honkaiimpact3.com/com.miHoYo.bh3oversea"
            ]
        };
    }

    private static List<string> GetLocalUrlList(string type, string version)
    {
        var formattedVersion = version.Replace(".", "_");
        var baseUrl =
            $"{ConfigManager.Config.HttpServer.GetDisplayAddress()}/statics/{type}/{formattedVersion}";
        return [baseUrl, baseUrl];
    }
}