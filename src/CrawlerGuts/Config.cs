using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CrawlerGuts
{
	/// <summary>
	/// Reads <see cref="Settings"/> back at startup and writes it out whenever a <c>cg</c> command
	/// changes something. The file lives in the game's user data folder rather than in the mod
	/// folder, so it survives a mod update. Plain <c>key = value</c> text; every line names the
	/// console command that writes it. Nothing here can stop the mod working: any failure degrades
	/// to a log line and the defaults.
	/// </summary>
	internal static class Config
	{
		private const string FolderName = "CrawlerGuts";

		private const string FileName = "settings.txt";

		/// <summary>What the last load or save did, as reported by <c>cg info</c>.</summary>
		internal static string Status = "not loaded - mod init has not run";

		/// <summary>Where the file is, once resolved. Null means it never was.</summary>
		private static string filePath;

		/// <summary>
		/// Called once from <see cref="ModApi.InitMod"/>, before the patch goes in, so the startup
		/// log reports the player's settings rather than the defaults. A missing file is a first
		/// run: writing the defaults out is what makes the file discoverable.
		/// </summary>
		internal static void Load()
		{
			if (!Resolve())
			{
				return;
			}

			if (!File.Exists(filePath))
			{
				Save();
				return;
			}

			try
			{
				int applied = 0;
				int rejected = 0;
				foreach (string line in File.ReadAllLines(filePath))
				{
					switch (Parse(line))
					{
					case LineResult.Applied:
						applied++;
						break;
					case LineResult.Rejected:
						rejected++;
						break;
					}
				}

				Status = applied + " settings loaded"
					+ (rejected > 0 ? ", " + rejected + " line(s) ignored" : "")
					+ " - " + filePath;
				Log.Out(Patches.LogPrefix + "Settings loaded from " + filePath + ".");
			}
			catch (Exception e)
			{
				Status = "NOT LOADED - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not read " + filePath + ", so the built-in "
					+ "defaults are in force: " + e.Message);
			}
		}

		/// <summary>
		/// Writes the whole file, which is what keeps the comments and ordering intact. Called by
		/// every <c>cg</c> command that changes a setting.
		/// </summary>
		internal static void Save()
		{
			if (!Resolve())
			{
				return;
			}

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(filePath));
				File.WriteAllText(filePath, Compose());
				Status = "saved - " + filePath;
			}
			catch (Exception e)
			{
				Status = "NOT SAVED - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not write " + filePath + ", so this change "
					+ "will not survive a restart: " + e.Message);
			}
		}

		/// <summary>Works out where the file goes, once.</summary>
		private static bool Resolve()
		{
			if (filePath != null)
			{
				return true;
			}

			try
			{
				string dir = GameIO.GetUserGameDataDir();
				if (string.IsNullOrEmpty(dir))
				{
					Status = "unavailable - the game reported no user data folder";
					return false;
				}
				filePath = Path.Combine(Path.Combine(dir, FolderName), FileName);
				return true;
			}
			catch (Exception e)
			{
				Status = "unavailable - " + e.Message;
				Log.Warning(Patches.LogPrefix + "Could not work out where to keep settings, so they "
					+ "will not persist: " + e.Message);
				return false;
			}
		}

		private static string Compose()
		{
			StringBuilder text = new StringBuilder();
			text.AppendLine("# CrawlerGuts settings.");
			text.AppendLine("#");
			text.AppendLine("# Read once when the game starts and rewritten whenever a 'cg' command changes");
			text.AppendLine("# something, so edit this with the game closed. Every line names the command that");
			text.AppendLine("# sets it; anything after a '#' is a comment, and a line that will not parse is");
			text.AppendLine("# ignored rather than fatal.");
			text.AppendLine();
			Setting(text, "enabled", OnOff(Settings.Enabled), "cg on|off");
			Setting(text, "damage", Number(Settings.DamagePerBlock), "cg dmg {hp} - health per block dragged");
			Setting(text, "floor", Settings.FloorPercent.ToString(), "cg floor {pct} - percent of max health");
			Setting(text, "bleed", Number(Settings.BleedChance), "cg bleed {pct} - chance per block, 0 = off");
			Setting(text, "credit", OnOff(Settings.CreditPlayer), "cg credit");
			return text.ToString();
		}

		/// <summary>One setting, padded so the values and the commands each share a column.</summary>
		private static void Setting(StringBuilder _text, string _key, string _value, string _command)
		{
			_text.AppendLine(_key.PadRight(8) + "= " + _value.PadRight(8) + " # " + _command);
		}

		private enum LineResult
		{
			/// <summary>Blank or a comment.</summary>
			Skipped,

			Applied,

			Rejected
		}

		/// <summary>
		/// One line of the file. An unknown key is a warning rather than an error: that is what a
		/// file written by a newer version of the mod looks like to an older one.
		/// </summary>
		private static LineResult Parse(string _line)
		{
			int comment = _line.IndexOf('#');
			string text = (comment >= 0 ? _line.Substring(0, comment) : _line).Trim();
			if (text.Length == 0)
			{
				return LineResult.Skipped;
			}

			int split = text.IndexOf('=');
			if (split <= 0)
			{
				Log.Warning(Patches.LogPrefix + "Ignoring a settings line that is not 'key = value': "
					+ _line.Trim());
				return LineResult.Rejected;
			}

			string key = text.Substring(0, split).Trim().ToLowerInvariant();
			string value = text.Substring(split + 1).Trim();
			if (Apply(key, value))
			{
				return LineResult.Applied;
			}

			Log.Warning(Patches.LogPrefix + "Ignoring settings line '" + _line.Trim()
				+ "' - unknown setting or unusable value.");
			return LineResult.Rejected;
		}

		private static bool Apply(string _key, string _value)
		{
			switch (_key)
			{
			case "enabled":
				return TryBool(_value, ref Settings.Enabled);
			case "damage":
				return LoadMeasure(_value, ref Settings.DamagePerBlock);
			case "floor":
				return LoadPercentCount(_value, ref Settings.FloorPercent);
			case "bleed":
				return LoadPercent(_value, ref Settings.BleedChance);
			case "credit":
				return TryBool(_value, ref Settings.CreditPlayer);
			default:
				return false;
			}
		}

		/// <summary>Accepts what the file writes plus the obvious hand-edit synonyms.</summary>
		private static bool TryBool(string _value, ref bool _target)
		{
			switch (_value.ToLowerInvariant())
			{
			case "on":
			case "true":
			case "yes":
			case "1":
				_target = true;
				return true;
			case "off":
			case "false":
			case "no":
			case "0":
				_target = false;
				return true;
			default:
				return false;
			}
		}

		/// <summary>A whole-number percentage, 0 to 100. Shared with the console command.</summary>
		internal static bool TryPercentCount(string _value, out int _parsed)
		{
			return int.TryParse(_value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _parsed)
				&& _parsed >= 0 && _parsed <= 100;
		}

		private static bool LoadPercentCount(string _value, ref int _target)
		{
			if (!TryPercentCount(_value, out int parsed))
			{
				return false;
			}
			_target = parsed;
			return true;
		}

		/// <summary>
		/// Health per block, zero or more, against the invariant culture so a file written on one
		/// machine means the same on one whose decimal separator is a comma. Shared with the console
		/// command. <c>!(x &gt;= 0)</c> rather than <c>x &lt; 0</c> so NaN is rejected too.
		/// </summary>
		internal static bool TryMeasure(string _value, out float _parsed)
		{
			return float.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out _parsed)
				&& _parsed >= 0f && !float.IsInfinity(_parsed);
		}

		private static bool LoadMeasure(string _value, ref float _target)
		{
			if (!TryMeasure(_value, out float parsed))
			{
				return false;
			}
			_target = parsed;
			return true;
		}

		/// <summary>A percentage that may be fractional, 0 to 100. Shared with the console command.</summary>
		internal static bool TryPercent(string _value, out float _parsed)
		{
			return TryMeasure(_value, out _parsed) && _parsed <= 100f;
		}

		private static bool LoadPercent(string _value, ref float _target)
		{
			if (!TryPercent(_value, out float parsed))
			{
				return false;
			}
			_target = parsed;
			return true;
		}

		private static string OnOff(bool _on)
		{
			return _on ? "on" : "off";
		}

		/// <summary>Written the way it is parsed, so a reported value can be typed back in.</summary>
		internal static string Number(float _value)
		{
			return _value.ToString(CultureInfo.InvariantCulture);
		}
	}
}
