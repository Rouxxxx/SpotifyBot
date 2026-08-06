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
    public string artistName { get; set; }
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
        input = SanitizeString(input);

        string user;
        CPH.TryGetArg("user", out user);

        bool isTimedOut = CPH.GetTwitchUserVar<bool>(user, "SPOTIFYBOT_timedout", false);
        if (isTimedOut)
        {
            CPH.SendMessage($"{user} you're currently timed out from adding songs to the queue");
            return true;
        }

        string broadcasterId = CPH.TwitchGetBroadcaster().UserId;
        string userId = args["userId"].ToString();

        TwitchUserInfoEx userInfo = CPH.TwitchGetExtendedUserInfoByLogin(user);
        bool follower = userInfo.IsFollowing;
        bool subscriber = userInfo.IsSubscribed;
        bool vip = userInfo.IsVip;

        bool restriction = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_restriction", false);
        bool restriction_follower = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_restriction_follower", false);
        bool restriction_subscriber = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_restriction_subscriber", false);
        bool restriction_vip = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_restriction_vip", false);

        // If user doesn't match a restriction, abort
        if ((userId != broadcasterId) && restriction && ((restriction_follower && !follower) || (restriction_subscriber && !subscriber) || (restriction_vip && !vip)))
        {
            string role;
            if (restriction_follower && !follower)
            {
                role = "follower";
            }
            else if (restriction_subscriber && !subscriber)
            {
                role = "subscriber";
            }
            else
            {
                role = "VIP";
            }
            CPH.SendMessage($"You need to be a {role} to request songs.");
            return true;
        }

        // Check if user already requests too much songs in queue
        bool maxRequests = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_maxuser", false);
        int max = CPH.GetGlobalVar<int>("SPOTIFYBOT_SR_maxuser_number", false);
        bool checkQueue = CanUserAddTrackInQueue(maxRequests, max, user);
        if (!checkQueue)
        {
            CPH.SendMessage($"You already added {max} songs to the queue ! Please wait before requesting more songs");
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
        QueueItem track = null;
        int songDuration = 0;
        int error = 0;
        bool isSpotifyLink = false;
        bool isYoutubeLink = false;
        bool spotifyLinksAllowed = false;
        bool youtubeLinksAllowed = false;
        bool songLength = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_length", false);
        int songLengthNumber = CPH.GetGlobalVar<int>("SPOTIFYBOT_SR_length_number", false);

        Task.Run(async () =>
        {
            try
            {
                bool isLink = Regex.IsMatch(input, @"^https?:\/\/[^\s\/$.?#].[^\s]*$", RegexOptions.IgnoreCase);
                isSpotifyLink = input.Contains("spotify.com");
                isYoutubeLink = input.Contains("youtube.com") || input.Contains("youtu.be");
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
                    (track, songDuration) = await FetchSpotifyLinkInfo(accessToken, input);
                }
                // If youtube URL, try and search the song
                else if (isYoutubeLink)
                {
                    // Tracking spotify links not allowed
                    youtubeLinksAllowed = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_youtubelink", false);
                    if (!youtubeLinksAllowed)
                    {
                        error = (int)TrackError.linksNotAllowed;
                        return;
                    }
                    input = await FetchYoutubeLinkInfo(input);
                    input = SanitizeString(input);
                    if (string.IsNullOrEmpty(input))
                    {
                        error = (int)TrackError.notFound;
                        return;
                    }
                    (track, songDuration) = await SearchTrack(accessToken, input);
                }
                // If normal request, search for the track
                else
                {
				    (track, songDuration) = await SearchTrack(accessToken, input);
                }

                // Tracking song not found
                if (track == null)
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
                int trackReturn = await AddTrackToQueue(accessToken, track.trackURI);
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
            HandleError(error, input, track, songDuration, songLengthNumber, isSpotifyLink);
            return true;
        }

        // Add song to the internal queue, and send message to user
        AddTrackToInternalQueue(user, track);
        CPH.SendMessage($"Added [{track.trackName} | {track.artistName}] to the queue");

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
            if (retries < 2) {
                return await ProcessAPIRequest(accessToken, URI, method, retries + 1);
            }
            return (statusCode, string.Empty);
        }

        string json = await response.Content.ReadAsStringAsync();
        return (statusCode, json);
    }
    #endregion

    #region links
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

    // Find track info from a youtube link
    private async Task<string> FetchYoutubeLinkInfo(string URL)
    {
        // Fetch youtube API
        string response = string.Empty;
        try { response = await _http.GetStringAsync(URL); }
        catch (Exception ex) { }
        if (string.IsNullOrEmpty(response))
        {
            return string.Empty;
        }

        // Find the div in the response
        string pattern = @"var\s+ytInitialPlayerResponse\s*=\s*(\{.*?\});";
        var match = System.Text.RegularExpressions.Regex.Match(response, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!match.Success)
        {
            return string.Empty;
        }

        // Find details in JSON
        string jsonStr = match.Groups[1].Value;
        JObject initialResponse = JObject.Parse(jsonStr);
        JObject videoDetails = initialResponse["videoDetails"] as JObject;
        if (videoDetails == null)
        {
            return string.Empty;
        }
                
        string title = videoDetails["title"]?.ToString();
        string author = videoDetails["author"]?.ToString();

        // Erase everything concerning feats in title
        string marker = "ft";
        string marker2 = "feat";
        int index = title.IndexOf(marker);
        int index2 = title.IndexOf(marker2);

        int min = Math.Min((index > 0) ? index : int.MaxValue, (index2 > 0) ? index2 : int.MaxValue);
        title = (min != int.MaxValue) ? title.Substring(0, min) : title;

        return $"{title} {author}";
    }


    // Add a track to the queue from a link
    private async Task<(QueueItem track, int songDuration)> FetchSpotifyLinkInfo(string accessToken, string URL)
    {
        // Get track URI
        string trackID = GetSpotifyTrackID(URL);
        if (string.IsNullOrEmpty(trackID))
        {
            return (null, 0);
        }

        string URI = $"https://api.spotify.com/v1/tracks/{trackID}";

        // Get track info
        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);
        JObject root = JObject.Parse(json);

        return GetTrackInfo(root);
    }
    #endregion

    #region addsong
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
    private void AddTrackToInternalQueue(string username, QueueItem track)
    {
        string json = CPH.GetGlobalVar<string>("SPOTIFYBOT_queue", false);
        List<QueueItem> queue = string.IsNullOrEmpty(json) ? new() : JsonConvert.DeserializeObject<List<QueueItem>>(json);

        track.username = username;
        queue.Add(track);

        json = JsonConvert.SerializeObject(queue);
        CPH.SetGlobalVar("SPOTIFYBOT_queue", json, false);
    }
    #endregion

    #region searchSong
    // Search a track and return its uri
    private async Task<(QueueItem track, int songDuration)> SearchTrack(string accessToken, string query)
    {
        string URI = $"https://api.spotify.com/v1/search?limit=10&market=US&type=track&q=track:{query}";
        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);

        // Error handling
        if (string.IsNullOrWhiteSpace(json))
        {
            return (null, 0);
        }

        // Parse the JSON response
        JObject root = JObject.Parse(json);
        JArray tracks = (JArray)root["tracks"]?["items"];
        JToken item = FindBestMatch(tracks, query);

        return GetTrackInfo(item);
    }
    
    // Find the best suited track for the current query
    private JToken FindBestMatch(JArray tracks, string query)
    {
        if (tracks == null || tracks.Count == 0)
        {
            return null;
        }

        // Init variables
        int bestScore = 0;
        int bestBloat = 0;
        JToken bestTrack = null;
        List<string> queryWords = query.ToLower().Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries).ToList();

        // Loop through tracks and compute their score
        foreach(JToken item in tracks)
        {
            int currentScore = 0;
            string? name = item["name"]?.ToString();
            string nameLower = name.ToLower();
            string artists = GetArtists(item);

            // If query word is in artist / song name, increase score
            // Else, add 1 to bloat score
            foreach (string word in queryWords)
            {
                if(string.IsNullOrWhiteSpace(word))
                {
                    continue;
                }
                if (nameLower.Contains(word) || artists.Contains(word))
                {
                    currentScore++;
                }
            }
            string concat = $"{nameLower} {artists}";
            int wordCount = concat.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries).Length;
            int currentBloatScore = wordCount - currentScore;

            if (currentScore > bestScore || (currentScore == bestScore && currentBloatScore < bestBloat))
            {
                bestScore = currentScore;
                bestTrack = item;
                bestBloat = currentBloatScore;
            }
        }

        return bestTrack;
    }

    // For a track, get artists in a string separated by spaces
    private string GetArtists(JToken item)
    {
        string artistsString = string.Empty;
        JArray artists = (JArray)item["artists"];
        if (artists == null || artists.Count == 0)
        {
            return string.Empty;
        }

        // Loop through the artists array and fetch the names
        foreach (JToken artist in artists)
        {
            string currentArtist = artist["name"]?.ToString();
            if (string.IsNullOrEmpty(currentArtist))
            {
                continue;
            }
            string currentArtistLower = currentArtist.ToLower();
            artistsString = string.IsNullOrEmpty(artistsString) ? currentArtistLower : $"{artistsString} {currentArtistLower}";
        }
        return artistsString;
    }
    #endregion

    #region command
    // Parse a Spotify API track into a QueueItem
    private (QueueItem track, int duration) GetTrackInfo(JToken item)
    {
        if (item == null)
        {
            return (null, 0);
        }

        // Extract song URI
        string? trackURI = item["uri"]?.ToString();
        // Extract song name
        string? trackName = item["name"]?.ToString();
        // Extract artist name
        string? artistName = item["artists"]?[0]?["name"]?.ToString();

        // Order and return song info
        int duration = (int)item["duration_ms"] / 1000;

        QueueItem track = new QueueItem{ username = null, trackURI = trackURI, trackName = trackName, artistName = artistName };
        return (track, duration);
    }

    private string SanitizeString(string input)
    {
        string res = Regex.Replace(input, @"\([^)]*\)", "");
        res = Regex.Replace(res, @"\[[^\]]*\]", "");
        return res;
    }

    // Send error msg to represent error encountered
    private void HandleError(int error, string input, QueueItem track, int songDuration, int songLengthNumber, bool isSpotifyLink)
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
                CPH.SendMessage($"Song [{track.trackName} | {track.artistName}] ({songDuration}s) is longer than maximum allowed ({songLengthNumber}s)");
                break;
            }
            case (int)TrackError.linksNotAllowed:
            {
                string msg = (isSpotifyLink) ? "Spotify" : "Youtube";
                CPH.SendMessage($"{msg} links are forbidden");
                break;
            }
            case (int)TrackError.addTrackError:
            {
                CPH.SendMessage("There was an error trying to add song to queue");
                break;
            }
            default: break;
        }
    }
    #endregion
}