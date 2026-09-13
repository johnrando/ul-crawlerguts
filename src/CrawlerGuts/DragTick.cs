using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrawlerGuts
{
	/// <summary>
	/// The effect itself. Every AI tick, a crawler that is locked onto a player has the ground it
	/// covered since the last tick added to a running total, and whole points of health come off
	/// as the total earns them. A half-eaten corpse dragging its guts across the ground should not
	/// get to do that for free.
	///
	/// "Crawler" is the engine's own floor-crawler walk type, 21: the spawned crawlers, any modded
	/// zombie built on that rig, and a walker that lost a leg and was turned into one mid-fight
	/// (EntityAlive.cs:6652). The wall-climbing spider zombie is walk type 22 and is left alone.
	///
	/// Distance is measured here rather than read from <c>Entity.distanceWalked</c>: the engine only
	/// writes that on a zombie from its footstep code (EntityAlive.cs:6170), which skips ticks in
	/// the air and over an air block, so it undercounts on rough ground. One stored position per
	/// crawler is enough.
	///
	/// Damage goes through <c>DamageEntity</c> so the game's own kill path runs: with the credit
	/// switch on the source carries the chased player's id, which is what sets
	/// <c>entityThatKilledMe</c> and awards the kill. Kill XP is a separate call that every vanilla
	/// caller makes itself (Explosion.cs:414-423, vehicles, projectiles), so it is made here too.
	/// </summary>
	internal static class DragTick
	{
		private const string BleedBuff = "buffInjuryBleeding";

		/// <summary>The vanilla stack counter that <c>buffInjuryBleeding</c> reads its damage from.</summary>
		private const string BleedCounter = "bleedCounter";

		/// <summary>The engine's floor-crawler walk type (RotateToGround crawlers).</summary>
		private const int CrawlerWalkType = 21;

		/// <summary>A jump in position larger than this in one tick is a teleport or a respawn, not
		/// a drag, and is not counted.</summary>
		private const float MaxStepPerTick = 5f;

		/// <summary>How far ahead of the drag roll the arrow roll runs, in metres.</summary>
		private const float ArrowRollOffset = 0.5f;

		/// <summary>A tracker not touched for this long belongs to a crawler that is gone.</summary>
		private const float StaleSeconds = 10f;

		private const float PruneEverySeconds = 5f;

		private sealed class Tracker
		{
			internal Vector3 LastPos;

			/// <summary>Fractional damage earned but not yet applied.</summary>
			internal float Owed;

			/// <summary>Metres since the last bleed roll.</summary>
			internal float SinceRoll;

			/// <summary>Metres since the last arrow roll. Starts half a block ahead of
			/// <see cref="SinceRoll"/>, so the two rolls fall on alternate half-blocks and never
			/// on the same one: a block never gets both a drag bleed and an arrow pull.</summary>
			internal float SinceArrowRoll = ArrowRollOffset;

			internal float LastSeen;
		}

		private static readonly Dictionary<int, Tracker> trackers = new Dictionary<int, Tracker>();

		private static readonly List<int> stale = new List<int>();

		private static float nextPrune;

		/// <summary>The last thing that happened, for <c>cg info</c>.</summary>
		internal static string Last = "no crawler has chased a player yet";

		internal static int TrackedNow => trackers.Count;

		internal static void OnUpdateLivePostfix(EntityAlive __instance)
		{
			if (!Settings.Enabled)
			{
				return;
			}

			if (!IsCrawler(__instance))
			{
				return;
			}

			EntityPlayer player = __instance.GetAttackTarget() as EntityPlayer;
			if (player == null)
			{
				// Lost interest: forget where it was, so the next chase starts measuring from
				// wherever it is then rather than crediting the wander in between.
				if (trackers.Count > 0)
				{
					trackers.Remove(__instance.entityId);
				}
				return;
			}

			float now = Time.time;
			if (now >= nextPrune)
			{
				Prune(now);
			}

			Vector3 pos = __instance.position;
			if (!trackers.TryGetValue(__instance.entityId, out Tracker tracker))
			{
				// First tick of a chase only establishes the origin.
				trackers[__instance.entityId] = new Tracker { LastPos = pos, LastSeen = now };
				return;
			}
			tracker.LastSeen = now;

			// Horizontal only: sliding down a slope is not dragging, and terrain following on a
			// RotateToGround rig makes y jitter.
			float dx = pos.x - tracker.LastPos.x;
			float dz = pos.z - tracker.LastPos.z;
			tracker.LastPos = pos;
			float step = Mathf.Sqrt(dx * dx + dz * dz);
			if (step <= 0f || step > MaxStepPerTick)
			{
				return;
			}

			Counters.BlocksDragged += step;
			tracker.Owed += step * Settings.DamagePerBlock;
			tracker.SinceRoll += step;
			tracker.SinceArrowRoll += step;

			if (tracker.SinceRoll >= 1f)
			{
				tracker.SinceRoll -= 1f;
				RollBleed(__instance, player);
			}

			if (tracker.SinceArrowRoll >= 1f)
			{
				tracker.SinceArrowRoll -= 1f;
				RollArrowPull(__instance, player);
			}

			int hit = (int)tracker.Owed;
			if (hit < 1)
			{
				return;
			}
			tracker.Owed -= hit;
			Damage(__instance, player, hit);
		}

		/// <summary>
		/// The zombie gate, as Stumblr's, but keeping crawlers instead of skipping them. EntityFlags
		/// comes from entityclasses.xml, so unlike a type check it excludes bandits and covers
		/// modded zombies. Remote entities are skipped because the attack target and health are
		/// authoritative-side; this runs on the host or server.
		/// </summary>
		private static bool IsCrawler(EntityAlive _entity)
		{
			if (_entity == null || (_entity.entityFlags & EntityFlags.Zombie) == EntityFlags.None)
			{
				return false;
			}
			if (_entity.isEntityRemote || _entity.IsDead())
			{
				return false;
			}
			return _entity.walkType == CrawlerWalkType;
		}

		/// <summary>
		/// One roll per block. Only a crawler with no bleed at all gets one, and then at the lowest
		/// tier: the counter is set rather than bumped, and never touched while any bleed is
		/// running, so a blade's stacked bleed is never reduced, refreshed or extended by this.
		/// </summary>
		private static void RollBleed(EntityAlive _crawler, EntityPlayer _player)
		{
			if (Settings.BleedChance <= 0f || _crawler.rand.RandomFloat * 100f >= Settings.BleedChance)
			{
				return;
			}
			if (StartBleed(_crawler, _player))
			{
				Counters.BleedsStarted++;
				Last = _crawler.EntityName + " started bleeding";
			}
		}

		/// <summary>
		/// The FletchWounds interaction, on the half-blocks the drag roll skips: a crawler with one
		/// of the player's arrows still in it works the arrowhead deeper, which is FletchWounds'
		/// own pull effect - its damage, its stack of bleed, its sound - rather than anything of
		/// this mod's. FletchWounds does the arrow search, so this only rolls and hands over.
		///
		/// A crawler that is already bleeding is skipped: FletchWounds' bleed stacks and refreshes
		/// the way a blade does, and this mod's rule is that no bleed already running - a blade's,
		/// the drag roll's, or FletchWounds' own - is ever touched by anything it starts.
		/// </summary>
		private static void RollArrowPull(EntityAlive _crawler, EntityPlayer _player)
		{
			if (!FletchWoundsInterop.Active
				|| _crawler.rand.RandomFloat * 100f >= Settings.ArrowBleedChance)
			{
				return;
			}
			if (_crawler.Buffs.HasBuff(BleedBuff))
			{
				return;
			}
			if (FletchWoundsInterop.TryProc(_crawler, _player))
			{
				Counters.ArrowProcs++;
				Last = _crawler.EntityName + " worked " + _player.EntityName + "'s arrow deeper";
			}
		}

		/// <summary>
		/// One stack of the game's own bleed on a crawler that has none. The counter is set rather
		/// than bumped, and nothing is touched while any bleed is running, so a blade's stacked
		/// bleed is never reduced, refreshed or extended by this.
		/// </summary>
		private static bool StartBleed(EntityAlive _crawler, EntityPlayer _player)
		{
			if (_crawler.Buffs.HasBuff(BleedBuff))
			{
				return false;
			}

			// Both calls net-sync themselves. An absent buff definition returns FailedInvalidName
			// rather than throwing.
			_crawler.Buffs.SetCustomVar(BleedCounter, 1f);
			int instigator = Settings.CreditPlayer ? _player.entityId : -1;
			return _crawler.Buffs.AddBuff(BleedBuff, instigator) == EntityBuffs.BuffStatus.Added;
		}

		private static void Damage(EntityAlive _crawler, EntityPlayer _player, int _hit)
		{
			// The floor is a cap on the hit, not a gate in front of it. It is never below 1 while
			// a percentage is set: a strength that merely ties current health is treated as fatal
			// by damageEntityLocal (EntityAlive.cs:4702), so 1 HP is the lowest "alive" the cap
			// can leave.
			int floor = 0;
			if (Settings.FloorPercent > 0)
			{
				floor = Math.Max(1, Mathf.CeilToInt(_crawler.GetMaxHealth() * Settings.FloorPercent / 100f));
			}
			int headroom = _crawler.Health - floor;
			if (headroom <= 0)
			{
				Counters.SparedByFloor++;
				return;
			}
			int strength = Math.Min(_hit, headroom);

			// BloodLoss: not a stun type, so it never accumulates into a knockdown, and at strength
			// 1 it fails the PainHit threshold too, so no hit reaction. _impulseScale 0 means no
			// knockback. With the credit switch on the source owns the player's id, which is the
			// only thing that makes a death count as their kill.
			//
			// Internal, as the game's own bleed and fall damage are, rather than External: a hit
			// credited to the local player from more than 10 m away while they hold anything
			// tagged "ranged" plays the hit-marker thud in their head (EntityAlive.cs:4600-4612),
			// and only Internal is exempt - otherwise a bow in hand made every drag tick thud.
			// Internal also means AffectedByArmor() is false, so armour cannot round the damage
			// down and 'cg dmg' means what it says, and it skips the per-source 30-tick throttle
			// that External shares with unrelated damage.
			DamageSource source = Settings.CreditPlayer
				? new DamageSourceEntity(EnumDamageSource.Internal, EnumDamageTypes.BloodLoss, _player.entityId)
				: new DamageSource(EnumDamageSource.Internal, EnumDamageTypes.BloodLoss);
			source.DismemberChance = 0f;

			int applied = _crawler.DamageEntity(source, strength, _criticalHit: false, _impulseScale: 0f);
			if (applied < 0)
			{
				// Rejected outright (god mode, a friendly-fire check) rather than merely absorbed.
				return;
			}

			Counters.DamageDealt += strength;
			Last = _crawler.EntityName + " lost " + strength + " health dragging after " + _player.EntityName;

			if (Settings.CreditPlayer && _crawler.IsDead())
			{
				// AwardKill (score, quest events) ran off the damage source; XP is the caller's job.
				_player.AddKillXP(_crawler);
				Counters.KillsCredited++;
				Last = _crawler.EntityName + " bled out dragging after " + _player.EntityName;
				trackers.Remove(_crawler.entityId);
			}
		}

		private static void Prune(float _now)
		{
			nextPrune = _now + PruneEverySeconds;
			stale.Clear();
			foreach (KeyValuePair<int, Tracker> entry in trackers)
			{
				if (_now - entry.Value.LastSeen > StaleSeconds)
				{
					stale.Add(entry.Key);
				}
			}
			for (int i = 0; i < stale.Count; i++)
			{
				trackers.Remove(stale[i]);
			}
		}
	}
}
