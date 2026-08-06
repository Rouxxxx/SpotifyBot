using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

public class CPHInline
{
	public bool Execute()
    {
        CPH.TryGetArg("user", out string user);
        CPH.TryGetArg("duration", out int setduration);

        Guid guid = Guid.NewGuid();
        CPH.SetTwitchUserVar(user, "SPOTIFYBOT_timedoutGUID", guid, false);
        CPH.SetTwitchUserVar(user, "SPOTIFYBOT_timedout", true, false);
        CPH.SetTwitchUserVar(user, "SPOTIFYBOT_strikes", new List<DateTime>(), false);

        int duration = (setduration != 0) ? setduration : CPH.GetGlobalVar<int>("SPOTIFYBOT_SR_timeout_duration", false);
        SetUnTimeout(guid, duration, user);

        DateTime now = DateTime.Now;
        CPH.SetTwitchUserVar(user, "SPOTIFYBOT_timeoutLast", now, false);
        CPH.SetTwitchUserVar(user, "SPOTIFYBOT_timeoutLastDuration", duration, false);


        CPH.SendMessage($"Timed out {user} from sending songs for {duration} minutes");

        return true;
    }

    // Start task to un-timeout user
    private void SetUnTimeout(Guid guid, int duration, string user)
    {
        Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromMinutes(duration));
            Guid savedguid = CPH.GetTwitchUserVar<Guid>(user, "SPOTIFYBOT_timedoutGUID", false);
            if (savedguid != guid)
            {
                return;
            }
            CPH.SetTwitchUserVar(user, "SPOTIFYBOT_timedout", false, false);
        });    
    }
}