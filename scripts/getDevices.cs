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

    #region execute
	public bool Execute()
	{
        // Request can only be from WebSocket
        if (!args.ContainsKey("data")) 
        {
            return true;
        }

        string URIDevices = "https://api.spotify.com/v1/me/player/devices";
        string URIPlayer = "https://api.spotify.com/v1/me/player";
        var (accessToken, expiresAt, message) = InitAPI();

        // If error with access token, return error 500 (Internal Server error)
        if (string.IsNullOrEmpty(accessToken))
        {
            SendResponse(args, 403, null, message);
            return true;
        }

        int statusDevices = 0;
        string jsonDevices = string.Empty;

        int statusPlayer = 0;
        string jsonPlayer = string.Empty;

        // Fetch connection status from API
        Task.Run(async () =>
        {
            try
            {
				(statusDevices, jsonDevices) = await ProcessAPIRequest(accessToken, URIDevices, HttpMethod.Get);
                (statusPlayer, jsonPlayer) = await ProcessAPIRequest(accessToken, URIPlayer, HttpMethod.Get);
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();

        object data = BuildData(statusDevices, statusPlayer, jsonDevices, jsonPlayer);

        // Send the response via websocket
        SendResponse(args, statusDevices, data);

		return true;
	}
    #endregion

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

	#region APIutils
    private (string accessToken, DateTime expiresAt, string message) InitAPI()
    {
        // Get relevant user credentials
        string accessToken = CPH.GetGlobalVar<string>("SPOTIFYBOT_accessToken", true);
        DateTime expiresAt = CPH.GetGlobalVar<DateTime>("SPOTIFYBOT_expiresAt", true);

        // Check if token info is null
        if (string.IsNullOrEmpty(accessToken))
        {
            // If both access token and refresh token are null, exit
            string refreshToken = CPH.GetGlobalVar<string>("SPOTIFYBOT_refreshToken", true);
            if (string.IsNullOrEmpty(refreshToken))
            {
                return (string.Empty, DateTime.Now, "Refresh Token doesn't exist");
            }

            // If only access token is null, refresh token
            CPH.RunAction("SPOTIFYBOT - Refresh Token");

            // Load new variables
            accessToken = CPH.GetGlobalVar<string>("SPOTIFYBOT_accessToken", true);
            expiresAt = CPH.GetGlobalVar<DateTime>("SPOTIFYBOT_expiresAt", true);

            if (string.IsNullOrEmpty(accessToken))
            {
                return (string.Empty, DateTime.Now, "Access Token empty after calling refresh (Access Token was empty)");
            }
            return (accessToken, expiresAt, string.Empty);
        }

        // If token expired, refresh them
        if (expiresAt <= DateTime.Now)
        {
            // If only refresh token is null, refresh token
            CPH.RunAction("SPOTIFYBOT - Refresh Token");

            // Load new variables
            accessToken = CPH.GetGlobalVar<string>("SPOTIFYBOT_accessToken", true);
            expiresAt = CPH.GetGlobalVar<DateTime>("SPOTIFYBOT_expiresAt", true);
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            return (string.Empty, DateTime.Now, "Access Token empty after calling refresh (Access Token was empty)");
        }
        return (accessToken, expiresAt, string.Empty);
    }

	// Send a HTTP request and parse the JSON body of the response
	private async Task<(int code, string json)> ProcessAPIRequest(string accessToken, string URI, HttpMethod method)
    {
        var request = new HttpRequestMessage(method, URI);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _http.SendAsync(request);
        int statusCode = (int)response.StatusCode;

        CPH.LogDebug($"HTTP Request: [{URI}] Reponse code: {statusCode}");

        // If error, just return code with empty json
        if (!response.IsSuccessStatusCode)
            return (statusCode, string.Empty);

        string json = await response.Content.ReadAsStringAsync();
        return (statusCode, json);
    }
    #endregion

	#region websocket
    // Build the data to send via websocket
    private object BuildData(int statusDevices, int statusPlayer, string jsonDevices = "", string jsonPlayer = "")
    {
        var data = new Dictionary<string, object>{};

        // If player is available, get current active device
        if (statusPlayer == 200)
        {
            JObject jsonPlayerObject = JObject.Parse(jsonPlayer);
            string activeDevice = jsonPlayerObject["device"]?["id"]?.ToString();
            data["active_device"] = activeDevice;
        }

        // If user has devices, get the list
        if (statusDevices == 200)
        {
            JObject jsonDevicesObject = JObject.Parse(jsonDevices);
            data["devices"] = jsonDevicesObject["devices"];
        };

        // If user already has a saved device in StreamerBot, get it
        string savedDevice = CPH.GetGlobalVar<string>("SPOTIFYBOT_savedDevice", true);
        if (!string.IsNullOrEmpty(savedDevice))
        {
            data["saved_device"] = savedDevice;
        };

        return data;
    }
    // Send the response via websocket
    private void SendResponse(Dictionary<string, object> args, int statusDevices, object data, string message = "")
    {
        // Build response
        var response = new Dictionary<string, object>
        {
            ["requestID"] = args.ContainsKey("runningActionId") ? args["runningActionId"] : null,
            ["status"] = statusDevices,
            ["data"] = data
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

    #region command
    // Get the song and artist name of the music currently playing (none if nothing is playing)
    private async Task<int> GetConnectionStatus(string accessToken)
    {
        // Send the request to API
        string URI = "https://api.spotify.com/v1/me/";

        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);

        return status;
    }
    #endregion
}