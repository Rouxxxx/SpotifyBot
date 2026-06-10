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
    private static JObject defaultOptions = new JObject
    {
        ["SR_restriction"] = false,
        ["SR_restriction_list"] = {},
        ["SR_length"] = false,
        ["SR_length_number"] = 300,

        ["SR_queue"] = true,
        ["SR_maxuser"] = true,
        ["SR_maxuser_number"] = 5,
        ["SR_skip"] = false,
        ["SR_skip_number"] = 5,

        ["SR_spotifylink"] = true,
        ["SR_youtubelink"] = true,
    };

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
        string mode = root["mode"]?.ToString();

        if (string.IsNullOrEmpty(mode))
        {
            SendResponseError(args, 404, "Mode isnt't specified");
        }

        if (mode == "set")
        {
            SaveOptions(root);
            // Load the new options
            CPH.RunAction("SPOTIFYBOT - Load configuration");
            SendResponse(args, 200, mode);
        }
        else
        {
            string data = CPH.GetGlobalVar<string>("SPOTIFYBOT_configuration", true);
            SendResponse(args, 200, mode, data);
        }
		return true;
	}
    #endregion

	#region command
    private static T GetOption<T>(JObject obj, string key)
    {
        JToken? token = obj.ContainsKey(key)
            ? obj[key]
            : defaultOptions[key];
        return token.ToObject<T>();
    }
    // Get valuable elements sent and save them to the configuration
    private void SaveOptions(JObject argsRoot)
    {
        string configuration = CPH.GetGlobalVar<string>("SPOTIFYBOT_configuration", true);
        JObject configurationRoot = string.IsNullOrEmpty(configuration) ? new() : JObject.Parse(configuration);

        // Song role restrictions
        bool SRRestriction = GetOption<bool>(argsRoot, "SR_restriction");
        configurationRoot["SR_restriction"] = SRRestriction;
        if (SRRestriction) {
            configurationRoot["SR_restriction_list"] = argsRoot["SR_restriction_list"];
        }
        
        // Song length
        bool songLength = GetOption<bool>(argsRoot, "SR_length");
        configurationRoot["SR_length"] = songLength;
        if (songLength) {
            int SR_length_number = GetOption<int>(argsRoot, "SR_length_number");
            configurationRoot["SR_length_number"] = SR_length_number;
        }

        // Queue system
        bool queue = GetOption<bool>(argsRoot, "SR_queue");
        configurationRoot["SR_queue"] = queue;

        // Max requests
        bool maxRequests = GetOption<bool>(argsRoot, "SR_maxuser");
        configurationRoot["SR_maxuser"] = maxRequests;
        if (maxRequests) {
            int SR_maxuser_number = GetOption<int>(argsRoot, "SR_maxuser_number");
            configurationRoot["SR_maxuser_number"] = SR_maxuser_number;
        }

        // Skip songs
        bool skipSongs = GetOption<bool>(argsRoot, "SR_skip");
        configurationRoot["SR_skip"] = skipSongs;
        if (skipSongs) {
            int SR_skip_number = GetOption<int>(argsRoot, "SR_skip_number");
            configurationRoot["SR_skip_number"] = SR_skip_number;
        }

        // Spotify links
        bool spotifyLinks = GetOption<bool>(argsRoot, "SR_spotifylink");
        configurationRoot["SR_spotifylink"] = spotifyLinks;

        // Youtube links
        bool youtubeLinks = GetOption<bool>(argsRoot, "SR_youtubelink");
        configurationRoot["SR_youtubelink"] = youtubeLinks;

        // Save configuration
        string json = configurationRoot.ToString(Newtonsoft.Json.Formatting.None);
        CPH.SetGlobalVar("SPOTIFYBOT_configuration", json, true);
    }

    // Send the response via websocket
    private void SendResponse(Dictionary<string, object> args, int status, string mode, string data = "")
    {
        // Build response
        var response = new Dictionary<string, object>
        {
            ["requestID"] = args.ContainsKey("runningActionId") ? args["runningActionId"] : null,
            ["status"] = status,
        };

        // If error, add error message to the request
        if (!string.IsNullOrEmpty(mode))
        {
            response["mode"] = mode;
        }
        // If error, add error message to the request
        if (!string.IsNullOrEmpty(data))
        {
            response["data"] = data;
        }

        string responseJson = JsonConvert.SerializeObject(response);

        // Send back through websocket
        CPH.WebsocketBroadcastJson(responseJson);
    }
    // Send the response via websocket
    private void SendResponseError(Dictionary<string, object> args, int status, string message)
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