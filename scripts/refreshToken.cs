using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

public class refreshedTokenResponse
{
    public string access_token { get; set; }
    public string expires_in { get; set; }
}

public class CPHInline
{
	private static HttpClient _http;
	
	#region execute
    public bool Execute()
    {
		string accessToken = string.Empty;
		DateTime expiresAt = DateTime.Now;

		// Get relevant user credentials
		string clientID = CPH.GetGlobalVar<string>("SPOTIFYBOT_clientID", true);
		string clientSecret = CPH.GetGlobalVar<string>("SPOTIFYBOT_clientSecret", true);
		string refreshToken = CPH.GetGlobalVar<string>("SPOTIFYBOT_refreshToken", true);

		// If not logged in, exit
		if (string.IsNullOrEmpty(clientID) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
		{
			return true;
		}

        Task.Run(async () =>
        {
            try
            {
				(accessToken, expiresAt) = await RefreshToken(clientID, clientSecret, refreshToken);
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
            }
        }).GetAwaiter().GetResult();
		CPH.SetGlobalVar("SPOTIFYBOT_accessToken", accessToken, true);
		CPH.SetGlobalVar("SPOTIFYBOT_expiresAt", expiresAt, true);

		CPH.LogDebug($"{DateTime.Now} - Refreshed access token");
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

	#region utils
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

	#region command
	// Refresh the access token using the refresh token
	public async Task<(string accessToken, DateTime expiresAt)> RefreshToken(string clientID, string clientSecret, string refreshToken)
	{
		// Handle wrong variables
		if (string.IsNullOrEmpty(clientID) || string.IsNullOrEmpty(clientSecret) || string.IsNullOrEmpty(refreshToken))
		{
			return (string.Empty, DateTime.Now);
		}

		const string URI = "https://accounts.spotify.com/api/token";
		// Build the refresh request
		var request = new HttpRequestMessage(HttpMethod.Post, URI);

		// Add refresh token to the body
		var body = new Dictionary<string, string>
		{
			{ "grant_type", "refresh_token" },
			{ "refresh_token", refreshToken }
		};
		request.Content = new FormUrlEncodedContent(body);
		AddClientHeader(clientID, clientSecret, request);

		// Error handling
		var (status, json) = await ProcessAPIAuthRequest(request);
		CPH.LogDebug($"HTTP Request: [{URI}] Reponse code: {status}");

		if (status != 200)
		{
			return (string.Empty, DateTime.Now);
		}

		// Parse the JSON response
        JObject root = JObject.Parse(json);
        if (root == null)
        {
            return (string.Empty, DateTime.Now);
        }

        // Extract access token
		string? accessToken = root["access_token"]?.ToString();
		// Extraxt expiry
        string? expires_in = root["expires_in"]?.ToString();
		int expiresIn = string.IsNullOrEmpty(expires_in) ? 0 : int.Parse(expires_in);
		DateTime expiresAt = DateTime.Now.AddSeconds(expiresIn - 60);

		return (accessToken, expiresAt);
	}
	#endregion
}