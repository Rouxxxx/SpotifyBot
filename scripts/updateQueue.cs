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
    public string artistName { get; set; }
}

public class CPHInline
{
    private static string defaultSongUser = string.Empty;
	private static HttpClient _http;

    #region execute
    public bool Execute()
    {
        // Run only if user is live
        if (!CPH.ObsIsStreaming())
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

        // Init stored variables
        string queueJSON = CPH.GetGlobalVar<string>("SPOTIFYBOT_queue", false);
        List<QueueItem> queue = string.IsNullOrEmpty(queueJSON) ? new() : JsonConvert.DeserializeObject<List<QueueItem>>(queueJSON);

        // Init variables
        List<QueueItem> queueAPI = null;
        QueueItem currentTrackAPI = null;
        int progressMS = 0;

        Task.Run(async () =>
        {
            try
            {
                // Get current queue info
                queueAPI = await GetPlayerInfo(accessToken);

            }
            catch (Exception ex)
            {
                CPH.LogDebug($"Exception: {ex.Message}");
                return;
            }
        }).GetAwaiter().GetResult();

        bool songChanged = false;
        QueueItem newFirst = null;
        (songChanged, newFirst) = UpdateInternalQueue(queue, queueAPI);

        // If song changed, change variable and trigger song change
        if (songChanged && (newFirst != null))
        {
            string currentSongSTR = JsonConvert.SerializeObject(newFirst);
            CPH.SetGlobalVar("SPOTIFYBOT_currentsong", currentSongSTR, false);
            CPH.RunAction("SPOTIFYBOT - EVENT - New song");
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

    #region API
    // Extract the API result in a QueueItem format
    private QueueItem GetQueueItem(JToken track)
    {
        if (track == null)
        {
            return null;
        }
        string? trackURI = track["uri"]?.ToString();
        string? trackName = track["name"]?.ToString();
        string? artistName = track["artists"]?[0]?["name"]?.ToString();
        if (string.IsNullOrEmpty(trackURI) || string.IsNullOrEmpty(trackName) || string.IsNullOrEmpty(artistName))
        {
            return null;
        }
        return new QueueItem{ username = defaultSongUser, trackURI = trackURI, trackName = trackName, artistName = artistName };
    }

    // Extract the API queue result in a List<QueueItem> format
    private List<QueueItem> ExtractQueueAPI(JToken root)
    {
        // Extract queue token
        JArray tracks = (JArray)root["queue"];
        if (tracks == null)
        {
            return [];
        }

        // Loop through the queue array and fetch the items
        List<QueueItem> queue = new();
        foreach (JToken track in tracks)
        {
            // Extract track info
            QueueItem item = GetQueueItem(track);
            if (item == null)
            {
                continue;
            }
            queue.Add(item);
        }
        return queue;
    }

    // Check current state of Spotify player
    private async Task<List<QueueItem>> GetPlayerInfo(string accessToken)
    {
        string URI = $"https://api.spotify.com/v1/me/player/queue";
        var (status, json) = await ProcessAPIRequest(accessToken, URI, HttpMethod.Get);
        if (status != 200 || string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        // Parse the JSON response
        JObject root = JObject.Parse(json);
        if (root == null || root["queue"] == null || string.IsNullOrEmpty(root["queue"].ToString()))
        {
            return [];
        }

        // Extract current song
        QueueItem currentTrack = GetQueueItem(root["currently_playing"]);
        // Extract current song
        List<QueueItem> queue = ExtractQueueAPI(root);
        queue.Insert(0, currentTrack);
        
        return queue;
    }
    #endregion

    #region updatedata
    // Update the internal queue by comparing it with the API
    private (bool songChanged, QueueItem song) UpdateInternalQueue(List<QueueItem> queue, List<QueueItem> queueAPI)
    {
        // Compute new queue
        List<QueueItem> newQueue = UpdateQueue(queue, queueAPI);
        if (newQueue.Count == 0)
        {
            CPH.SetGlobalVar("SPOTIFYBOT_queue", "[]", false);
            return ((queue.Count != 0), null);
        }

        // Edit queue in config only if it changed
        bool changed = (queue.Count != newQueue.Count);
        if (!changed)
        {
            for (int id = 0; id < queue.Count; id++) 
            {
                // Compare by trackURI      
                string URI1 = queue[id].trackURI;
                string URI2 = newQueue[id].trackURI;
                if (URI1 != URI2)
                {
                    changed = true;
                    break;
                }
            }
        }
        if (changed)
        {
            string queueJSON = JsonConvert.SerializeObject(newQueue);
            CPH.SetGlobalVar("SPOTIFYBOT_queue", queueJSON, false);
        }
        // Track song change by comparing first song in old and new queue
        bool firstChanged = (queue.Count > 0 && queue[0].trackURI != newQueue[0].trackURI);

        return (firstChanged, newQueue[0]);
    }

    // Try to find a match in the internal queue, starting from IDqueue
    // Returns : The match ID in internal queue, or -1 if not found
    private int FindMatch(string searchURI, List<QueueItem> queue, int IDqueue, int sizeQueue)
    {
        for (int id = IDqueue; id < sizeQueue; id++) 
        {
            // Compare by trackURI      
            string URI = queue[id].trackURI;
            if (URI == searchURI)
            {
                return id;
            }
        }
        return -1;
    }

    // Find last track that wasnt added by streamer manually
    private int FindLastUserTrack(List<QueueItem> queue, int queueSize)
    {
        for (int id = queueSize - 1; id >= 0; id--) 
        {
            // Compare by usernames      
            string username = queue[id].username;
            if (username != defaultSongUser)
            {
                return id;
            }
        }
        return -1;
    }

    // Update queue based on URI of current song playing
    public List<QueueItem> UpdateQueue(List<QueueItem> queue, List<QueueItem> queueAPI)
    {
        // Init variables
        int sizeQueue = queue.Count;
        int sizeQueueAPI = queueAPI.Count;
        int IDqueue = 0;
        int IDqueueAPI = 0;
        List<QueueItem> newQueue = new();

        while(true)
        {
            if (IDqueueAPI >= sizeQueueAPI)
            {
                break;
            }

            QueueItem currentTrack = queueAPI[IDqueueAPI];
            int IDmatch = FindMatch(currentTrack.trackURI, queue, IDqueue, sizeQueue);

            // If no match, streamer added song
            if (IDmatch == -1)
            {
                newQueue.Add(currentTrack);
            }
            // If match, user added song
            else 
            {
                newQueue.Add(queue[IDmatch]);
                IDqueue = IDmatch + 1;
            }
            IDqueueAPI++;
        }
        if (newQueue.Count == 0)
        {
            return newQueue;
        }

        // Find the last user added element, to remove everything after
        int sizeNewQueue = newQueue.Count;
        int IDLastUserTrack = FindLastUserTrack(newQueue, sizeNewQueue);
        // If all songs are BOT songs, queue should be just containing current song
        if (IDLastUserTrack == -1)
        {
            return (sizeNewQueue > 0) ? [newQueue[0]] : [];
        }
        // Last queue song is user based, no need to cut anything
        if (IDLastUserTrack == sizeNewQueue - 1)
        {
            return newQueue;
        }
        // Remove all elements after last user track
        int startRemoval = IDLastUserTrack + 1;
        
        newQueue.RemoveRange(startRemoval, sizeNewQueue - startRemoval);

        return newQueue;
    }
    #endregion
}