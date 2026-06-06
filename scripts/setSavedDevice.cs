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
	private static HttpClient _http;

    #region execute
	public bool Execute()
	{
        // Request from WebSocket
        if (!args.ContainsKey("data")) 
        {
            return true;
        }

        JObject root = JObject.Parse(args["data"].ToString());
        string deviceID = root["device_id"]?.ToString();

        // If var is empty, abort
        if (string.IsNullOrEmpty(deviceID))
        {
            SendResponse(args, 403, "device_id is empty");
            return true;
        }
        CPH.SetGlobalVar("SPOTIFYBOT_savedDevice", deviceID, true);
        SendResponse(args, 200);

		return true;
	}
    #endregion

	#region command
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