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
        // Init user credentials
        var (accessToken, expiresAt) = InitAPI();
        // TODO : HANDLE WRONG VALUES
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        var song = string.Empty;
        bool error = false;

        Task.Run(async () =>
        {
            try
            {
                // Search for the track and add it to the queue
				song = await GetMusicPlaying(accessToken);
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                error = true;
                return;
            }
        }).GetAwaiter().GetResult();

        if (error)
        {
            CPH.SendMessage("Error during GetMusicPlaying (check logs for more information).");
        }
        if (string.IsNullOrEmpty(song))
        {
            CPH.SendMessage("No song is playing");
            return true;
        }
        CPH.SendMessage($"{song}");
        return true;
    }
    #endregion

    #region APIutils
    private (string accessToken, DateTime expiresAt) InitAPI()
    {
        // Get relevant user credentials
        // TODO : rate limiting
        string accessToken = CPH.GetGlobalVar<string>("SPOTIFYBOT_accessToken", true);
        DateTime expiresAt = CPH.GetGlobalVar<DateTime>("SPOTIFYBOT_expiresAt", true);

        // Check if token info is null
        if (string.IsNullOrEmpty(accessToken))
        {
            // If both access token and refresh token are null, exit
            string refreshToken = CPH.GetGlobalVar<string>("SPOTIFYBOT_refreshToken", true);
            if (string.IsNullOrEmpty(refreshToken))
            {
                return (string.Empty, DateTime.Now);
            }

            // If only access token is null, refresh token
            CPH.RunAction("SPOTIFYBOT - Refresh Token");

            // Load new variables
            accessToken = CPH.GetGlobalVar<string>("SPOTIFYBOT_accessToken", true);
            expiresAt = CPH.GetGlobalVar<DateTime>("SPOTIFYBOT_expiresAt", true);
            return (accessToken, expiresAt);
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

        return (accessToken, expiresAt);
    }

	// Send a HTTP request and parse the JSON body of the response
	private async Task<(int code, string json)> ProcessAPIRequest(string accessToken, string URI, HttpMethod method, int retries = 1)
    {
        var request = new HttpRequestMessage(method, URI);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _http.SendAsync(request);
        int statusCode = (int)response.StatusCode;

        CPH.LogDebug($"HTTP Request: [{URI}] Reponse code: {statusCode}");

        // If error, retries up to 5 times
        if (!response.IsSuccessStatusCode) {
            if (retries < 5) {
                return await ProcessAPIRequest(accessToken, URI, method, retries + 1);
            }
            return (statusCode, string.Empty);
        }

        string json = await response.Content.ReadAsStringAsync();
        return (statusCode, json);
    }
    #endregion

    #region command
    // Get the song and artist name of the music currently playing (none if nothing is playing)
    public async Task<string> GetMusicPlaying(string accessToken)
    {
        // Send the request to API
        string URI = "https://api.spotify.com/v1/me/player/currently-playing/";

        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);

        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        // Parse the JSON response
        JObject root = JObject.Parse(json);
        if (root == null)
        {
            return string.Empty;
        }

        // Check if a song is currently playing
        string isPlaying = root["is_playing"]?.ToString();
        if (isPlaying == "false")
            return string.Empty;

        // Get the song + artist names
        string? songName = root["item"]?["name"]?.ToString();
        string? artistName = root["item"]?["artists"]?[0]?["name"]?.ToString();

        if (string.IsNullOrEmpty(artistName) || string.IsNullOrEmpty(songName))
        {
            return string.Empty;
        }

        return $"{songName} | {artistName}";
    }
    #endregion
}