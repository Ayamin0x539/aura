﻿// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aura.Channel.World.Entities;
using Aura.Shared.Network;
using Aura.Channel.World;
using Aura.Shared.Mabi.Const;
using E = Aura.Shared.Mabi.Const.Effect;
using Aura.Data.Database;

namespace Aura.Channel.Network.Sending
{
	public static partial class Send
	{
		/// <summary>
		/// Broadcasts Effect in range of creature.
		/// </summary>
		/// <remarks>
		/// Parameters have to be casted to the proper type, use carefully!
		/// </remarks>
		/// <param name="creature"></param>
		/// <param name="parameters"></param>
		public static void Effect(Creature creature, int effectId, params object[] parameters)
		{
			var packet = new Packet(Op.Effect, creature.EntityId);
			packet.PutInt(effectId);
			foreach (var p in parameters)
			{
				if (p is byte) packet.PutByte((byte)p);
				else if (p is bool) packet.PutByte((bool)p);
				else if (p is short) packet.PutShort((short)p);
				else if (p is int) packet.PutInt((int)p);
				else if (p is long) packet.PutLong((long)p);
				else if (p is float) packet.PutFloat((float)p);
				else if (p is string) packet.PutString((string)p);
				else
					throw new Exception("Unsupported effect parameter: " + p.GetType());
			}

			creature.Region.Broadcast(packet, creature);
		}

		/// <summary>
		/// Broadcasts skill init effect (Effect, SkillInit, "flashing")
		/// in range of creature.
		/// </summary>
		/// <param name="creature"></param>
		public static void SkillFlashEffect(Creature creature)
		{
			var packet = new Packet(Op.Effect, creature.EntityId);
			packet.PutInt(E.SkillInit);
			packet.PutString("flashing");

			creature.Region.Broadcast(packet, creature);
		}

		/// <summary>
		/// Broadcasts spawn effect (Effect, Spawn) in range of sendFrom.
		/// </summary>
		/// <param name="spawnEffect"></param>
		/// <param name="regionId"></param>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="sender"></param>
		/// <param name="sendFrom">Falls back to sender if null</param>
		public static void SpawnEffect(SpawnEffect spawnEffect, int regionId, int x, int y, Creature sender, Creature sendFrom = null)
		{
			if (sendFrom == null)
				sendFrom = sender;

			var packet = new Packet(Op.Effect, sender.EntityId);
			packet.PutInt(E.Spawn);
			packet.PutInt(regionId);
			packet.PutFloat((float)x);
			packet.PutFloat((float)y);
			packet.PutByte((byte)spawnEffect);

			sender.Region.Broadcast(packet, sendFrom);
		}

		/// <summary>
		/// Broadcasts PetAction in range of pet.
		/// </summary>
		/// <param name="pet"></param>
		/// <param name="action"></param>
		public static void PetActionEffect(Creature pet, PetAction action)
		{
			var packet = new Packet(Op.Effect, pet.EntityId);
			packet.PutInt(E.PetAction);
			packet.PutLong(pet.Master.EntityId);
			packet.PutByte((byte)action);
			packet.PutByte(0);

			pet.Region.Broadcast(packet, pet);
		}

		/// <summary>
		/// Broadcasts Effect in range of creature.
		/// </summary>
		/// <param name="creature"></param>
		/// <param name="instrument"></param>
		/// <param name="quality"></param>
		/// <param name="compressedMML"></param>
		/// <param name="rndScore"></param>
		public static void PlayEffect(Creature creature, InstrumentType instrument, PlayingQuality quality, string compressedMML, int rndScore)
		{
			var packet = new Packet(Op.Effect, creature.EntityId);
			packet.PutInt(E.PlayMusic);
			packet.PutByte(compressedMML != null); // has scroll
			if (compressedMML != null)
				packet.PutString(compressedMML);
			else
				packet.PutInt(rndScore);
			packet.PutInt(0);
			packet.PutShort(0);
			packet.PutInt(14113); // ?
			packet.PutByte((byte)quality);
			packet.PutByte((byte)instrument);
			packet.PutByte(0);
			packet.PutByte(0);
			packet.PutByte(1); // loops

			// Singing?
			//packet.PutByte(0);
			//packet.PutByte(1);

			creature.Region.Broadcast(packet, creature);
		}
	}
}
