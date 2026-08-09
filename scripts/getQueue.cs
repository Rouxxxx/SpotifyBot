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
        bool queueActive = CPH.GetGlobalVar<bool>("SPOTIFYBOT_SR_queue", false);
        if (!queueActive)
        {
            CPH.SendMessage("Queue is disabled");
            return true;
        }
        string queueJSON = CPH.GetGlobalVar<string>("SPOTIFYBOT_queue", false);

        // If queue is empty, no need to do anything
        if (string.IsNullOrEmpty(queueJSON) || queueJSON == "[]")
        {
            CPH.SendMessage("Queue is empty");
            return true;
        }

        // If queue is empty, no need to do anything
        List<QueueItem> queue = JsonConvert.DeserializeObject<List<QueueItem>>(queueJSON);
        if (queue == null || queue.Count < 2)
        {
            CPH.SendMessage("Queue is empty");
            return true;
        }

        string message = BuildQueueMessage(queue);
        // If queue returned empty, show empty queue
        if (string.IsNullOrWhiteSpace(message))
        {
            CPH.SendMessage("Queue is empty");
            return true;
        }
        CPH.SendMessage(message);
        return true;
    }
    #endregion

    #region command
    // Builds message containing queue info
    public string BuildQueueMessage(List<QueueItem> queue)
    {
        int queueLength = queue.Count;
        string message = string.Empty;
        // Skip first element (current song)
        for (int id = 1; id < queue.Count; id++) 
        {
            int msgLength = message.Length;
            string endQueueMsg = $"and {queueLength - id} other";
            endQueueMsg = (queueLength - id > 1) ? $"{endQueueMsg}s" : endQueueMsg;

            int endQueueMsgLength = endQueueMsg.Length;

            QueueItem item = queue[id];
            // Skip elements addded manually
            if (item.username == defaultSongUser)
            {
                continue;
            }
            string currentSong = $"[{item.trackName} | {item.artistName}]";
            int currentSongLength = currentSong.Length;

            // Twitch messages cannot be longer than 500 characters
            if (msgLength + currentSongLength + endQueueMsgLength + 1 >= 50)
            {
                message += $" {endQueueMsg}";
                break;
            }

            // Add current song to the list
            if (!string.IsNullOrEmpty(message))
            {
                message += ", ";
            }
            message += $"[{item.trackName} | {item.artistName}]";
        }
        return message;
    }
    #endregion
}