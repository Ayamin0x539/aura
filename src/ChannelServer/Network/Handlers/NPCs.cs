﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aura.Channel.Util;
using Aura.Shared.Network;
using Aura.Shared.Util;
using Aura.Channel.Network.Sending;
using Aura.Channel.Scripting;
using System.Text.RegularExpressions;
using Aura.Data.Database;
using Aura.Shared.Mabi.Const;
using Aura.Data;
using Aura.Channel.World.Entities;
using Aura.Channel.Scripting.Scripts;

namespace Aura.Channel.Network.Handlers
{
	public partial class ChannelServerHandlers : PacketHandlerManager<ChannelClient>
	{
		/// <summary>
		/// Request to talk to an NPC.
		/// </summary>
		/// <example>
		/// 0001 [0010F0000000032A] Long   : 4767482418037546
		/// </example>
		[PacketHandler(Op.NpcTalkStart)]
		public void NpcTalkStart(ChannelClient client, Packet packet)
		{
			var npcEntityId = packet.GetLong();

			// Check creature
			var creature = client.GetCreature(packet.Id);

			// Check NPC
			var target = ChannelServer.Instance.World.GetNpc(npcEntityId);
			if (target == null)
			{
				throw new SevereViolation("Creature '{0}' tried to talk to non-existing NPC '{1:X16}'.", creature.Name, npcEntityId);
			}

			// Special NPCs
			var bypassDistanceCheck = false;
			var disallow = false;
			if (npcEntityId == MabiId.Nao || npcEntityId == MabiId.Tin)
			{
				bypassDistanceCheck = creature.Temp.InSoulStream;
				disallow = !creature.Temp.InSoulStream;
			}

			// Some special NPCs require special permission.
			if (disallow)
			{
				throw new SevereViolation("Creature '{0}' tried to talk to NPC '{1}' without permission.", creature.Name, target.Name);
							}

			// Check script
			if (target.Script == null)
			{
				Send.NpcTalkStartR_Fail(creature);

				Log.Warning("Creature '{0}' tried to talk to NPC '{1}', that doesn't have a script.", creature.Name, target.Name);
				return;
			}

			// Check distance
			if (!bypassDistanceCheck && (creature.RegionId != target.RegionId || target.GetPosition().GetDistance(creature.GetPosition()) > 1000))
			{
				Send.MsgBox(creature, Localization.Get("world.too_far")); // You're too far away.
				Send.NpcTalkStartR_Fail(creature);

				Log.Warning("Creature '{0}' tried to talk to NPC '{1}' out of range.", creature.Name, target.Name);
				return;
			}

			Send.NpcTalkStartR(creature, npcEntityId);

			client.NpcSession.Start(target, creature);
		}

		/// <summary>
		/// Sent when "End Conversation" button is clicked.
		/// </summary>
		/// <remarks>
		/// Not every "End Conversation" button is the same. Some send this,
		/// others, like the one you get while the keywords are open,
		/// send an "@end" response to Select instead.
		/// </remarks>
		/// <example>
		/// 001 [0010F00000000003] Long   : 4767482418036739
		/// 002 [..............01] Byte   : 1
		/// </example>
		[PacketHandler(Op.NpcTalkEnd)]
		public void NpcTalkEnd(ChannelClient client, Packet packet)
		{
			var npcId = packet.GetLong();
			var unkByte = packet.GetByte();

			// Check creature
			var creature = client.GetCreature(packet.Id);

			// Check session
			if (!client.NpcSession.IsValid(npcId))
			{
				throw new SevereViolation("Player '{0}' tried ending invalid NPC session.", creature.Name);
			}

			client.NpcSession.Clear();
			creature.Temp.CurrentShop = null;

			Send.NpcTalkEndR(creature, npcId);
		}

		/// <summary>
		/// Sent whenever a button, other than "Continue", is pressed
		/// while the client is in "SelectInTalk" mode.
		/// </summary>
		/// <example>
		/// 001 [................] String : <result session='1837'><this type="character">4503599627370498</this><return type="string">@end</return></result>
		/// 002 [........0000072D] Int    : 1837
		/// </example>
		[PacketHandler(Op.NpcTalkSelect)]
		public void NpcTalkSelect(ChannelClient client, Packet packet)
		{
			var result = packet.GetString();
			var sessionid = packet.GetInt();

			var creature = client.GetCreature(packet.Id);

			// Check session
			if (!client.NpcSession.IsValid())
			{
				throw new SevereViolation("Player '{0}' is in invalid session.", creature.Name);
			}

			// Check result string
			var match = Regex.Match(result, "<return type=\"string\">(?<result>[^<]*)</return>");
			if (!match.Success)
			{
				throw new SevereViolation("Player '{0}' sent invalid return ({1}).", creature.Name, result);
			}

			var response = match.Groups["result"].Value;

			if (response == "@end")
			{
				try
				{
					client.NpcSession.Script.EndConversation();
				}
				catch (OperationCanceledException)
				{
					//Log.Debug("Received @end");
				}
				client.NpcSession.Clear();
				return;
			}

			// Cut @input "prefix" added for <input> element.
			if (response.StartsWith("@input"))
				response = response.Substring(7).Trim();

			// TODO: Do another keyword check, in case modders bypass the
			//   actual check below.

			if (client.NpcSession.Script.ConversationState != ConversationState.Select)
				Log.Debug("Received Select without being in Select mode ({0}).", client.NpcSession.Script.GetType().Name);

			client.NpcSession.Script.Resume(response);
		}

