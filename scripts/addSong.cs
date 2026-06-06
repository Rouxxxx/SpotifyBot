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

[Serializable]
public class QueueItem
{
    public string username { get; set; }
    public string trackURI { get; set; }
    public string trackName { get; set; }
}

public class Song
{
    public string artist { get; set; }
    public string name { get; set; }
    public string URI { get; set; }
}

public enum TrackError
{
    notFound = 1,
    tooLong,
    linksNotAllowed,
    addTrackError
}

public class CPHInline
{
	private static HttpClient _http;

    #region execute
    public bool Execute()
    {
        // Get user input
        string input = string.Empty;
        CPH.TryGetArg("rawInput", out input);

        // Error handling for song input
        if (string.IsNullOrEmpty(input))
        {
            CPH.SendMessage("No input found in request");
            return true;
        }

        string user;
        CPH.TryGetArg("user", out user);

        bool maxRequests = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_maxuser", false);
        int max = CPH.GetGlobalVar<int>("SPOTIFYBOT_SR_maxuser_number", false);

        bool checkQueue = CanUserAddTrackInQueue(maxRequests, max, user);
        if (!checkQueue)
        {
            CPH.SendMessage($"User already added {max} songs to the queue");
            return true;
        }

        // Init user credentials
        var (accessToken, expiresAt) = InitAPI();
        // TODO : HANDLE WRONG VALUES
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        // Init variables
        string songURI = string.Empty;
        string songInfo = string.Empty;
        int songDuration = 0;
        int error = 0;
        bool isSpotifyLink = false;
        bool spotifyLinksAllowed = false;

        bool songLength = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_length", false);
        // Convert s to ms
        int songLengthNumber = CPH.GetGlobalVar<int>("SPOTIFYBOT_SR_length_number", false);

        Task.Run(async () =>
        {
            try
            {
                isSpotifyLink = Regex.IsMatch(input, @"^https?:\/\/[^\s\/$.?#].[^\s]*$", RegexOptions.IgnoreCase);
                // If URL, just add it to the queue
                if (isSpotifyLink)
                {
                    // Tracking spotify links not allowed
                    spotifyLinksAllowed = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_spotifylink", false);
                    if (!spotifyLinksAllowed)
                    {
                        error = (int)TrackError.linksNotAllowed;
                        return;
                    }
                    (songURI, songInfo, songDuration) = await FetchSpotifyLinkInfo(accessToken, input);
                }
                // If normal request, search for the track
                else
                {
				    (songURI, songInfo, songDuration) = await SearchTrack(accessToken, input);
                }

                // Tracking song not found
                if (string.IsNullOrEmpty(songURI) || string.IsNullOrEmpty(songInfo))
                {
                    error = (int)TrackError.notFound;
                    return;
                }
                // Tracking song too long
                if (songLength && (songDuration > songLengthNumber))
                {
                    error = (int)TrackError.tooLong;
                    return;
                }

                // Add track to queue
                int trackReturn = await AddTrackToQueue(accessToken, songURI);
                if (trackReturn != 200)
                {
                    error = (int)TrackError.addTrackError;
                    return;
                }
            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();

        // Error handling
        if (error != 0)
        {
            switch(error)
            {
                case (int)TrackError.notFound:
                {
                    CPH.SendMessage($"No song found for {input}");
                    break;
                }
                case (int)TrackError.tooLong:
                {
                    CPH.SendMessage($"Song [{songInfo}] ({songDuration}s) is longer than maximum allowed ({songLengthNumber}s)");
                    break;
                }
                case (int)TrackError.linksNotAllowed:
                {
                    CPH.SendMessage($"Spotify links are forbidden");
                    break;
                }
                case (int)TrackError.addTrackError:
                {
                    CPH.SendMessage("There was an error trying to add song to queue");
                    break;
                }
                default: break;
            }
            return true;
        }

        // Add song to the internal queue, and send message to user
        AddTrackToInternalQueue(user, songURI, songInfo);
        CPH.SendMessage($"Added [{songInfo}] to the queue");

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
        var request = new HttpRequestMessage(
            method,
            URI);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await _http.SendAsync(request);
        int statusCode = (int)response.StatusCode;

        CPH.LogDebug($"HTTP Request: [{URI}] Reponse code: {statusCode}");
        

        // If error, just return code with empty json
        if (!response.IsSuccessStatusCode)
            return (statusCode, string.Empty);

        string json = await response.Content.ReadAsStringAsync();
        CPH.LogDebug($"HTTP Request: [{URI}] JSON: {json}");
        return (statusCode, json);
    }
    #endregion

    #region addSong
    private static string GetSpotifyTrackID(string URL)
    {
        // Create URI object from URL
        if (!Uri.TryCreate(URL, UriKind.Absolute, out var URI))
            return null;

        // Split path: /intl-XX/track/ID
        // Absolute path to discard unecessary query parameters
        var segments = URI.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

        // Find the /track/XXX part of the URI
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].Equals("track", StringComparison.OrdinalIgnoreCase)
                && i + 1 < segments.Length)
            {
                return segments[i + 1];
            }
        }

        return null;
    }

