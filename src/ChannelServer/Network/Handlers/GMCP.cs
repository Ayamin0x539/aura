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

namespace Aura.Channel.Network.Handlers
{
	public partial class ChannelServerHandlers : PacketHandlerManager<ChannelClient>
	{
		/// <summary>
		/// Sent when closing the GMCP.
		/// </summary>
		/// <example>
		/// No parameters.
		/// </example>
		[PacketHandler(Op.GmcpClose)]
		public void GmcpClose(ChannelClient client, Packet packet)
		{
			// Log it?
		}

		/// <summary>
		/// Summoning a character
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.GmcpSummon)]
		public void GmcpSummon(ChannelClient client, Packet packet)
		{
			var targetName = packet.GetString();

			var creature = client.GetCreature(packet.Id);

			if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth) // You're not authorized to use the GMCP.
			{
				throw new SevereViolation("'{0}' tried to use GM Summon without proper authorization", creature.Name);
			}

			var target = ChannelServer.Instance.World.GetPlayer(targetName);
			if (target == null)
			{
				Send.MsgBox(creature, Localization.Get("gm.gmcp_nochar"), targetName); // Character '{0}' couldn't be found.
				return;
			}

			var pos = creature.GetPosition();
			target.Warp(creature.RegionId, pos.X, pos.Y);

			Send.ServerMessage(target, Localization.Get("gm.gmcp_summon"), creature.Name); // You've been summoned by '{0}'.
		}

		/// <summary>
		/// Warping to creature
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.GmcpMoveToChar)]
		public void GmcpMoveToChar(ChannelClient client, Packet packet)
		{
			var targetName = packet.GetString();

			var creature = client.GetCreature(packet.Id);

			if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth) // You're not authorized to use the GMCP.
			{
				throw new SevereViolation("'{0}' tried to use GM Move To without proper authorization", creature.Name);
			}

			var target = ChannelServer.Instance.World.GetCreature(targetName);
			if (target == null)
			{
				Send.MsgBox(creature, Localization.Get("gm.gmcp_nochar"), targetName); // Character '{0}' couldn't be found.
				return;
			}

			var pos = target.GetPosition();
			creature.Warp(target.RegionId, pos.X, pos.Y);
		}

		/// <summary>
		/// Sent when clicking mini-map while GMCP is open.
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.GmcpWarp)]
		public void GmcpWarp(ChannelClient client, Packet packet)
		{
			var regionId = packet.GetInt();
			var x = packet.GetInt();
			var y = packet.GetInt();

			var creature = client.GetCreature(packet.Id);

			if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth)
			{
				throw new SevereViolation("'{0}' tried to use GM Minimap Warp without proper authorization", creature.Name);
			}

			creature.Warp(regionId, x, y);
		}

		/// <summary>
		/// Reviving via GMCP
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.GmcpRevive)]
		public void GmcpRevive(ChannelClient client, Packet packet)
		{
			var creature = client.GetCreature(packet.Id);
			if (!creature.IsDead) return;

			if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth)
			{
				throw new SevereViolation("'{0}' tried to use GM Revive without proper authorization", creature.Name);
			}

			creature.FullHeal();
			creature.Revive();
		}

		/// <summary>
		/// The invisible ma- GM.
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.GmcpInvisibility)]
		public void GmcpInvisibility(ChannelClient client, Packet packet)
		{
			var activate = packet.GetBool();

			var creature = client.GetCreature(packet.Id);

			if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth)
			{
				throw new SevereViolation("'{0}' tried to use GM Invisibility without proper authorization", creature.Name);
			}

			if (activate)
				creature.Conditions.Activate(ConditionsA.Invisible);
			else
				creature.Conditions.Deactivate(ConditionsA.Invisible);

			Send.GmcpInvisibilityR(creature, true);
		}

		/// <summary>
		/// Kills connection of target.
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.GmcpExpel)]
		public void GmcpExpel(ChannelClient client, Packet packet)
		{
			var targetName = packet.GetString();

			var creature = client.GetCreature(packet.Id);

			if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth) // You're not authorized to use the GMCP.
			{
				throw new SevereViolation("'{0}' tried to use GM Expel without proper authorization", creature.Name);
			}

			var target = ChannelServer.Instance.World.GetPlayer(targetName);
			if (target == null)
			{
				Send.MsgBox(creature, Localization.Get("gm.gmcp_nochar"), targetName); // Character '{0}' couldn't be found.
				return;
			}

			// Better kill the connection, modders could bypass a dc request.
			target.Client.Kill();

			Send.MsgBox(creature, Localization.Get("gm.gmcp_kicked"), targetName); // '{0}' has been kicked.
		}

		/// <summary>
		/// Bans target
		/// </summary>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.GmcpBan)]
		public void GmcpBan(ChannelClient client, Packet packet)
		{
			var targetName = packet.GetString();
			var duration = packet.GetInt();
			var reason = packet.GetString();

			var creature = client.GetCreature(packet.Id);

			if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth) // You're not authorized to use the GMCP.
			{
				throw new SevereViolation("'{0}' tried to use GM Ban without proper authorization", creature.Name);
			}

			var target = ChannelServer.Instance.World.GetPlayer(targetName);
			if (target == null)
			{
				Send.MsgBox(creature, Localization.Get("gm.gmcp_nochar"), targetName); // Character '{0}' couldn't be found.
				return;
			}

			var end = DateTime.Now.AddMinutes(duration);
			target.Client.Account.BanExpiration = end;
			target.Client.Account.BanReason = reason;

			// Better kill the connection, modders could bypass a dc request.
			target.Client.Kill();

			Send.MsgBox(creature, Localization.Get("gm.gmcp_banned"), targetName, end); // '{0}' has been banned till '{1}'.
		}

		/// <summary>
		/// Displays a list of NPCs?
		/// </summary>
		/// <remarks>
		/// Values and types of the response are guessed,
		/// but they seem to be working. NPCs are only displayed once
		/// in the list, probably grouped if all values are the same.
		/// </remarks>
		/// <example>
		/// ...
		/// </example>
		[PacketHandler(Op.GmcpNpcList)]
		public void GmcpNpcList(ChannelClient client, Packet packet)
		{
			var creature = client.GetCreature(packet.Id);

			if (client.Account.Authority < ChannelServer.Instance.Conf.World.GmcpMinAuth) // You're not authorized to use the GMCP.
			{
				throw new SevereViolation("'{0}' tried to use GM Npc List without proper authorization", creature.Name);
			}

			var npcs = ChannelServer.Instance.World.GetAllGoodNpcs();

			Send.GmcpNpcListR(creature, npcs);
		}
	}
}
