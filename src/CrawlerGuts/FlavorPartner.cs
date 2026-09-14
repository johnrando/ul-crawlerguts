using System;
using System.Reflection;
using HarmonyLib;

namespace CrawlerGuts
{
	/// <summary>
	/// One mod this one links up with. Every mod in the family publishes the same two methods on a
	/// <c>&lt;Mod&gt;.FlavorInterop</c> type - <c>SetFlavor(string partner, bool on)</c>, and, for
	/// a mod that receives a proc, <c>TryProc(EntityAlive zombie, EntityAlive attacker, string
	/// caller) : bool</c> - and this binds them by reflection, so either side works with the other
	/// absent. Each is bound independently, in game types only, into a delegate so a call pays no
	/// reflection cost. Every failure degrades to a log line and an inert partner.
	///
	/// <see cref="FlavorPartners"/> holds the instances; this class knows nothing about the others.
	/// </summary>
	internal sealed class FlavorPartner
	{
		private const string TypeSuffix = ".FlavorInterop";

		/// <summary>The mod's name, which is also its assembly's simple name.</summary>
		internal readonly string Label;

		/// <summary>Its console command, which doubles as the short name on ours: "fw", "sb".</summary>
		internal readonly string Alias;

		/// <summary>What the pair does, as a clause: "a slam that catches a zombie drives your arrows deeper".</summary>
		internal readonly string Effect;

		/// <summary>The same, short enough for the menu line.</summary>
		internal readonly string Menu;

		/// <summary>Whether this mod calls the partner's <c>TryProc</c>, as opposed to only being called.</summary>
		internal readonly bool Active;

		/// <summary>Outcome of the lookup, as reported by the info command.</summary>
		internal string Status = "not checked";

		/// <summary>Times the partner acted on what it was handed. Active partners only.</summary>
		internal int Procs;

		private Func<EntityAlive, EntityAlive, string, bool> proc;

		/// <summary>The partner's own flavor setter, bound the same way it binds ours.</summary>
		private Action<string, bool> setTheirs;

		/// <summary>Whether the assembly was there at all, as opposed to there but unusable.</summary>
		internal bool Found { get; private set; }

		/// <summary>Whether the proc is bound and a call will reach it.</summary>
		internal bool Wired => proc != null;

		/// <summary>Whether a toggle here reaches the partner's switch for us.</summary>
		internal bool Linked => setTheirs != null;

		internal FlavorPartner(string _label, string _alias, bool _active, string _menu, string _effect)
		{
			Label = _label;
			Alias = _alias;
			Active = _active;
			Menu = _menu;
			Effect = _effect;
		}

		/// <summary>Whether a token names this partner, by label or alias.</summary>
		internal bool Matches(string _token)
		{
			return string.Equals(_token, Label, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(_token, Alias, StringComparison.OrdinalIgnoreCase);
		}

		internal void Resolve()
		{
			Assembly assembly = UndeadLegacyInfo.FindAssembly(Label);
			Found = assembly != null;
			if (assembly == null)
			{
				Status = "not installed";
				return;
			}

			string typeName = Label + TypeSuffix;
			try
			{
				Type type = assembly.GetType(typeName, false);
				if (type == null)
				{
					Status = "installed, but has no " + typeName;
					Log.Warning(Patches.LogPrefix + Label + " is installed but has no " + typeName
						+ ", so the two mods do not talk to each other. Both still work on their own.");
					return;
				}

				setTheirs = BindFlavorSetter(type);

				if (Active)
				{
					MethodInfo method = AccessTools.DeclaredMethod(type, "TryProc",
						new[] { typeof(EntityAlive), typeof(EntityAlive), typeof(string) });
					if (method == null || method.ReturnType != typeof(bool))
					{
						Status = "installed, but " + typeName + ".TryProc did not match";
						Log.Warning(Patches.LogPrefix + Label + " is installed but " + typeName
							+ ".TryProc could not be bound, so nothing will reach it. "
							+ "Both mods still work; they just do not talk to each other.");
						return;
					}

					proc = (Func<EntityAlive, EntityAlive, string, bool>)Delegate.CreateDelegate(
						typeof(Func<EntityAlive, EntityAlive, string, bool>), method);
					Status = "installed - wired up";
				}
				else
				{
					Status = setTheirs != null ? "installed - linked" : "installed - not linked";
				}

				Log.Out(Patches.LogPrefix + Label + " detected: " + Effect + ". Toggle with '"
					+ FlavorPartners.Command + " flavor " + Alias + "'.");
			}
			catch (Exception e)
			{
				Status = "installed, but the lookup threw";
				Log.Warning(Patches.LogPrefix + "Could not bind " + Label + ": " + e.Message);
			}
		}

		/// <summary>
		/// Bound separately: a build whose TryProc binds but whose SetFlavor does not still gets
		/// the interaction, the player just has to set the other switch themselves.
		/// </summary>
		private Action<string, bool> BindFlavorSetter(Type _type)
		{
			MethodInfo method = AccessTools.DeclaredMethod(_type, "SetFlavor",
				new[] { typeof(string), typeof(bool) });
			if (method == null)
			{
				Log.Warning(Patches.LogPrefix + Label + " has no flavor switch to link, so '"
					+ FlavorPartners.Command + " flavor " + Alias + "' does not reach it. Set its own "
					+ "flavor switch for " + FlavorSwitches.Self + " too.");
				return null;
			}
			return (Action<string, bool>)Delegate.CreateDelegate(typeof(Action<string, bool>), method);
		}

		/// <summary>Mirror this side's switch for the partner onto its switch for us. Receivers never push back.</summary>
		internal void PushFlavor(bool _on)
		{
			if (setTheirs == null)
			{
				return;
			}

			try
			{
				setTheirs(FlavorSwitches.Self, _on);
			}
			catch (Exception e)
			{
				setTheirs = null;
				Log.Warning(Patches.LogPrefix + "Could not mirror the flavor switch to " + Label
					+ "; set it there by hand. " + e.Message);
			}
		}

		/// <summary>Hand a zombie over, as this mod. Returns whether the partner acted on it.</summary>
		internal bool TryProc(EntityAlive _zombie, EntityAlive _attacker)
		{
			if (proc == null)
			{
				return false;
			}

			try
			{
				if (!proc(_zombie, _attacker, FlavorSwitches.Self))
				{
					return false;
				}
				Procs++;
				return true;
			}
			catch (Exception e)
			{
				// One throw retires the partner rather than repeating on every future call.
				proc = null;
				Status = "installed, but the call threw - retired for this session";
				Log.Error(Patches.LogPrefix + Label + " threw when handed a zombie; its part of the "
					+ "interaction is off for the rest of this session. This mod is unaffected.");
				Log.Exception(e);
				return false;
			}
		}
	}
}
