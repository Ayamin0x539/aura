﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Net.Sockets;
using Aura.Shared.Util;

namespace Aura.Shared.Network
{
	/// <summary>
	/// Normal Mabi client (Login, Channel).
	/// </summary>
	public class DefaultClient : BaseClient
	{
		protected override void EncodeBuffer(ref byte[] buffer)
		{
			// Set raw flag
			buffer[5] = 0x03;
		}

		protected override byte[] BuildPacket(Packet packet)
		{
			var result = new byte[6 + packet.GetSize() + 4]; // header + packet + checksum
			result[0] = 0x88;
			System.Buffer.BlockCopy(BitConverter.GetBytes(result.Length), 0, result, 1, sizeof(int));
			packet.Build(ref result, 6);

			return result;
		}
	}
}
