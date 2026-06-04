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
        string URI = "https://api.spotify.com/v1/me/";
        var (accessToken, expiresAt, message) = InitAPI();

        // If error with access token, return error 500 (Internal Server error)
        if (string.IsNullOrEmpty(accessToken))
        {
            SendResponse(args, 403, message);
            return true;
        }

        int status = 0;
        string json = string.Empty;

        // Fetch connection status from API
        Task.Run(async () =>
        {
            try
            {
				(status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();

        // Send the response via websocket
        SendResponse(args, status);

		return true;
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

	#region command
    // Send the response via websocket
    public void SendResponse(Dictionary<string, object> args, int status, string message = "")
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

        // Add clientID and clientSecret to the request if it exists
        if (!string.IsNullOrEmpty(clientID))
        {
            response["clientID"] = clientID;
        }
        if (!string.IsNullOrEmpty(clientSecret))
        {
            response["clientSecret"] = clientSecret;
        }
        string responseJson = JsonConvert.SerializeObject(response);

        // Send back through websocket
        CPH.WebsocketBroadcastJson(responseJson);
    }
    // Get the song and artist name of the music currently playing (none if nothing is playing)
    public async Task<int> GetConnectionStatus(string accessToken)
    {
        // Send the request to API
        string URI = "https://api.spotify.com/v1/me/";

        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);

        return status;
    }
    #endregion
}