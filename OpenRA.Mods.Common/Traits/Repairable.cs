#region Copyright & License Information
/*
 * Copyright 2007-2015 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation. For more information,
 * see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Orders;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("This actor can be sent to a structure for repairs.")]
	class RepairableInfo : ITraitInfo, Requires<HealthInfo>
	{
		public readonly string[] RepairBuildings = { "fix" };
		public virtual object Create(ActorInitializer init) { return new Repairable(init.Self, this); }
	}

	class Repairable : IIssueOrder, IResolveOrder, IOrderVoice
	{
		readonly RepairableInfo info;
		readonly Health health;
		readonly AmmoPool[] ammoPools;

		public Repairable(Actor self, RepairableInfo info)
		{
			this.info = info;
			health = self.Trait<Health>();
			ammoPools = self.TraitsImplementing<AmmoPool>().ToArray();
		}

		public IEnumerable<IOrderTargeter> Orders
		{
			get
			{
				yield return new EnterAlliedActorTargeter<Building>("Repair", 5,
					target => CanRepairAt(target), _ => CanRepair() || CanRearm());
			}
		}

		public Order IssueOrder(Actor self, IOrderTargeter order, Target target, bool queued)
		{
			if (order.OrderID == "Repair")
				return new Order(order.OrderID, self, queued) { TargetActor = target.Actor };

			return null;
		}

		bool CanRepairAt(Actor target)
		{
			return info.RepairBuildings.Contains(target.Info.Name);
		}

		bool CanRearmAt(Actor target)
		{
			return info.RepairBuildings.Contains(target.Info.Name);
		}

		bool CanRepair()
		{
			return health.DamageState > DamageState.Undamaged;
		}

		bool CanRearm()
		{
			return ammoPools.Any(x => !x.Info.SelfReloads && !x.FullAmmo());
		}

		public string VoicePhraseForOrder(Actor self, Order order)
		{
			return (order.OrderString == "Repair" && CanRepair()) ? "Move" : null;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Repair")
			{
				if (!CanRepairAt(order.TargetActor) || (!CanRepair() && !CanRearm()))
					return;

				var movement = self.Trait<IMove>();
				var target = Target.FromOrder(self.World, order);
				self.SetTargetLine(target, Color.Green);

				self.CancelActivity();
				self.QueueActivity(new MoveAdjacentTo(self, target));
				self.QueueActivity(movement.MoveTo(self.World.Map.CellContaining(order.TargetActor.CenterPosition), order.TargetActor));
				if (CanRearmAt(order.TargetActor) && CanRearm())
					self.QueueActivity(new Rearm(self));

				self.QueueActivity(new Repair(order.TargetActor));

				var rp = order.TargetActor.TraitOrDefault<RallyPoint>();
				if (rp != null)
					self.QueueActivity(new CallFunc(() =>
					{
						self.SetTargetLine(Target.FromCell(self.World, rp.Location), Color.Green);
						self.QueueActivity(movement.MoveTo(rp.Location, order.TargetActor));
					}));
			}
		}
	}
}
