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
        JObject configuration = string.IsNullOrEmpty(json) ? new() : JObject.Parse(json);
        LoadOptions(configuration);
        ToggleQueue(configuration);

		return true;
	}
    #endregion

	#region command
    // Get valuable elements sent and save them to the configuration
    private void LoadOptions(JObject configuration)
    {
        // Song role restrictions
        bool SRRestriction = configuration["SR_restriction"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_restriction", SRRestriction, false);
        if (SRRestriction) {
            JObject SRRestrictionList = configuration["SR_restriction_list"] as JObject;
            bool SRRestrictionFol = SRRestrictionList["follower"]?.ToObject<bool>() ?? false;
            bool SRRestrictionSub = SRRestrictionList["subscriber"]?.ToObject<bool>() ?? false;
            bool SRRestrictionVIP = SRRestrictionList["vip"]?.ToObject<bool>() ?? false;

            CPH.SetGlobalVar("SPOTIFYBOT_SR_restriction_follower", SRRestrictionFol, false);
            CPH.SetGlobalVar("SPOTIFYBOT_SR_restriction_subscriber", SRRestrictionSub, false);
            CPH.SetGlobalVar("SPOTIFYBOT_SR_restriction_vip", SRRestrictionVIP, false);
        }

        // Max requests
        bool maxRequests = configuration["SR_maxuser"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_maxuser", maxRequests, false);
        if (maxRequests) {
            int maxRequestsNumber = configuration["SR_maxuser_number"]?.ToObject<int>() ?? 0;
            CPH.SetGlobalVar("SPOTIFYBOT_SR_maxuser_number", maxRequestsNumber, false);
        }

        // Skip songs
        bool skipSongs = configuration["SR_skip"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_skip", skipSongs, false);
        if (skipSongs) {
            int skipSongsNumber = configuration["SR_skip_number"]?.ToObject<int>() ?? 0;
            CPH.SetGlobalVar("SPOTIFYBOT_SR_skip_number", skipSongsNumber, false);
        }

        // Song length
        bool songLength = configuration["SR_length"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_length", songLength, false);
        if (songLength) {
            int songLengthNumber = configuration["SR_length_number"]?.ToObject<int>() ?? 0;
            CPH.SetGlobalVar("SPOTIFYBOT_SR_length_number", songLengthNumber, false);
        }

        // Spotify links
        bool spotifyLinks = configuration["SR_spotifylink"]?.ToObject<bool>() ?? false;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_spotifylink", spotifyLinks, false);
    }

    // Enable or disable the queue system
    private void ToggleQueue(JObject configuration)
    {
        bool SRqueue = configuration["SR_queue"]?.ToObject<bool>() ?? true;
        CPH.SetGlobalVar("SPOTIFYBOT_SR_queue", SRqueue, false);
        if (SRqueue)
        {
            CPH.EnableTimer("SPOTIFYBOT - Update Queue");
            return;
        }

        // If queue disabled, also disable the max amount of songs an user can queue + the skip command
        CPH.DisableTimer("SPOTIFYBOT - Update Queue");
        CPH.SetGlobalVar("SPOTIFYBOT_SR_maxuser", false, false);
        CPH.SetGlobalVar("SPOTIFYBOT_SR_skip", false, false);
    }
    #endregion

    #region websocket
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