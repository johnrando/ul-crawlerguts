using System;
using System.Reflection;
using HarmonyLib;

namespace CrawlerGuts
{
	/// <summary>
	/// The optional other half of a drag: with FletchWounds installed, a crawler dragging itself
	/// along with one of your arrows still in it works the arrowhead deeper, and FletchWounds'
	/// own arrow effect fires - its damage, its stack of bleed, its stab sound. Behind
	/// <c>cg flavor</c>, the family's linked switch, and <c>cg arrow {pct}</c> for the chance.
	///
	/// FletchWounds publishes two methods on <c>FletchWounds.DoorSlamInterop</c> for exactly this -
	/// <c>TryProc(EntityAlive zombie, EntityAlive attacker) : bool</c>, which finds an arrow in the
	/// zombie and runs the pull effect if there is one, and <c>SetFlavor(bool)</c> - and this binds
	/// them by reflection the way DoorSlammer does, so either mod works with the other absent.
	/// Both are bound once, into delegates, so a roll pays no reflection cost. Receivers never
	/// push a flavor change back.
	/// </summary>
	public static class FletchWoundsInterop
	{
		private const string AssemblyName = "FletchWounds";

		private const string TypeName = "FletchWounds.DoorSlamInterop";

		internal const string Label = "FletchWounds";

		/// <summary>One-line presence report for <c>cg info</c>.</summary>
		internal static string Status = "not checked";

		/// <summary>The tail of the <c>cg flavor</c> menu line, ready to print.</summary>
		internal static string FlavorSummary = "no supported mods installed";

		/// <summary>Whether the assembly was there at all, as opposed to there but unusable.</summary>
		internal static bool Present { get; private set; }

		private static Func<EntityAlive, EntityAlive, bool> proc;

		/// <summary>Whether the proc is bound and a roll will reach it.</summary>
		internal static bool Wired => proc != null;

		/// <summary>FletchWounds' own flavor setter, bound the same way it binds DoorSlammer's.</summary>
		private static Action<bool> setTheirs;

		/// <summary>Whether the arrow roll can do anything at all right now.</summary>
		internal static bool Active => proc != null && Settings.Flavor && Settings.ArrowBleedChance > 0f;

		/// <summary>
		/// Hand a crawler over. FletchWounds looks for one of the player's arrows in it and, if
		/// there is one, runs its pull effect credited to that player. Returns whether it did.
		/// </summary>
		internal static bool TryProc(EntityAlive _crawler, EntityPlayer _player)
		{
			if (proc == null)
			{
				return false;
			}

			try
			{
				return proc(_crawler, _player);
			}
			catch (Exception e)
			{
				// One throw retires the bridge rather than repeating on every future roll.
				proc = null;
				Status = "installed, but the call threw - bridge retired for this session";
				Log.Error(Patches.LogPrefix + Label + " threw during an arrow roll; its part of a "
					+ "drag is off for the rest of this session. Drag damage itself is unaffected.");
				Log.Exception(e);
				return false;
			}
		}

		/// <summary>
		/// PUBLISHED CONTRACT, the same shape as FletchWounds' and DoorSlammer's: another mod may
		/// bind this by reflection to mirror its flavor switch here. Deliberately does not push
		/// back; whoever the player typed at owns the propagation.
		/// </summary>
		public static void SetFlavor(bool _on)
		{
			Settings.Flavor = _on;
			Config.Save();
		}

		/// <summary>Mirror this mod's flavor setting onto FletchWounds. Called only from the console
		/// command.</summary>
		internal static void PushFlavor(bool _on)
		{
			if (setTheirs == null)
			{
				return;
			}

			try
			{
				setTheirs(_on);
			}
			catch (Exception e)
			{
				setTheirs = null;
				Log.Warning(Patches.LogPrefix + "Could not mirror the flavor switch to " + Label
					+ "; set it there by hand. " + e.Message);
			}
		}

		/// <summary>The line <c>cg flavor</c> prints after toggling.</summary>
		internal static string Describe()
		{
			bool linked = setTheirs != null;
			string here = linked ? " here and in " + Label : string.Empty;

			if (!Settings.Flavor)
			{
				return "Flavor OFF" + here + " - a crawler drags for the drag damage and nothing more.";
			}

			if (!Wired)
			{
				// Installed-but-unbound is a version mismatch somebody can act on; absent is not.
				return Present
					? "Flavor ON - but " + Label + " could not be bound, so nothing changes. See 'cg info'."
					: "Flavor ON - but no mod that hooks into it is installed, so nothing changes.";
			}

			string text = "Flavor ON" + here + " - a crawler dragging itself with one of your arrows "
				+ "in it now works the arrowhead deeper as it goes.";
			if (!linked)
			{
				text += " " + Label + " needs 'fw flavor' on too.";
			}
			return text;
		}

		/// <summary>
		/// Binds FletchWounds' proc and links the two flavor switches. Every failure degrades to a
		/// log line and an inert bridge: the drag itself never depends on this.
		/// </summary>
		internal static void Report()
		{
			Assembly assembly = UndeadLegacyInfo.FindAssembly(AssemblyName);
			Present = assembly != null;
			if (!Present)
			{
				Status = "not installed";
				return;
			}

			try
			{
				Type type = assembly.GetType(TypeName, false);
				MethodInfo method = type == null
					? null
					: AccessTools.DeclaredMethod(type, "TryProc",
						new[] { typeof(EntityAlive), typeof(EntityAlive) });

				if (method == null || method.ReturnType != typeof(bool))
				{
					Status = "installed, but " + TypeName + ".TryProc did not match";
					Log.Warning(Patches.LogPrefix + Label + " is installed but " + TypeName
						+ ".TryProc could not be bound, so a drag will not reach it. Both mods still "
						+ "work; they just do not talk to each other.");
					return;
				}

				proc = (Func<EntityAlive, EntityAlive, bool>)Delegate.CreateDelegate(
					typeof(Func<EntityAlive, EntityAlive, bool>), method);
				setTheirs = BindFlavorSetter(type);

				Status = "installed - wired up";
				FlavorSummary = "enhanced mod interaction with " + Label;
				Log.Out(Patches.LogPrefix + Label + " detected: a crawler dragging itself with one of "
					+ "your arrows in it now has a separate "
					+ Config.Number(Settings.ArrowBleedChance) + "% chance per block to work it deeper. "
					+ "Toggle with 'cg flavor', tune with 'cg arrow'.");
			}
			catch (Exception e)
			{
				Status = "installed, but the lookup threw";
				Log.Warning(Patches.LogPrefix + "Could not bind " + Label + ": " + e.Message);
			}
		}

		/// <summary>
		/// Optional, and bound separately: a FletchWounds build whose TryProc binds but whose
		/// SetFlavor does not still gets the interaction, the player just has to set both switches.
		/// </summary>
		private static Action<bool> BindFlavorSetter(Type _type)
		{
			MethodInfo method = AccessTools.DeclaredMethod(_type, "SetFlavor", new[] { typeof(bool) });
			if (method == null)
			{
				Log.Warning(Patches.LogPrefix + Label + " has no flavor switch to link, so "
					+ "'cg flavor' only sets this side. Set 'fw flavor' too.");
				return null;
			}
			return (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), method);
		}
	}
}
