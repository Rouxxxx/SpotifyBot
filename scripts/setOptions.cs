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

        JObject root = JObject.Parse(args["data"].ToString());
        SaveOptions(root);

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
        bool SRRestriction = argsRoot["songrequest_restriction"]?.ToObject<bool>() ?? false;
        configurationRoot["songrequest_restriction"] = SRRestriction;
        if (SRRestriction) {
            configurationRoot["songrequest_restriction_list"] = argsRoot["songrequest_restriction_list"];
        }

        // Max requests
        bool maxRequests = argsRoot["max_requests"]?.ToObject<bool>() ?? false;
        configurationRoot["max_requests"] = maxRequests;
        if (maxRequests) {
            configurationRoot["max_requests_number"] = argsRoot["max_requests_number"];
        }

        // Skip songs
        bool skipSongs = argsRoot["skip_songs"]?.ToObject<bool>() ?? false;
        configurationRoot["skip_songs"] = skipSongs;
        if (maxRequests) {
            configurationRoot["skip_songs_number"] = argsRoot["skip_songs_number"];
        }

        // Song length
        bool songLength = argsRoot["song_length"]?.ToObject<bool>() ?? false;
        configurationRoot["song_length"] = songLength;
        if (maxRequests) {
            configurationRoot["song_length_number"] = argsRoot["song_length_number"];
        }

        // Spotify links
        bool spotifyLinks = argsRoot["songrequest_spotifylink"]?.ToObject<bool>() ?? false;
        configurationRoot["songrequest_spotifylink"] = spotifyLinks;
        
        string json = configurationRoot.ToString(Newtonsoft.Json.Formatting.None);
        CPH.SetGlobalVar("SPOTIFYBOT_configuration", json, true);
    }

    // Send the response via websocket
    private void SendResponse(Dictionary<string, object> args, int status, string message = "")
    {
        string clientID = CPH.GetGlobalVar<string>("SPOTIFYBOT_clientID", true);
        string clientSecret = CPH.GetGlobalVar<string>("SPOTIFYBOT_clientSecret", true);
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