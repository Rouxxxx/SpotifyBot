using System;
using System.Collections.Generic;

public class CPHInline
{
	public bool Execute()
	{
        // Get user input
        string input = string.Empty;
        CPH.TryGetArg("rawInput", out input);

        // user info
        if (input.StartsWith("@"))
        {
            input = input.Substring(1);
        }

        CPH.SetTwitchUserVar(input, "SPOTIFYBOT_timedoutGUID", "", false);
        CPH.SetTwitchUserVar(input, "SPOTIFYBOT_timedout", false, false);
        CPH.SetTwitchUserVar(input, "SPOTIFYBOT_strikes", new List<DateTime>(), false);

        CPH.SendMessage($"Removed timeout for @{input}");

        return true;
	}
}