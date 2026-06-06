using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
public class CPHInline
{
	private static HttpClient _http;

	#region init
	// Init variables before run
	public void Init()
    {
        if (_http == null)
        {
            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }
        _http.DefaultRequestHeaders.Clear();
    }

	// Dispose variables after run
    public void Dispose()
    {
        _http?.Dispose();
    }
	#endregion

    #region execute
	public bool Execute()
	{
        // Request from WebSocket
        if (!args.ContainsKey("data")) 
        {
            return true;
        }

        // Save the options on StreamerBot
        JObject root = JObject.Parse(args["data"].ToString());
        SaveOptions(root);

        // Load the new options
        CPH.RunAction("SPOTIFYBOT - Load configuration");

        // Send the response via websocket
        SendResponse(args, 200);

		return true;
	}
    #endregion

	#region command
    // Get valuable elements sent and save them to the configuration
    private void SaveOptions(JObject argsRoot)
    {
        string configuration = CPH.GetGlobalVar<string>("SPOTIFYBOT_configuration", true);
        JObject configurationRoot = string.IsNullOrEmpty(configuration) ? new() : JObject.Parse(configuration);

        // Song role restrictions
        bool SRRestriction = argsRoot["SR_restriction"]?.ToObject<bool>() ?? false;
        configurationRoot["SR_restriction"] = SRRestriction;
        if (SRRestriction) {
            configurationRoot["SR_restriction_list"] = argsRoot["SR_restriction_list"];
        }

        // Max requests
        bool maxRequests = argsRoot["SR_maxuser"]?.ToObject<bool>() ?? false;
        configurationRoot["SR_maxuser"] = maxRequests;
        if (maxRequests) {
            configurationRoot["SR_maxuser_number"] = argsRoot["SR_maxuser_number"];
        }

        // Skip songs
        bool skipSongs = argsRoot["SR_skip"]?.ToObject<bool>() ?? false;
        configurationRoot["SR_skip"] = skipSongs;
        if (skipSongs) {
            configurationRoot["SR_skip_number"] = argsRoot["SR_skip_number"];
        }

        // Song length
        bool songLength = argsRoot["SR_length"]?.ToObject<bool>() ?? false;
        configurationRoot["SR_length"] = songLength;
        if (songLength) {
            configurationRoot["SR_length"] = argsRoot["SR_length"];
        }

        // Spotify links
        bool spotifyLinks = argsRoot["SR_spotifylink"]?.ToObject<bool>() ?? false;
        configurationRoot["SR_spotifylink"] = spotifyLinks;
        
        string json = configurationRoot.ToString(Newtonsoft.Json.Formatting.None);
        CPH.SetGlobalVar("SPOTIFYBOT_configuration", json, true);
    }

    // Send the response via websocket
    private void SendResponse(Dictionary<string, object> args, int status, string message = "")
    {
        // Build response
        var response = new Dictionary<string, object>
        {
            ["requestID"] = args.ContainsKey("runningActionId") ? args["runningActionId"] : null,
            ["status"] = status,
        };

        // If error, add error message to the request
        if (!string.IsNullOrEmpty(message))
        {
            response["message"] = message;
        }

        string responseJson = JsonConvert.SerializeObject(response);

        // Send back through websocket
        CPH.WebsocketBroadcastJson(responseJson);
    }
    #endregion
}