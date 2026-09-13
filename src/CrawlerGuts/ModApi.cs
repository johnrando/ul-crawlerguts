namespace CrawlerGuts
{
	public class ModApi : IModApi
	{
		public void InitMod(Mod _modInstance)
		{
			// Settings first, so the startup log reports the player's values rather than the
			// built-in defaults.
			Config.Load();
			Patches.Apply();
		}
	}
}
