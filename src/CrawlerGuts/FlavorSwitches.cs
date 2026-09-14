using System;
using System.Collections.Generic;

namespace CrawlerGuts
{
	/// <summary>
	/// One flavor switch per partner mod, keyed by that mod's label. An interaction is live only
	/// when both ends have their switch for the other on, and toggling a pair at either end
	/// mirrors to that one partner and nowhere else - there is no hub.
	///
	/// A label that has never been set is on: that is what leaves the door open to a mod this
	/// build does not know about. Its first contact - a proc, a mirrored toggle, or a line in the
	/// settings file - gives it a switch of its own, which is then written out and shown in the
	/// menu like any other. Labels compare case-insensitively.
	/// </summary>
	internal static class FlavorSwitches
	{
		/// <summary>This mod's own label, which is what a partner sees as the caller.</summary>
		internal const string Self = "CrawlerGuts";

		private static readonly Dictionary<string, bool> switches =
			new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Labels that are not in <see cref="FlavorPartners.All"/>, as first seen.</summary>
		private static readonly List<string> discovered = new List<string>();

		/// <summary>Whether this side's switch for a partner is on. Never seen means on.</summary>
		internal static bool IsOn(string _label)
		{
			if (string.IsNullOrEmpty(_label))
			{
				return true;
			}

			if (switches.TryGetValue(_label, out bool on))
			{
				return on;
			}

			// A known partner that was never set is simply on. First contact from a mod nobody
			// told us about gets a switch, and a save so the line is there to edit - once per
			// label per session at most.
			if (FlavorPartners.Find(_label) == null)
			{
				Set(_label, true);
				Config.Save();
			}
			return true;
		}

		/// <summary>Sets one switch. Does not save and does not mirror; the caller does both.</summary>
		internal static void Set(string _label, bool _on)
		{
			string label = Canonical(_label);
			if (!switches.ContainsKey(label) && FlavorPartners.Find(label) == null)
			{
				discovered.Add(label);
				discovered.Sort(StringComparer.OrdinalIgnoreCase);
			}
			switches[label] = _on;
		}

		/// <summary>Every known and discovered switch at once.</summary>
		internal static void SetAll(bool _on)
		{
			foreach (string label in Labels)
			{
				switches[label] = _on;
			}
		}

		/// <summary>Known partners in declared order, then anything discovered, alphabetically.</summary>
		internal static List<string> Labels
		{
			get
			{
				List<string> labels = new List<string>();
				for (int i = 0; i < FlavorPartners.All.Length; i++)
				{
					labels.Add(FlavorPartners.All[i].Label);
				}
				labels.AddRange(discovered);
				return labels;
			}
		}

		/// <summary>
		/// A console token - a known partner's alias or label, or a discovered label - to the
		/// label it means. Null when it is neither, so the command can say so.
		/// </summary>
		internal static string Resolve(string _token)
		{
			if (string.IsNullOrEmpty(_token))
			{
				return null;
			}

			FlavorPartner partner = FlavorPartners.Find(_token);
			if (partner != null)
			{
				return partner.Label;
			}

			for (int i = 0; i < discovered.Count; i++)
			{
				if (string.Equals(discovered[i], _token, StringComparison.OrdinalIgnoreCase))
				{
					return discovered[i];
				}
			}

			return null;
		}

		/// <summary>The declared spelling for a known partner; the given one otherwise.</summary>
		private static string Canonical(string _label)
		{
			FlavorPartner partner = FlavorPartners.Find(_label);
			return partner != null ? partner.Label : _label;
		}
	}
}
