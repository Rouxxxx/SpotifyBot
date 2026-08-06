using System;

public class CPHInline
{
	public bool Execute()
	{
        // Get user input
        string input = string.Empty;
        CPH.TryGetArg("rawInput", out input);
        string[] args = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (args.Length == 0 || args.Length > 2)
        {
            CPH.SendMessage("Usage : !songtimeout @user [minutes]");
            return true;
        }

        // user info
        string user = args[0];
        if (user.StartsWith("@"))
        {
            user = user.Substring(1);
        }
        CPH.SetArgument("user", user);

        // duration (non mandatory)
        if (args.Length == 2)
        {
            Int32.TryParse(args[1], out int duration);
            CPH.SetArgument("duration", duration);
        }

		CPH.RunAction("SPOTIFYBOT - Timeout user", false);

        return true;
	}
}