    // Add a track to the queue from a link
    private async Task<(string songURI, string songInfo, int songDuration)> FetchSpotifyLinkInfo(string accessToken, string URL)
    {
        // Get track URI
        string trackID = GetSpotifyTrackID(URL);
        if (string.IsNullOrEmpty(trackID))
        {
            return (string.Empty, string.Empty, 0);
        }

        string URI = $"https://api.spotify.com/v1/tracks/{trackID}";

        // Get track info
        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);
        JObject root = JObject.Parse(json);

        return GetTrackInfo(root);
    }
    
    // Add a selected track to the queue
    private async Task<int> AddTrackToQueue(string accessToken, string trackURI)
    {
        string URI = $"https://api.spotify.com/v1/me/player/queue?uri={trackURI}";
        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Post);
        return status;
    }

    // Check if user already sent too much songs in queue
    private bool CanUserAddTrackInQueue(bool isEnabled, int max, string username)
    {
        // If max request number isn't enabled, allow all requests
        if (!isEnabled) 
        {
            return true;
        }
        // Load variables
        string json = CPH.GetGlobalVar<string>("SPOTIFYBOT_queue", false);
        List<QueueItem> queue = string.IsNullOrEmpty(json) ? new() : JsonConvert.DeserializeObject<List<QueueItem>>(json);

        // If less tracks in queue than max, OK
        if (queue.Count < max)
        {
            return true;
        }

        int numberUserTracks = 0;
        foreach (QueueItem item in queue)
        {
            if (item.username == username)
            {
                numberUserTracks += 1;
            }
        }
        return (numberUserTracks < max);
    }

    // Add a track to StreamerBot's internal queue, to check and limit how much songs users can add
    private void AddTrackToInternalQueue(string username, string songURI, string songInfo)
    {
        string json = CPH.GetGlobalVar<string>("SPOTIFYBOT_queue", false);
        List<QueueItem> queue = string.IsNullOrEmpty(json) ? new() : JsonConvert.DeserializeObject<List<QueueItem>>(json);
        queue.Add(new QueueItem
        {
            username = username,
            trackURI = songURI,
            trackName = songInfo
        });

        json = JsonConvert.SerializeObject(queue);
        CPH.SetGlobalVar("SPOTIFYBOT_queue", json, false);
    }
    #endregion

    #region searchSong
    // Search a track and return its uri
    private async Task<(string songURI, string songInfo, int songDuration)> SearchTrack(string accessToken, string query)
    {
        string URI = $"https://api.spotify.com/v1/search?limit=10&market=US&type=track&q=track:{query}";
        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);

        // Error handling
        if (string.IsNullOrWhiteSpace(json))
        {
            return (string.Empty, string.Empty, 0);
        }

        // Parse the JSON response
        JObject root = JObject.Parse(json);
        JToken? item = root?["tracks"]?["items"]?[0];

        return GetTrackInfo(item);
    }
    #endregion

    #region command
    private (string songURI, string songInfo, int duration) GetTrackInfo(JToken item)
    {
        if (item == null)
        {
            return (string.Empty, string.Empty, 0);
        }

        // Extract song URI
        string? songURI = item["uri"]?.ToString();
        // Extract song name
        string? songName = item["name"]?.ToString();
        // Extract artist name
        string? artistName = item["artists"]?[0]?["name"]?.ToString();

        // Order and return song info
        int duration = (int)item["duration_ms"] / 1000;
        string songInfo = $"{songName} | {artistName}";
        return (songURI, songInfo, duration);
    }
    #endregion
}