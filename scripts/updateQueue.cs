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

[Serializable]
public class QueueItem
{
    public string username { get; set; }
    public string trackURI { get; set; }
    public string trackName { get; set; }
}

public class CPHInline
{
	private static HttpClient _http;

    #region execute
    public bool Execute()
    {
        // Run only if user is live
        //if (!CPH.ObsIsStreaming())
        //    return false;
        string queueJSON = CPH.GetGlobalVar<string>("SPOTIFYBOT_queue", false);
        List<QueueItem> queue = string.IsNullOrEmpty(queueJSON) ? new() : JsonConvert.DeserializeObject<List<QueueItem>>(queueJSON);
        // If queue is empty, no need to do anything
        if (queue.Count == 0)
        {
            return true;
        }

        // Init user credentials
        var (accessToken, expiresAt) = InitAPI();
        // TODO : HANDLE WRONG VALUES
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        // Init current song variables
        string songURI = string.Empty;
        string songInfo = string.Empty;
        int progressMS = 0;

        Task.Run(async () =>
        {
            try
            {
                // Get current track info
                (songURI, songInfo, progressMS) = await GetPlayerInfo(accessToken);

                // TODO : HANDLE ERROR
                if (string.IsNullOrEmpty(songURI))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();;

        UpdateInternalQueue(queue, songURI, songInfo, progressMS);
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
    // Update the internal queue if needed
    public void UpdateInternalQueue(List<QueueItem> queue, string songURI, string songInfo, int progressMS)
    {
        // If nothing playing, ignore
        if (string.IsNullOrEmpty(songURI))
        {
            return;
        }
        // If queue changes, update the queue + current song global variables
        var (newQueue, changed) = UpdateQueue(queue, songURI, songInfo, progressMS);
        if (changed)
        {
            ResetQueueVariables(newQueue, songURI, songInfo);
        }
    }

    private void ResetQueueVariables(List<QueueItem> queue, string songURI, string songInfo)
    {
        // If queue empty, reset vars
        if (queue.Count == 0)
        {
            CPH.SetGlobalVar("SPOTIFYBOT_queue", "[]", false);
            CPH.SetGlobalVar("SPOTIFYBOT_currentSong", songURI, false);
            return;
        }
        string queueJSON = JsonConvert.SerializeObject(queue);
        CPH.SetGlobalVar("SPOTIFYBOT_queue", queueJSON, false);
        CPH.SetGlobalVar("SPOTIFYBOT_currentSong", songURI, false);

        //TODO : change everytime currentSong changes instead
        CPH.SetGlobalVar("SPOTIFYBOT_SR_usersSkipping", string.Empty, false);
    }

    // Update queue based on URI of current song playing
    public (List<QueueItem> queue, bool changed) UpdateQueue(List<QueueItem> queue, string currentSongURI, string currentSongInfo, int progressMS)
    {
        // Check if current track is in queue
        bool isTrackInQueue = false;
        foreach (QueueItem item in queue)
        {
            if (item.trackURI == currentSongURI)
            {
                isTrackInQueue = true;
                break;
            }
        }
        
        // If current song isn't in queue, wipe queue
        if (!isTrackInQueue)
        {
            string currentSong = CPH.GetGlobalVar<string>("SPOTIFYBOT_currentSong", false);
            QueueItem lastSongInQueue = queue[queue.Count - 1];

            // If the last song was just played, reset the queue
            if (lastSongInQueue.trackURI == currentSong)
            {
                return (new List<QueueItem>(), true);
            }
            queue.Insert(0, new QueueItem
            {
                username = "BOT",
                trackURI = currentSongURI,
                trackName = currentSongInfo
            });
            return (queue, true);
        }

        // Find where is track in queue
        // Remove all previous songs from queue
        bool changed = false;
        while (queue.Count > 0 && queue[0].trackURI != currentSongURI)
        {
            changed = true;
            queue.RemoveAt(0);
        }
        return (queue, changed);
    }

    // Check current state of Spotify player
    public async Task<(string songURI, string songInfo, int progressMS)> GetPlayerInfo(string accessToken)
    {
        string URI = $"https://api.spotify.com/v1/me/player/";
        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);

        if (string.IsNullOrWhiteSpace(json))
        {
            return (string.Empty, string.Empty, 0);
        }

        // Parse the JSON response
        JObject root = JObject.Parse(json);
        if (root == null || root["item"] == null || string.IsNullOrEmpty(root["item"].ToString()))
        {
            return (string.Empty, string.Empty, 0);
        }

        // Extract song info
        string? songURI = root["item"]?["uri"]?.ToString();
        string? songName = root["item"]?["name"]?.ToString();
        string? artistName = root["item"]?["artists"]?[0]?["name"]?.ToString();

        // Extract player info
        string? progress_ms = root["progress_ms"]?.ToString();
		int progressMS = string.IsNullOrEmpty(progress_ms) ? 0 : int.Parse(progress_ms);

        // Return info
        string songInfo = $"{songName} by {artistName}";
        return (songURI, songInfo, progressMS);
    }
    #endregion
}