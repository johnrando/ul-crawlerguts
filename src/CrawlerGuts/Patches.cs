using System;
using System.Reflection;
using HarmonyLib;

namespace CrawlerGuts
{
	/// <summary>
	/// Installs the one Harmony patch. Resolved late and gated on its prerequisite, so a game
	/// update that moves the method degrades to a log line rather than an exception at init.
	///
	/// No load order needs declaring: UL applies its patches as a BepInEx plugin before any
	/// IModApi.InitMod runs, and ModManager loads every mod assembly before calling any InitMod.
	/// </summary>
	internal static class Patches
	{
		internal const string LogPrefix = "[CrawlerGuts] ";

		private const string HarmonyId = "CrawlerGuts";

		/// <summary>Outcome of the patch, as reported by <c>cg info</c>.</summary>
		internal static string TickHookStatus = "not applied - mod init has not run";

		private static bool applied;

		internal static void Apply()
		{
			if (applied)
			{
				return;
			}
			applied = true;
			try
			{
				ApplyPatches();
			}
			catch (Exception e)
			{
				Log.Error(LogPrefix + "Failed to apply patches; crawlers will drag for free.");
				Log.Exception(e);
			}
		}

		private static void ApplyPatches()
		{
			UndeadLegacyInfo.Report();

			Harmony harmony = new Harmony(HarmonyId);
			ApplyTickHook(harmony);
		}

		/// <summary>
		/// The AI tick, 20 times a second per living entity, on the authoritative side. UL has no
		/// patch on it (it postfixes OnUpdatePosition instead), and it is the same hook Stumblr
		/// uses for its per-zombie check.
		/// </summary>
		private static void ApplyTickHook(Harmony _harmony)
		{
			MethodInfo target = AccessTools.DeclaredMethod(typeof(EntityAlive), "OnUpdateLive");
			if (target == null)
			{
				TickHookStatus = "NOT APPLIED - EntityAlive.OnUpdateLive not found";
				Log.Error(LogPrefix + "Tick hook NOT applied: EntityAlive.OnUpdateLive could not be "
					+ "found, so crawlers will never take drag damage.");
				return;
			}

			_harmony.Patch(target, postfix: new HarmonyMethod(
				AccessTools.DeclaredMethod(typeof(DragTick), nameof(DragTick.OnUpdateLivePostfix))));

			TickHookStatus = "applied - postfix on EntityAlive.OnUpdateLive";
			Log.Out(LogPrefix + "Tick hook applied: a crawler chasing a player now loses "
				+ Config.Number(Settings.DamagePerBlock) + " health per block dragged, down to "
				+ Settings.FloorPercent + "% of its max health"
				+ (Settings.BleedChance > 0f
					? ", with a " + Config.Number(Settings.BleedChance) + "% chance per block to bleed."
					: ", with no bleed."));
		}
	}
}
