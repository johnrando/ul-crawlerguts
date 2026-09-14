using System.Collections.Generic;

namespace CrawlerGuts
{
	/// <summary>
	/// <c>cg</c> (or <c>crawlerguts</c>). The bare command prints the settings block and changes
	/// nothing; every line of the block names the command that changes it, so it doubles as the
	/// menu. <c>cg info</c> adds the diagnostics and counters that answer "is this thing working".
	/// </summary>
	public class ConsoleCmdCrawlerGuts : ConsoleCmdAbstract
	{
		public override bool IsExecuteOnClient => false;

		public override void Execute(List<string> _params, CommandSenderInfo _senderInfo)
		{
			string command = _params.Count > 0 ? _params[0].ToLower() : string.Empty;

			switch (command)
			{
			case "":
				OutputMenu("CrawlerGuts is " + OnOff(Settings.Enabled));
				return;

			case "on":
			case "off":
				SetEnabled(command == "on");
				return;

			case "dmg":
				SetDamage(_params);
				return;

			case "floor":
				SetFloor(_params);
				return;

			case "bleed":
				SetBleed(_params);
				return;

			case "credit":
				Settings.CreditPlayer = !Settings.CreditPlayer;
				Config.Save();
				Output(Settings.CreditPlayer
					? "Credit ON - a crawler that dies of dragging counts as the chased player's kill."
					: "Credit OFF - drag damage and the bleed are owned by nobody.");
				return;

			case "flavor":
				SetFlavor(_params);
				return;

			case "arrow":
				SetArrow(_params);
				return;

			case "info":
				OutputInfo();
				return;

			case "reset":
				Counters.Reset();
				Output("Counters reset.");
				return;

			default:
				Output("Unknown option '" + _params[0]
					+ "'. Try: cg [on|off|dmg|floor|bleed|credit|flavor {mod}|arrow|info|reset]");
				return;
			}
		}

		private static void OutputMenu(string _header)
		{
			Output(_header);
			Switch("cg on|off", EnabledChoices(), "a crawler loses health for every block it drags itself after you");
			Line("cg dmg {hp}", DamageLine());
			Line("cg floor {pct}", FloorLine());
			Line("cg bleed {pct}", BleedLine());
			Switch("cg credit", CreditChoices(), "drag damage and bleed count as your kill");
			FlavorLines();
			Line("cg arrow {pct}", ArrowLine());
		}

		/// <summary>
		/// 'cg flavor' alone is a read. 'cg flavor {mod}' toggles that pair and mirrors it to that
		/// mod only; 'cg flavor on|off' sets and mirrors every pair.
		/// </summary>
		private static void SetFlavor(List<string> _params)
		{
			if (_params.Count < 2)
			{
				FlavorLines();
				return;
			}

			string arg = _params[1].ToLowerInvariant();
			if (arg == "on" || arg == "off")
			{
				bool on = arg == "on";
				FlavorSwitches.SetAll(on);
				Config.Save();
				FlavorPartners.PushAll(on);
				Output("Flavor " + OnOff(on) + " for every partner: "
					+ string.Join(", ", FlavorSwitches.Labels.ToArray()) + ".");
				return;
			}

			string label = FlavorSwitches.Resolve(_params[1]);
			if (label == null)
			{
				Output("'" + _params[1] + "' is not a partner this mod knows. Try: cg flavor ["
					+ string.Join("|", Aliases()) + "|on|off]");
				return;
			}

			bool now = !FlavorSwitches.IsOn(label);
			FlavorSwitches.Set(label, now);
			Config.Save();
			FlavorPartners.Push(label, now);
			Output(FlavorPartners.Describe(label));
		}

		/// <summary>One menu line per partner, known ones first.</summary>
		private static void FlavorLines()
		{
			foreach (string label in FlavorSwitches.Labels)
			{
				bool on = FlavorSwitches.IsOn(label);
				Switch("cg flavor " + FlavorPartners.AliasOf(label),
					Choices(Mark("on", on), Mark("off", !on)), FlavorPartners.MenuNote(label));
			}
		}

