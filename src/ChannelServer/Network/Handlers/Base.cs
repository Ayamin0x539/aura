﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using Aura.Channel.Util;
using Aura.Shared.Network;

namespace Aura.Channel.Network.Handlers
{
	public partial class ChannelServerHandlers : PacketHandlerManager<ChannelClient>
	{
		public override void UnknownPacket(ChannelClient client, Packet packet)
		{
			base.UnknownPacket(client, packet);

			if (client.Controlling != null && client.Controlling.Region != null)
			{
				//Send.ServerMessage(client.Controlling, "Unknown action.");
				//Refresh?
			}
		}

		public override void Handle(ChannelClient client, Packet packet)
		{
			try
			{
				base.Handle(client, packet);
			}
			catch (SecurityViolationException ex)
			{
				client.Autoban.Incident(ex.Level, ex.Message);
			}
			// TODO: Catch other exceptions?
		}
	}
}
