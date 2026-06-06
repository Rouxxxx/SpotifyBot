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
    #region execute
	public bool Execute()
	{
        string json = CPH.GetGlobalVar<string>("SPOTIFYBOT_configuration", true);
        if (string.IsNullOrEmpty(json))
        {
            return true;
        }
        LoadOptions(json);

		return true;
	}
    #endregion

	#region command
    // Get valuable elements sent and save them to the configuration
    private void LoadOptions(string json)
    {
        JObject configurationRoot = string.IsNullOrEmpty(json) ? new() : JObject.Parse(json);

        // Song role restrictions
        bool SRRestriction = configurationRoot["SR_restriction"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_restriction", SRRestriction, false);
        if (SRRestriction) {
            JObject SRRestrictionList = configurationRoot["SR_restriction_list"] as JObject;
            bool SRRestrictionFol = SRRestrictionList["follower"]?.ToObject<bool>() ?? false;
            bool SRRestrictionSub = SRRestrictionList["subscriber"]?.ToObject<bool>() ?? false;
            bool SRRestrictionVIP = SRRestrictionList["vip"]?.ToObject<bool>() ?? false;

            CPH.SetGlobalVar("SPOTIFYBOT_SR_restriction_follower", SRRestrictionFol, false);
            CPH.SetGlobalVar("SPOTIFYBOT_SR_restriction_subscriber", SRRestrictionSub, false);
            CPH.SetGlobalVar("SPOTIFYBOT_SR_restriction_vip", SRRestrictionVIP, false);
        }

        // Max requests
        bool maxRequests = configurationRoot["SR_maxuser"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_maxuser", maxRequests, false);
        if (maxRequests) {
            int maxRequestsNumber = configurationRoot["SR_maxuser_number"]?.ToObject<int>() ?? 0;
            CPH.SetGlobalVar("SPOTIFYBOT_SR_maxuser_number", maxRequestsNumber, false);
        }

        // Skip songs
        bool skipSongs = configurationRoot["SR_skip"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_skip", skipSongs, false);
        if (skipSongs) {
            int skipSongsNumber = configurationRoot["SR_skip_number"]?.ToObject<int>() ?? 0;
            CPH.SetGlobalVar("SPOTIFYBOT_SR_skip_number", skipSongsNumber, false);
        }

        // Song length
        bool songLength = configurationRoot["SR_length"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_length", skipSongs, false);
        if (songLength) {
            int songLengthNumber = configurationRoot["SR_length_number"]?.ToObject<int>() ?? 0;
            CPH.SetGlobalVar("SPOTIFYBOT_SR_length_number", songLengthNumber, false);
        }

        // Spotify links
        bool spotifyLinks = configurationRoot["SR_spotifylink"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_spotifylink", spotifyLinks, false);
    }

    // Send the response via websocket
    private void SendResponse(Dictionary<string, object> args, int status, string message = "")
    {
        // Build response
        var response = new Dictionary<string, object>
        {
            ["requestID"] = args.ContainsKey("runningActionId") ? args["runningActionId"] : null,
            ["status"] = status,
        };

        // If error, add error message to the request
        if (!string.IsNullOrEmpty(message))
        {
            response["message"] = message;
        }

        string responseJson = JsonConvert.SerializeObject(response);

        // Send back through websocket
        CPH.WebsocketBroadcastJson(responseJson);
    }
    #endregion
}