		private static string[] Aliases()
		{
			List<string> labels = FlavorSwitches.Labels;
			string[] aliases = new string[labels.Count];
			for (int i = 0; i < labels.Count; i++)
			{
				aliases[i] = FlavorPartners.AliasOf(labels[i]);
			}
			return aliases;
		}

		/// <summary>The header says whether anything moved: typing the state you were already in
		/// should not read like a change.</summary>
		private static void SetEnabled(bool _on)
		{
			bool changed = Settings.Enabled != _on;
			Settings.Enabled = _on;
			if (changed)
			{
				Config.Save();
			}
			OutputMenu("CrawlerGuts is " + (changed ? "now " : "already ") + OnOff(_on));
		}

		private static string OnOff(bool _on)
		{
			return _on ? "ON" : "OFF";
		}

		/// <summary>The menu, with the read-only lines appended in the same column.</summary>
		private static void OutputInfo()
		{
			OutputMenu("CrawlerGuts is " + OnOff(Settings.Enabled));
			Line("settings file", Config.Status);
			Line("Undead Legacy", UndeadLegacyInfo.Status);
			for (int i = 0; i < FlavorPartners.All.Length; i++)
			{
				Line(FlavorPartners.All[i].Label, FlavorPartners.All[i].Status);
			}
			Line("tick hook", Patches.TickHookStatus);
			Line("crawlers tracked now", DragTick.TrackedNow.ToString());
			Line("blocks dragged", Config.Number((float)System.Math.Round(Counters.BlocksDragged, 1)));
			Line("damage dealt", Counters.DamageDealt + " health, " + Counters.SparedByFloor
				+ " tick(s) spared by the floor");
			Line("bleeds started", Counters.BleedsStarted.ToString());
			Line("arrows worked deeper", Counters.ArrowProcs + " (by " + FlavorPartners.FletchWounds.Label + ")");
			Line("kills credited", Counters.KillsCredited.ToString());
			Line("last event", DragTick.Last);

			if (Counters.BlocksDragged <= 0f)
			{
				Output("Note: no crawler has dragged after a player yet. If 'blocks dragged' stays at");
				Output("zero while a crawler is chasing you, the hook is not live.");
			}
		}

		/// <summary>Labels padded to the longest one ("crawlers tracked now") so the block shares a column.</summary>
		private static void Line(string _label, string _value)
		{
			Output("  " + _label.PadRight(20) + ": " + _value);
		}

		/// <summary>A switch line: the choices padded to the widest set, then what the switch is for.</summary>
		private static void Switch(string _label, string _choices, string _note)
		{
			Line(_label, _choices.PadRight(14) + " - " + _note);
		}

		private static void SetDamage(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: cg dmg {hp} - currently: " + DamageLine());
				return;
			}

			if (!Config.TryMeasure(_params[1], out float damage))
			{
				Output("'" + _params[1] + "' is not a valid damage - numbers from 0 up, like 0.5.");
				return;
			}

			Settings.DamagePerBlock = damage;
			Config.Save();
			Output("Drag damage: " + DamageLine());
		}

		private static void SetFloor(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: cg floor {pct} - currently: " + FloorLine());
				return;
			}

			if (!Config.TryPercentCount(_params[1], out int floor))
			{
				Output("'" + _params[1] + "' is not a valid floor - whole numbers from 0 to 100.");
				return;
			}

