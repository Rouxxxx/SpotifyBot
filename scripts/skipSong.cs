using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.RegularExpressions;

public class CPHInline
{
	private static HttpClient _http;

    #region execute
    public bool Execute()
    {
        // If users aren't allowed to skip, abort
        bool skip = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_skip", false);
        if (!skip)
        {
            CPH.SendMessage("Song skipping is disabled");
            return true;
        }

        // Load variables
        string user;
        CPH.TryGetArg("user", out user);
        string usersSkipping = CPH.GetGlobalVar<string>("SPOTIFYBOT_SR_usersSkipping", false);

        // If user cannot skip, abort
        bool checkSkip = CanUserSkipTrack(user, usersSkipping);
        if (!checkSkip)
        {
            CPH.SendMessage($"@{user} already voted to skip the song");
            return true;
        }

        // Add user to the list
        usersSkipping = string.IsNullOrEmpty(usersSkipping) ? user : $"{usersSkipping};{user}";
        CPH.SetGlobalVar("SPOTIFYBOT_SR_usersSkipping", usersSkipping, false);

        // Check if sufficient amount to skip has been reached
        int skipNumber = CPH.GetGlobalVar<int>("SPOTIFYBOT_SR_skip_number", false);
        int currentSkippingNumber = Regex.Matches(usersSkipping, ";").Count + 1;
        // If cannot skip, return
        if (currentSkippingNumber < skipNumber)
        {
            CPH.SendMessage($"@{user} voted to skip the track! ({currentSkippingNumber} / {skipNumber})");
            return true;
        }

        // Init user credentials
        var (accessToken, expiresAt) = InitAPI();
        // TODO : HANDLE WRONG VALUES
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        // Load current queue before skipping
        string queueJSON = CPH.GetGlobalVar<string>("SPOTIFYBOT_queue", false);
        List<QueueItem> queue = string.IsNullOrEmpty(queueJSON) ? new() : JsonConvert.DeserializeObject<List<QueueItem>>(queueJSON);

        Task.Run(async () =>
        {
            try
            {
                int status = await SkipTrack(accessToken);
                if (status == 200)
                {
                    // If skipped, add a strike to user
                    bool isTOEnabled = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_timeout", false);
                    if (isTOEnabled)
                    {
                        addStrike(queue);
                    }
                }
                // TODO : do smth with status
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();;

        // Song is skipped
        CPH.SetGlobalVar("SPOTIFYBOT_SR_usersSkipping", string.Empty, false);
        CPH.SendMessage("Skipping song...");

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
    private (string accessToken, DateTime expiresAt) InitAPI()
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

    #region skipSong
    // Skip a track on Spotify
    private async Task<int> SkipTrack(string accessToken)
    {
        string URI = $"https://api.spotify.com/v1/me/player/next/";
        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Post);
        return status;
    }

    // Add strike to user who requested skipped song
    private void addStrike(List<QueueItem> queue)
    {
        if (queue == null || queue.Count == 0)
        {
            return;
        }
        QueueItem currentSong = queue[0];
        string username = currentSong.username;
        if (string.IsNullOrEmpty(username))
        {
            return;
        }
        CPH.SetArgument("user", username);
		CPH.RunAction("SPOTIFYBOT - Timeout add strike", false);
    }
    #endregion

    #region command
    // Check if user already sent too much songs in queue
    private bool CanUserSkipTrack(string username, string usersSkipping)
    {
        // Entry case, no user skipping
        if (string.IsNullOrEmpty(usersSkipping))
        {
            return true;
        }

        // Check if user is already in list of users skipping
        return !usersSkipping.Contains(username);
    }
    #endregion
}