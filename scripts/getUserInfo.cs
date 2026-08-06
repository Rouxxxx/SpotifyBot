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
    private static string RedirectUri = "http://127.0.0.1:4067/auth";

    #region execute
    public bool Execute()
    {
        // Request can only be from WebSocket
        if (!args.ContainsKey("data")) 
        {
            return true;
        }

        var (accessToken, expiresAt, message) = InitAPI();

        // If error with access token, return error 500 (Internal Server error)
        if (string.IsNullOrEmpty(accessToken))
        {
            SendResponse(args, 403, null, message);
            return true;
        }

        // Init variables
        int status = 0;
        string username = string.Empty;
        string pfp = string.Empty;

        // Fetch user info from API
        Task.Run(async () =>
        {
            try
            {
                (status, username, pfp) = await GetUserInfo(accessToken);
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();

        // If fail, return empty response
        if (status != 200)
        {
            SendResponse(args, status, null);
            return true;
        } 

        // Build and send data through WebSocket
        object data = BuildData(username, pfp);
        SendResponse(args, 200, data);
        
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
    private object BuildData(string username, string pfp)
    {
        var data = new Dictionary<string, object>{};

        // Add data if its not null
        if (!string.IsNullOrEmpty(username))
        {
            data["username"] = username;
        }
        // If player is available, get current active device
        if (!string.IsNullOrEmpty(pfp))
        {
            data["pfp"] = pfp;
        }

        return data;
    }

    // Send the response via websocket
    private void SendResponse(Dictionary<string, object> args, int status, object data, string message = "")
    {
        // Build response
        var response = new Dictionary<string, object>
        {
            ["requestID"] = args.ContainsKey("runningActionId") ? args["runningActionId"] : null,
            ["status"] = status,
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
    private async Task<(int status, string username, string pfp)> GetUserInfo(string accessToken)
    {
        // Send the request to API
        string URI = "https://api.spotify.com/v1/me/";

        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);
        if (status != 200 || string.IsNullOrEmpty(json))
        {
            return (status, string.Empty, string.Empty);
        }

        JObject root = JObject.Parse(json);
        string? username = root?["display_name"]?.ToString();
        string? pfp = root?["images"]?[0]?["url"]?.ToString();

        return (status, username, pfp);
    }
    #endregion
}