		/// <summary>
		/// Sent when selecting a keyword, to check the validity.
		/// </summary>
		/// <remarks>
		/// Client blocks until the server answers it.
		/// Failing it unblocks the client and makes it not send Select,
		/// effectively ignoring the keyword click.
		/// </remarks>
		/// <example>
		/// 001 [................] String : personal_info
		/// </example>
		[PacketHandler(Op.NpcTalkKeyword)]
		public void NpcTalkKeyword(ChannelClient client, Packet packet)
		{
			var keyword = packet.GetString();

			var character = client.GetCreature(packet.Id);

			// Check session
			if (!client.NpcSession.IsValid())
			{
				throw new SevereViolation("Player '{0}' sent a keyword without valid NPC session.", character.Name);
			}

			// Check keyword
			if (!character.Keywords.Has(keyword))
			{
				throw new SevereViolation("Player '{0}' tried using keyword '{1}', without knowing it.", character.Name, keyword);
			}

			Send.NpcTalkKeywordR(character, keyword);
		}

		/// <summary>
		/// Sent when buying an item from a regular NPC shop.
		/// </summary>
		/// <example>
		/// 0001 [005000CBB3152F26] Long   : 22518873019723558
		/// 0002 [..............00] Byte   : 0
		/// 0003 [..............00] Byte   : 0
		/// </example>
		[PacketHandler(Op.NpcShopBuyItem)]
		public void NpcShopBuyItem(ChannelClient client, Packet packet)
		{
			var entityId = packet.GetLong();
			var targetPocket = packet.GetByte(); // 0:cursor, 1:inv
			var unk = packet.GetByte(); // storage gold?

			var creature = client.GetCreature(packet.Id);

			// Check session
			if (!client.NpcSession.IsValid())
			{
				throw new SevereViolation("Player '{0}' is in invalid session.", creature.Name);
			}

			// Check open shop
			if (creature.Temp.CurrentShop == null)
			{
				throw new SevereViolation("Player '{0}' tried to buy something with current shop being null.", creature.EntityIdHex);
			}

			// Get item
			var item = creature.Temp.CurrentShop.GetItem(entityId);
			if (item == null)
			{
				throw new SevereViolation("Item '{0:X16}' doesn't exist in shop.", entityId);
			}

			// The client expects the price for a full stack to be sent
			// in the ItemOptionInfo, so we have to calculate the actual price here.
			var price = item.OptionInfo.Price;
			if (item.Data.StackType == StackType.Stackable)
				price = (int)(price / (float)item.Data.StackMax * item.Amount);

			// Check gold
			if (creature.Inventory.Gold < price)
			{
				Send.MsgBox(creature, Localization.Get("world.shop_gold")); // Insufficient amount of gold.
				Send.NpcShopBuyItemR(creature, false);
			}

			var success = false;

			// Cursor
			if (targetPocket == 0)
				success = creature.Inventory.Add(item, Pocket.Cursor);
			// Inventory
			else if (targetPocket == 1)
				success = creature.Inventory.Add(item, false);

			if (success)
				creature.Inventory.RemoveGold(price);

			Send.NpcShopBuyItemR(creature, success);
		}

		/// <summary>
		/// Sent when selling an item from the inventory to a regular NPC shop.
		/// </summary>
		/// <example>
		/// 0001 [005000CBB3154E13] Long   : 22518873019731475
		/// 0002 [..............00] Byte   : 0
		/// </example>
		[PacketHandler(Op.NpcShopSellItem)]
		public void NpcShopSellItem(ChannelClient client, Packet packet)
		{
			var entityId = packet.GetLong();
			var unk = packet.GetByte();

			var creature = client.GetCreature(packet.Id);

			// Check session
			if (!client.NpcSession.IsValid())
			{
				throw new SevereViolation("Player '{0}' is in invalid session.", creature.Name);
			}

			// Check open shop
			if (creature.Temp.CurrentShop == null)
			{
				throw new SevereViolation("Player '{0}' tried to sell something with current shop being null.", creature.EntityIdHex);
			}

			// Get item
			var item = creature.Inventory.GetItem(entityId);
			if (item == null)
			{
				throw new SevereViolation("Item '{0}' doesn't exist in '{1}'s inventory.", entityId.ToString("X16"), creature.Name);
			}

			// Calculate selling price
			int sellingPrice = sellingPrice = item.OptionInfo.SellingPrice;
			if (item.Data.StackType == StackType.Sac)
			{
				// Add costs of the items inside the sac
				var stackItemData = AuraData.ItemDb.Find(item.Data.StackItem);
				if (stackItemData != null)
					sellingPrice += (int)((item.Info.Amount / (float)stackItemData.StackMax) * stackItemData.SellingPrice);
				else
					Log.Warning("Missing stack item data for '{0}'.", item.Data.StackItem);
			}
			else if (item.Data.StackType == StackType.Stackable)
			{
				// Individual price for this stack
				sellingPrice = (int)((item.Amount / (float)item.Data.StackMax) * sellingPrice);
			}

			// Remove item from inv
			if (!creature.Inventory.Remove(item))
			{
				throw new SevereViolation("Failed to remove item '{0}' from '{1}'s inventory.", entityId.ToString("X16"), creature.Name);
			}

			// Add gold
			// TODO: What if there's no space for the gold? Space check?
			// ^ No, it just goes into the temp inv. -- Xcell
			creature.Inventory.AddGold(sellingPrice);

			ChannelServer.Instance.Events.OnPlayerRemovesItem(creature, item.Info.Id, item.Info.Amount);
		}
	}
}
