namespace CrawlerGuts
{
	/// <summary>
	/// PUBLISHED CONTRACT. Partner mods bind <see cref="SetFlavor"/> by reflection, the way
	/// <see cref="FlavorPartner"/> binds theirs, so this signature is the whole interface. Changing
	/// it does not break the build; it silently unlinks the switches. Game types and strings only.
	/// </summary>
	public static class FlavorInterop
	{
		/// <summary>
		/// Called when the player toggles their flavor switch for this mod in another mod. Sets
		/// this side's switch for that partner and saves. Deliberately does not push anywhere:
		/// whoever the player typed at owns the mirror, which is what stops two mods calling each
		/// other forever.
		/// </summary>
		/// <param name="_partner">The calling mod's label, e.g. "FletchWounds".</param>
		public static void SetFlavor(string _partner, bool _on)
		{
			FlavorSwitches.Set(_partner, _on);
			Config.Save();
		}
	}
}
