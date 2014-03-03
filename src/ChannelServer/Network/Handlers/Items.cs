﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aura.Channel.Util;
using Aura.Shared.Network;
using Aura.Channel.Network.Sending;
using Aura.Shared.Util;
using Aura.Shared.Mabi.Const;
using Aura.Data.Database;
using Aura.Channel.World.Entities;
using Aura.Channel.World;

namespace Aura.Channel.Network.Handlers
{
	public partial class ChannelServerHandlers : PacketHandlerManager<ChannelClient>
	{
		/// <summary>
		/// Sent when interacting with items, using the cursor.
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.ItemMove)]
		public void ItemMove(ChannelClient client, Packet packet)
		{
			var entityId = packet.GetLong();
			var source = (Pocket)packet.GetByte();
			var target = (Pocket)packet.GetByte();
			var unk = packet.GetByte();
			var targetX = packet.GetByte();
			var targetY = packet.GetByte();

			var creature = client.GetCreature(packet.Id);

			var item = creature.Inventory.GetItem(entityId);
			if (item == null)
			{
				throw new SevereAutoban(client, "'{0}' tried to move nonexisting item {1:X16}", creature.Name, entityId);
			}

			// Try to move item
			if (!creature.Inventory.Move(item, target, targetX, targetY))
			{
				Send.ItemMoveR(creature, false);
				return;
			}

			// Stop moving when changing weapons
			if ((target >= Pocket.RightHand1 && target <= Pocket.Magazine2) || (source >= Pocket.RightHand1 && source <= Pocket.Magazine2))
				creature.StopMove();

			Send.ItemMoveR(creature, true);
		}

		/// <summary>
		/// Sent when dropping an item.
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.ItemDrop)]
		public void ItemDrop(ChannelClient client, Packet packet)
		{
			var entityId = packet.GetLong();
			var unk = packet.GetByte();

			var creature = client.GetCreature(packet.Id);
			if (creature.Region == null)
				return; // TODO: Autoban?

			var item = creature.Inventory.GetItem(entityId);
			if (item == null || item.Data.Type == ItemType.Hair || item.Data.Type == ItemType.Face)
			{
				Log.Warning("Player '{0}' ({1}) tried to drop invalid item.", creature.Name, creature.EntityIdHex);
				Send.ItemDropR(creature, false);
				return;
			}

			if (!creature.Inventory.Remove(item))
			{
				Send.ItemDropR(creature, false);
				return;
			}

			//if (HandleDungeonDrop(client, creature, item))
			//    return;

			item.Drop(creature.Region, creature.GetPosition());

			Send.ItemDropR(creature, true);

			ChannelServer.Instance.Events.OnPlayerRemovesItem(creature, item.Info.Id, item.Info.Amount);
		}

		/// <summary>
		/// Sent when clicking an item on the ground, to pick it up.
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.ItemPickUp)]
		public void ItemPickUp(ChannelClient client, Packet packet)
		{
			var entityId = packet.GetLong();

			var creature = client.GetCreature(packet.Id);
			if (creature.Region == null)
				return; // TODO: Autoban?

			var item = creature.Region.GetItem(entityId);
			if (item == null)
			{
				Send.ItemPickUpR(creature, false);
				return;
			}

			var success = creature.Inventory.PickUp(item);
			if (!success)
				Send.SystemMessage(creature, Localization.Get("world.insufficient_space")); // Not enough space.

			Send.ItemPickUpR(creature, success);
		}

		/// <summary>
		/// Sent when destroying an item (right click option).
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.ItemDestroy)]
		public void ItemDestroy(ChannelClient client, Packet packet)
		{
			var itemId = packet.GetLong();

			var creature = client.GetCreature(packet.Id);

			var item = creature.Inventory.GetItem(itemId);
			if (item == null || item.Data.Type == ItemType.Hair || item.Data.Type == ItemType.Face)
			{
				Log.Warning("Player '{0}' ({1}) tried to destroy invalid item.", creature.Name, creature.EntityIdHex);
				Send.ItemDestroyR(creature, false);
				return;
			}

			if (!creature.Inventory.Remove(item))
			{
				Send.ItemDestroyR(creature, false);
				return;
			}

			Send.ItemDestroyR(creature, true);

			ChannelServer.Instance.Events.OnPlayerRemovesItem(creature, item.Info.Id, item.Info.Amount);
		}

		/// <summary>
		/// Sent when splitting stacks
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.ItemSplit)]
		public void ItemSplit(ChannelClient client, Packet packet)
		{
			var itemId = packet.GetLong();
			var amount = packet.GetUShort();
			var unk1 = packet.GetByte();

			var creature = client.GetCreature(packet.Id);

			// Check item
			var item = creature.Inventory.GetItem(itemId);
			if (item == null || item.Data.StackType == StackType.None)
			{
				Send.ItemSplitR(creature, false);
				return;
			}

			// Check requested amount
			if (item.Info.Amount < amount)
				amount = item.Info.Amount;

			// Clone item or create new one based on stack item
			Item splitItem;
			if (item.Data.StackItem == 0)
				splitItem = new Item(item);
			else
				splitItem = new Item(item.Data.StackItem);
			splitItem.Info.Amount = amount;

			// New item on cursor
			if (!creature.Inventory.Add(splitItem, Pocket.Cursor))
			{
				Send.ItemSplitR(creature, false);
				return;
			}

			// Update amount or remove
			item.Info.Amount -= amount;

			if (item.Info.Amount > 0 || item.Data.StackItem != 0)
			{
				Send.ItemAmount(creature, item);
			}
			else
			{
				creature.Inventory.Remove(item);
			}

			Send.ItemSplitR(creature, true);
		}

		/// <summary>
		/// Sent when switching weapon sets (eg, on Tab/W).
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.SwitchSet)]
		public void SwitchSet(ChannelClient client, Packet packet)
		{
			var set = (WeaponSet)packet.GetByte();

			var creature = client.GetCreature(packet.Id);

			creature.StopMove();

			creature.Inventory.WeaponSet = set;

			Send.UpdateWeaponSet(creature);
			//ChannelServer.Instance.World.CreatureStatsUpdate(creature);

			Send.SwitchSetR(creature, true);
		}

		/// <summary>
		/// Sent when changing an item state, eg hood on robes.
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.ItemStateChange)]
		public void ItemStateChange(ChannelClient client, Packet packet)
		{
			var firstTarget = (Pocket)packet.GetByte();
			var secondTarget = (Pocket)packet.GetByte();
			var unk = packet.GetByte();

			var creature = client.GetCreature(packet.Id);

			// This might not be entirely correct, but works well.
			// Robe is opened first, Helm secondly, then Robe and Helm are both closed.

			foreach (var target in new [] { firstTarget, secondTarget })
			{
				if (target > 0)
				{
					var item = creature.Inventory.GetItemAt(target, 0, 0);
					if (item != null)
					{
						item.Info.State = (byte)(item.Info.State == 1 ? 0 : 1);
						Send.EquipmentChanged(creature, item);
					}
				}
			}

			Send.ItemStateChangeR(creature);
		}

		/// <summary>
		/// Sent when using an item.
		/// </summary>
		/// <remarks>
		/// Not all usable items send this packet. The client has some
		/// items send different packets, like starting hidden skills
		/// (eg res, hw, dye, etc).
		/// </remarks>
		/// <example>
		/// 0001 [005000CBB535EFC6] Long   : 22518873055424454
		/// </example>
		[PacketHandler(Op.UseItem)]
		public void UseItem(ChannelClient client, Packet packet)
		{
			var entityId = packet.GetLong();

			var creature = client.GetCreature(packet.Id);

			// Check states
			if (creature.IsDead)
			{
				Log.Warning("Player '{0}' tried to use item while being dead.", creature.Name);
				Send.UseItemR(creature, false, 0);
				return;
			}

			// Get item
			var item = creature.Inventory.GetItem(entityId);
			if (item == null)
			{
				Log.Warning("Player '{0}' tried to use item he doesn't possess.", creature.Name);
				Send.UseItemR(creature, false, 0);
				return;
			}

			// Get script
			var script = ChannelServer.Instance.ScriptManager.GetItemScript(item.Info.Id);
			if (script == null)
			{
				Log.Unimplemented("Item script for '{0}' not found.", item.Info.Id);
				Send.UseItemR(creature, false, 0);
				Send.ServerMessage(creature, Localization.Get("aura.unimplemented_item")); // Unimplemented item.
				return;
			}

			// Use item
			script.OnUse(creature, item);
			if (item.Data.Consumed)
			{
				creature.Inventory.Decrement(item);
				ChannelServer.Instance.Events.OnPlayerRemovesItem(creature, item.Info.Id, item.Info.Amount);
			}

			// Mandatory stat update
			Send.StatUpdate(creature, StatUpdateType.Private, Stat.Life, Stat.LifeInjured, Stat.Mana, Stat.Stamina, Stat.Hunger);
			Send.StatUpdate(creature, StatUpdateType.Public, Stat.Life, Stat.LifeInjured);

			Send.UseItemR(creature, true, item.Info.Id);
		}
	}
}
