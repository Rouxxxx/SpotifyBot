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
        // Request from WebSocket
        if (args.ContainsKey("data")) 
        {
            HandleWebSocket();
        }
        // Request from StreamerBot
        else {
            HandleTrigger();
        }
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

    #region trigger
    private void HandleTrigger()
    {
        // Get relevant user credentials
        string clientID = CPH.GetGlobalVar<string>("SPOTIFYBOT_clientID", true);
        string clientSecret = CPH.GetGlobalVar<string>("SPOTIFYBOT_clientSecret", true);

        // If one of the credentials is empty, abort
        if (string.IsNullOrEmpty(clientID) || string.IsNullOrEmpty(clientSecret)) {
            // Create credentials variables
            if (string.IsNullOrEmpty(clientID))
            {
                CPH.SetGlobalVar("SPOTIFYBOT_clientID", "CHANGE_ME", true);
            }
            if (string.IsNullOrEmpty(clientSecret))
            {
                CPH.SetGlobalVar("SPOTIFYBOT_clientSecret", "CHANGE_ME", true);
            }
            return;
        }

        string refreshToken = string.Empty;
        string accessToken = string.Empty;
        DateTime expiresAt = DateTime.Now;

        Task.Run(async () =>
        {
            try
            {
                // Get code for authentication
				string code = await StartAuthFlow(clientID, clientSecret);
                // Authenticate on the app
				(refreshToken, accessToken, expiresAt) = await ExchangeCode(clientID, clientSecret, code);
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();

        // Error handling
        if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(accessToken))
        {
            CPH.SendMessage("ERROR - Refresh Token or Access Token is empty");
            return;
        }

        CPH.SetGlobalVar("SPOTIFYBOT_refreshToken", refreshToken, true);
        CPH.SetGlobalVar("SPOTIFYBOT_accessToken", accessToken, true);
        CPH.SetGlobalVar("SPOTIFYBOT_expiresAt", expiresAt, true);

        SetMaxSongs();
        CPH.LogDebug($"Logged in via Trigger");
    }
    #endregion

	#region APIutils
	// Send a HTTP request and parse the JSON body of the response
	private async Task<(int code, string json)> ProcessAPIAuthRequest(HttpRequestMessage request)
	{
		// Send the request to Spotify API
		var response = await _http.SendAsync(request);
		int statusCode = (int)response.StatusCode;

		// If error, just return code with empty json
		if (!response.IsSuccessStatusCode)
			return (statusCode, string.Empty);

		// Parse JSON result
		string json = await response.Content.ReadAsStringAsync();
		return (statusCode, json);
	}

	// Add client infos to the request headers
	private void AddClientHeader(string clientID, string clientSecret, HttpRequestMessage request)
	{
		// Add client infos to the header
		string authHeader = Convert.ToBase64String(
			Encoding.UTF8.GetBytes($"{clientID}:{clientSecret}"));
		request.Headers.Add("Authorization", $"Basic {authHeader}");
	}
	#endregion

    #region websocket
    // Handle authentication if it was initiated by WebSocket
    private void HandleWebSocket() {
        var (clientID, clientSecret) = WSGetCredentials(args);
        // If one credential is empty, abort
        if (string.IsNullOrEmpty(clientID) || string.IsNullOrEmpty(clientSecret))
        {
            WSSendResponse(args, 403, "clientID or clientSecret is empty");
            return;
        }
        // Set global variables
        CPH.SetGlobalVar("SPOTIFYBOT_clientID", clientID, true);
        CPH.SetGlobalVar("SPOTIFYBOT_clientSecret", clientSecret, true);

        string refreshToken = string.Empty;
        string accessToken = string.Empty;
        DateTime expiresAt = DateTime.Now;

        Task.Run(async () =>
        {
            try
            {
                // Get code for authentication
				string code = await StartAuthFlow(clientID, clientSecret);
                // Authenticate on the app
				(refreshToken, accessToken, expiresAt) = await ExchangeCode(clientID, clientSecret, code);
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();

        // Error handling
        if (string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(accessToken))
        {
            WSSendResponse(args, 403, "Refresh Token or Access Token is empty");
            return;
        }

        // Set global variables
        CPH.SetGlobalVar("SPOTIFYBOT_refreshToken", refreshToken, true);
        CPH.SetGlobalVar("SPOTIFYBOT_accessToken", accessToken, true);
        CPH.SetGlobalVar("SPOTIFYBOT_expiresAt", expiresAt, true);

        SetMaxSongs();

        // Send response to WebSocket
        WSSendResponse(args, 200);

        CPH.LogDebug($"Logged in via WebSocket");
    }
    // Get credentials stored in data
    private (string clientID, string clientSecret) WSGetCredentials(Dictionary<string, object> args)
    {
        JObject root = JObject.Parse(args["data"].ToString());
        string clientID = root["clientID"]?.ToString();
        string clientSecret = root["clientSecret"]?.ToString();

        return (clientID, clientSecret);
    }
    // Send the response via websocket
    private void WSSendResponse(Dictionary<string, object> args, int status, string message = "")
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

    #region command
    private void SetMaxSongs()
    {
        // Add maxNb variable if it doesn't exist
        int maxNumberOfUserSongs = CPH.GetGlobalVar<int>("SPOTIFYBOT_maxNumberOfUserSongs", true);
        if (maxNumberOfUserSongs == 0) {
            CPH.SetGlobalVar("SPOTIFYBOT_maxNumberOfUserSongs", 5, true);
        }
    }
    // Get the auth code necessary for auth
    private async Task<string> StartAuthFlow(string clientID, string clientSecret)
    {
        string scope = "user-modify-playback-state user-read-playback-state";

        string url =
            "https://accounts.spotify.com/authorize" +
            $"?client_id={clientID}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&scope={Uri.EscapeDataString(scope)}";

        var code = string.Empty;
        try
        {
            using var listener = new HttpListener();

            listener.Prefixes.Add($"{RedirectUri}/");
            listener.Start();

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });

            var context = await listener.GetContextAsync();
            code = context.Request.QueryString["code"];

            string responseString = "You can close this window.";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);

            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            CPH.LogError($"Spotify auth failed: {ex}");
        }

        return code;
    }

    // Authenticate using the client information
    private async Task<(string refreshToken, string accessToken, DateTime expiresAt)> ExchangeCode(string clientID, string clientSecret, string code)
    {
        // Error handling
        if (string.IsNullOrEmpty(code))
        {
			return (string.Empty, string.Empty, DateTime.Now);
        }

        // Build the auth request
        var request = new HttpRequestMessage(HttpMethod.Post,
            "https://accounts.spotify.com/api/token");
        var body = new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", RedirectUri }
        };
        request.Content = new FormUrlEncodedContent(body);
        AddClientHeader(clientID, clientSecret, request);

        // Error handling
        var (status, json) = await ProcessAPIAuthRequest(request);

        CPH.LogDebug($"HTTP Request: [https://accounts.spotify.com/api/token] Reponse code: {status}");

        if (status != 200)
        {
			return (string.Empty, string.Empty, DateTime.Now);
        }

        // Parse the JSON response
        JObject root = JObject.Parse(json);
        if (root == null)
        {
            return (string.Empty, string.Empty, DateTime.Now);
        }

        // Extract refresh token
        string? refreshToken = root["refresh_token"]?.ToString();
        // Extract access token
		string? accessToken = root["access_token"]?.ToString();
		// Extraxt expiry
        string? expires_in = root["expires_in"]?.ToString();
		int expiresIn = string.IsNullOrEmpty(expires_in) ? 0 : int.Parse(expires_in);
		DateTime expiresAt = DateTime.Now.AddSeconds(expiresIn - 60);

        // Order result in the AuthData struct
        return (refreshToken, accessToken, expiresAt);
    }
    #endregion
}