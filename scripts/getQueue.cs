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
        string currentSong = CPH.GetGlobalVar<string>("SPOTIFYBOT_currentSong", false);
        string queueJSON = CPH.GetGlobalVar<string>("SPOTIFYBOT_queue", false);

        // If queue is empty, no need to do anything
        if (string.IsNullOrEmpty(queueJSON) || queueJSON == "[]")
        {
            CPH.SendMessage("Queue is empty");
            return true;
        }

        // If queue is empty, no need to do anything
        List<QueueItem> queue = JsonConvert.DeserializeObject<List<QueueItem>>(queueJSON);
        if (queue == null || queue.Count == 0 || (queue.Count == 1 && queue[0].trackURI == currentSong))
        {
            CPH.SendMessage("Queue is empty");
            return true;
        }

        string message = buildQueueMessage(queue, currentSong);
        CPH.SendMessage(message);
        return true;
    }
    #endregion

    #region command
    // Builds message containing queue info
    public string buildQueueMessage(List<QueueItem> queue, string currentSong)
    {
        string message = string.Empty;
        for (int id = 0; id < queue.Count; id++) 
        {
            // Skip first element if it's the current song  playing
            QueueItem item = queue[id];
            if (id == 0 && item.trackURI == currentSong)
            {
                continue;
            }

            // Add current song to the list
            if (!string.IsNullOrEmpty(message))
            {
                message += "\n";
            }
            message += item.trackName;
        }
        return message;
    }
    #endregion
}