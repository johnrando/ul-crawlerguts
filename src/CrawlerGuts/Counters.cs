namespace CrawlerGuts
{
	/// <summary>
	/// Live counters behind <c>cg info</c>: the startup log proves the patch was installed, these
	/// prove crawlers are reaching it and which gate a tick died on. No locking: all writes happen
	/// on the main thread.
	/// </summary>
	internal static class Counters
	{
		/// <summary>Metres crawled while locked onto a player, summed over every crawler.</summary>
		internal static float BlocksDragged;

		/// <summary>Health actually taken off by dragging.</summary>
		internal static int DamageDealt;

		/// <summary>Damage ticks discarded because the crawler was at or below the floor.</summary>
		internal static int SparedByFloor;

		internal static int BleedsStarted;

		/// <summary>Arrow rolls FletchWounds acted on: the crawler had an arrow in it.</summary>
		internal static int ArrowProcs;

		/// <summary>Crawlers that died to drag damage with the credit switch on.</summary>
		internal static int KillsCredited;

		internal static void Reset()
		{
			BlocksDragged = 0f;
			DamageDealt = 0;
			SparedByFloor = 0;
			BleedsStarted = 0;
			ArrowProcs = 0;
			KillsCredited = 0;
		}
	}
}
