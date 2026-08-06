using System;

public class CPHInline
{
	public bool Execute()
	{
        CPH.SetArgument("force", "true");
		CPH.RunAction("SPOTIFYBOT - Skip song", false);

		return true;
	}
}