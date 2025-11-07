using KianaBH.Configuration;
using KianaBH.Data.Models.Dispatch;
using KianaBH.Util;
using KianaBH.Util.Crypto;
using Microsoft.AspNetCore.Mvc;

namespace KianaBH.SdkServer.Handlers.Dispatch;

[ApiController]
public class QueryDispatchController : ControllerBase
{
    [HttpGet("/query_dispatch")]
    public IActionResult QueryDispatch([FromQuery] DispatchQuery query, Logger logger)
    {
        var version = HotfixContainer.ExtractVersionNumber(query.Version);
        var hotfix_version = query.Version!;
        //if (!ConfigManager.Hotfix.Hotfixes.ContainsKey(hotfix_version))
        //{
        //    logger.Warn($"Client sent requesting unsupported game version: {hotfix_version}");
        //    return BadRequest();
        //}

        var response = new QueryDispatchResponse
        {
            Retcode = 0,
            RegionList =
            [
                new QueryDispatchResponse.RegionInfo
                {
                    Retcode = 0,
                    DispatchUrl =
                        $"{ConfigManager.Config.HttpServer.GetDisplayAddress()}/query_gateway",
                    Ext = null,
                    Name = "KianaBH",
                    Title = "KianaBH",
                }
            ]
        };

        return Ok(DispatchEncryption.EncryptDispatchContent(version, response));
    }
}