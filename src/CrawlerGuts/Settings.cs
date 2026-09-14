namespace CrawlerGuts
{
	/// <summary>
	/// Runtime knobs, all switchable from the <c>cg</c> console command. The values here are the
	/// built-in defaults; <see cref="Config"/> reads the player's file over them at startup.
	/// </summary>
	internal static class Settings
	{
		/// <summary>Master switch. When off, the tick hook returns on its first line.</summary>
		internal static bool Enabled = true;

		/// <summary>
		/// Health a crawler loses per block (metre) it drags itself while chasing a player. May be
		/// fractional: the owed damage accumulates and whole points come off as they add up, so
		/// 0.5 is one point every two blocks.
		/// </summary>
		internal static float DamagePerBlock = 1f;

		/// <summary>
		/// Drag damage never takes a crawler below this percentage of its max health. A cap on the
		/// hit rather than a gate in front of it, so a crawler lands on the floor rather than
		/// through it. 0 removes the floor and lets dragging kill. The bleed is buff-driven and
		/// ignores this.
		/// </summary>
		internal static int FloorPercent = 10;

		/// <summary>
		/// Percent chance, rolled once per block dragged, to start the game's own bleed on a
		/// crawler that is not already bleeding. 0 switches the bleed off. A bleed that is already
		/// running - ours or a blade's - is never refreshed or stacked by this mod.
		/// </summary>
		internal static float BleedChance = 10f;

		/// <summary>
		/// Credit drag damage and the bleed to the player being chased, so a crawler that dies of
		/// it counts as that player's kill and pays XP. Off by default: the player did nothing to
		/// earn a kill from a crawler that merely chased them, so the damage and the bleed are
		/// owned by nobody (instigator -1, the engine's own "no one").
		/// </summary>
		internal static bool CreditPlayer = false;

		// The per-partner flavor switches live in FlavorSwitches: one per mod this one links up
		// with, all on by default, and mirrored pairwise rather than as one shared value.

		/// <summary>
		/// Percent chance, rolled once per block dragged and half a block out of step with
		/// <see cref="BleedChance"/> so the two never roll on the same block, to hand a crawler
		/// with one of the player's arrows stuck in it to FletchWounds for its own arrow effect.
		/// Only a crawler that is not already bleeding is handed over, so no running bleed is ever
		/// added to or refreshed. 0 switches it off. Needs FletchWounds and its flavor switch.
		/// </summary>
		internal static float ArrowBleedChance = 10f;
	}
}
