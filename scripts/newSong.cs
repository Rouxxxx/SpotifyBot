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
        // Init stored variables
        string songJSON = CPH.GetGlobalVar<string>("SPOTIFYBOT_currentsong", false);
        QueueItem song = string.IsNullOrEmpty(songJSON) ? null : JsonConvert.DeserializeObject<QueueItem>(songJSON);
        CPH.SetGlobalVar("SPOTIFYBOT_SR_usersSkipping", string.Empty, false);
        if (song!= null && !string.IsNullOrEmpty(song.username))
        {
            CPH.SendMessage($"[New song] (Requested by {song.username}) {song.trackName} - {song.artistName}");
        }
        return true;
    }
    #endregion
}