			Settings.FloorPercent = floor;
			Config.Save();
			Output("Health floor: " + FloorLine());
		}

		private static void SetBleed(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: cg bleed {pct} - currently: " + BleedLine());
				return;
			}

			if (!Config.TryPercent(_params[1], out float chance))
			{
				Output("'" + _params[1] + "' is not a valid chance - numbers from 0 to 100, like 12.5.");
				return;
			}

			Settings.BleedChance = chance;
			Config.Save();
			Output("Bleed: " + BleedLine());
		}

		/// <summary>The choice list for a toggle, with the live value marked.</summary>
		private static string Choices(params string[] _options)
		{
			return "[ " + string.Join(" | ", _options) + " ]";
		}

		private static string Mark(string _option, bool _live)
		{
			return _live ? ">" + _option + "<" : _option;
		}

		private static string EnabledChoices()
		{
			return Choices(Mark("on", Settings.Enabled), Mark("off", !Settings.Enabled));
		}

		private static string CreditChoices()
		{
			return Choices(Mark("on", Settings.CreditPlayer), Mark("off", !Settings.CreditPlayer));
		}

		private static void SetArrow(List<string> _params)
		{
			if (_params.Count != 2)
			{
				Output("Usage: cg arrow {pct} - currently: " + ArrowLine());
				return;
			}

			if (!Config.TryPercent(_params[1], out float chance))
			{
				Output("'" + _params[1] + "' is not a valid chance - numbers from 0 to 100, like 12.5.");
				return;
			}

			Settings.ArrowBleedChance = chance;
			Config.Save();
			Output("Arrow: " + ArrowLine());
		}

		private static string ArrowLine()
		{
			if (Settings.ArrowBleedChance <= 0f)
			{
				return "off - an arrow in a crawler changes nothing";
			}
			string line = Config.Number(Settings.ArrowBleedChance)
				+ "% chance per block to work a FletchWounds arrow deeper, if not bleeding";
			FlavorPartner fw = FlavorPartners.FletchWounds;
			if (!fw.Found)
			{
				return line + " (FletchWounds not installed)";
			}
			if (!fw.Wired)
			{
				return line + " (FletchWounds could not be bound - see cg info)";
			}
			return FlavorSwitches.IsOn(fw.Label) ? line : line + " (cg flavor fw is off)";
		}

		private static string DamageLine()
		{
			if (Settings.DamagePerBlock <= 0f)
			{
				return "0 - dragging costs nothing";
			}
			return Config.Number(Settings.DamagePerBlock) + " health per block dragged";
		}

		private static string FloorLine()
		{
			if (Settings.FloorPercent <= 0)
			{
				return "none - dragging can kill";
			}
			return "never dragged below " + Settings.FloorPercent
				+ "% of max health (a bleed can still kill)";
		}

		private static string BleedLine()
		{
			if (Settings.BleedChance <= 0f)
			{
				return "off - dragging never starts a bleed";
			}
			return Config.Number(Settings.BleedChance)
				+ "% chance per block to start bleeding, if not already";
		}

		private static void Output(string _line)
		{
			SdtdConsole.Instance.Output(_line);
		}

		public override string[] getCommands()
		{
			return new string[2] { "cg", "crawlerguts" };
		}

		public override string getDescription()
		{
			return "Reports CrawlerGuts' settings; 'cg on' and 'cg off' switch it.";
		}

		public override string getHelp()
		{
			return "Usage: cg [on|off|dmg {hp}|floor {pct}|bleed {pct}|credit|flavor {mod}|flavor on|off"
				+ "|arrow {pct}|info|reset]"
				+ "\r\n\r\nA crawler zombie is a half-eaten corpse dragging its guts across the "
				+ "ground, so it loses a little health for every block it drags itself while it is "
				+ "locked onto a player. It costs nothing while the crawler is idle, wandering or "
				+ "chasing something that is not a player. 'Crawler' is the game's own floor-crawler "
				+ "type, so it covers the spawned crawlers, crawlers from other mods, and a walker "
				+ "that lost a leg and dropped to the floor mid-fight. The wall-climbing spider "
				+ "zombie is not a crawler."
				+ "\r\n\r\n'cg' on its own prints the settings and changes nothing - it is the "
				+ "status read, so it is safe to type when you only want to look. Each line names "
				+ "the command that changes it, so the settings block is also the menu."
				+ "\r\n\r\n'cg on' and 'cg off' are the master switch. With it off the tick hook "
				+ "returns immediately and every other setting here is inert. Saying which state you "
				+ "want rather than toggling it means the command reads the same whichever state you "
				+ "were in, and repeating it is harmless."
				+ "\r\n\r\n'cg dmg {hp}' sets the health lost per block dragged, 1 by default. It "
				+ "may be fractional: the damage owed adds up and whole points come off as they are "
				+ "earned, so 0.5 is one point every two blocks. 0 switches the drag damage off "
				+ "while leaving the bleed roll running."
				+ "\r\n\r\n'cg floor {pct}' sets the never-kill floor as a percentage of the "
				+ "crawler's max health, 10 by default - 20 health on a 200 health crawler, 30 on "
				+ "a 300 health feral. A tick takes at most the health the crawler has above it, so "
				+ "dragging whittles a crawler down to the floor and then stops, at any 'cg dmg'. "
				+ "0 removes the floor and lets dragging kill. The bleed is one of the game's own "
				+ "buffs and pays no attention to the floor, so a bleeding crawler can still die."
				+ "\r\n\r\n'cg bleed {pct}' sets the chance, rolled once per block dragged, to "
				+ "start the game's own bleed on the crawler, 10 by default and 0 to switch it off. "
				+ "Only a crawler that is not already bleeding gets one, and then at the lowest tier: "
				+ "one health a second for twenty seconds, the same as one cut from a blade. A bleed "
				+ "that is already running - from this mod or from a blade - is never refreshed, "
				+ "extended or stacked by this, so a serrated blade's stack is left exactly as the "
				+ "blade left it."
				+ "\r\n\r\n'cg credit' toggles who owns the damage, off by default. On, the drag "
				+ "damage and the bleed carry the chased player's id, so a crawler that dies of "
				+ "either is that player's kill for score, quests and XP. Off, the damage and the "
				+ "bleed are owned by nobody and a crawler that dies of them is nobody's kill - "
				+ "being chased is not the same as fighting back."
				+ "\r\n\r\n'cg flavor' lists the interactions with the other mods in this family, "
				+ "one switch per mod, all on by default, and changes nothing. 'cg flavor {mod}' "
				+ "toggles one of them by that mod's command name - 'cg flavor fw' - and 'cg flavor "
				+ "on' or 'cg flavor off' sets them all. Each pair is switched on both sides and "
				+ "toggling it in either one sets both, so 'cg flavor fw' and 'fw flavor cg' are the "
				+ "same switch; FletchWounds' other pairs are not touched. "
				+ "With FletchWounds installed, a crawler dragging itself along with one of your "
				+ "arrows still stuck in it gets a second, separate chance per block to work the "
				+ "arrowhead deeper - which is FletchWounds' own arrow effect, exactly as if you had "
				+ "pulled the arrow: its damage, its stab sound, and its one stack of bleed, all "
				+ "credited to you and set by 'fw'. It is rolled half a block out of step with "
				+ "'cg bleed', so the two never roll on the same block, and only a crawler that is "
				+ "not already bleeding is handed over, so a bleed that is already running - from a "
				+ "blade, from dragging, or from FletchWounds itself - is never added to or "
				+ "refreshed by it. Without FletchWounds it does nothing."
				+ "\r\n\r\n'cg arrow {pct}' sets that chance, 10 by default and 0 to switch it off "
				+ "while leaving the flavor switches alone."
				+ "\r\n\r\nEvery setting here takes effect immediately and is written straight to a "
				+ "settings file, so it survives a restart - and survives updating the mod, because "
				+ "the file lives in the game's user data folder next to Saves rather than in Mods. "
				+ "'cg info' prints its full path. It is plain 'key = value' text and can be edited "
				+ "by hand with the game closed; a line that will not parse is ignored rather than "
				+ "fatal, and deleting the file goes back to the built-in defaults in Settings.cs."
				+ "\r\n\r\n'cg info' prints the same block with the patch state and the counters "
				+ "added. The key line is 'blocks dragged': the startup log only proves the hook was "
				+ "installed, that number proves crawlers chasing players are reaching it. 'cg reset' "
				+ "zeroes the counters so one scenario can be measured on its own."
				+ "\r\n\r\n'crawlerguts' is an alias for 'cg'.";
		}
	}
}
