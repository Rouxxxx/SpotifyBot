using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public class CPHInline
{
	public bool Execute()
    {
        CPH.TryGetArg("user", out string user);

        List<DateTime> strikes = CPH.GetTwitchUserVar<List<DateTime>>(user, "SPOTIFYBOT_strikes", false);
        strikes = updateStrikes(strikes);

        int strikesNecessary = CPH.GetGlobalVar<int>("SPOTIFYBOT_SR_timeout_number", false);
        strikesNecessary = strikesNecessary == 0 ? 5 : strikesNecessary;
        int strikesCount = strikes.Count;

        if (strikesCount >= strikesNecessary)
        {
            CPH.SetArgument("user", user);
            CPH.RunAction("SPOTIFYBOT - Timeout user", false);
            return true;
        }
        CPH.SetTwitchUserVar(user, "SPOTIFYBOT_strikes", strikes, false);

        return true;
    }

    private List<DateTime> updateStrikes(List<DateTime> strikes)
    {
        DateTime now = DateTime.Now;
        if (strikes == null)
        {
            return [now];
        }

        List<DateTime> newStrikes = new();
        int minutesThreshold = 2;
        foreach (DateTime strike in strikes)
        {
            double minutesDifference = (now - strike).TotalMinutes;
            if (minutesDifference >= minutesThreshold)
            {
                continue;
            }
            newStrikes.Add(strike);
        }
        newStrikes.Add(now);
        return newStrikes;
    }
}