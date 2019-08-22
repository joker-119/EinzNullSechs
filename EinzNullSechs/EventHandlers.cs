using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MEC;
using ServerMod2.API;
using Smod2.API;
using Smod2.Events;
using Smod2.EventHandlers;
using UnityEngine;
using UnityEngine.Networking;

namespace EinzNullSechs
{
	public class EventHandlers : IEventHandlerSetRole, IEventHandlerPocketDimensionDie,
		IEventHandlerPocketDimensionEnter, IEventHandlerPlayerHurt, IEventHandlerCallCommand, IEventHandlerPlayerDie,
		IEventHandlerRoundStart, IEventHandlerRoundEnd, IEventHandlerUpdate, IEventHandlerWaitingForPlayers, IEventHandler106CreatePortal
	{
		private readonly EinzNullSechs plugin;
		public EventHandlers(EinzNullSechs plugin) => this.plugin = plugin;
		private Vector lastPos;

		public void OnSetRole(PlayerSetRoleEvent ev)
		{
			if (ev.Role == Role.SCP_106 && plugin.Enabled)
			{
				ev.Player.PersonalBroadcast(10, plugin.InitialAnnounce, false);
				if (!plugin.CurrentStalkCd.ContainsKey(ev.Player.PlayerId))
					plugin.CurrentStalkCd.Add(ev.Player.PlayerId, plugin.StalkCooldown);
				if (!plugin.CurrentPocketCd.ContainsKey(ev.Player.PlayerId))
					plugin.CurrentPocketCd.Add(ev.Player.PlayerId, plugin.PocketCooldown);
				plugin.MaxLarryHp = ev.Player.GetHealth();

				ev.Player.SendConsoleMessage(
					$"SCP-106 will no longer be slowed down by walking through doors. (doors will appear to vanish when you get close to them, do not worry, you will not be banned for 'noclip' because of this) \n" +
					$"You can use the .stalk command in this console to move your portal to the location of a random player, this will also teleport you to the portal once it's been moved. {plugin.StalkCooldown} cooldown.\n" +
					$"You can use the .pocket command in this console to travel to your pocket dimension. You are only allowed to remain there for a limited period of time, and walking through any door will teleport you back to where you started. {plugin.PocketCooldown} cooldown, {plugin.PocketDuration} duration.\n" +
					$"Any player who walks ontop of your portal will have a 50% chance of being teleported to the pocket dimension. This includes SCP's.\n" +
					$"SCP-106 will not have some damage resistance against FRAG grenades.\n" + 
					$"SCP-106 will not auto-capture any human target close enough to him when he is below 10% health.\n" +
					$"SCP-106 will be healed for a small amount whenever a player dies inside the pocket dimension.");
			}
			if (!plugin.Deleted.ContainsKey(ev.Player.PlayerId))
				plugin.Deleted.Add(ev.Player.PlayerId, new List<NetworkIdentity>());
			List<Player> players = plugin.Server.GetPlayers();
			plugin.Scp106.Clear();
			foreach (Player player in players)
				if (player.TeamRole.Role == Role.SCP_106)
					plugin.Scp106.Add(player);
		}

		public void OnPocketDimensionDie(PlayerPocketDimensionDieEvent ev)
		{
			if (ev.Player.TeamRole.Team != Smod2.API.Team.SCP) return;
			
			ev.Die = false;
			if (lastPos != null)
			{
				ev.Player.Teleport(lastPos);
				lastPos = null;
			}
			else
			{
				Scp106PlayerScript script =
					((GameObject) ev.Player.GetGameObject()).GetComponent<Scp106PlayerScript>();
				if (script == null) return;
					
				Vector3 portalPosition = script.portalPosition;
				ev.Player.Teleport(portalPosition != Vector3.zero
					? new Vector(portalPosition.x, portalPosition.y, portalPosition.z)
					: plugin.Server.Map.GetRandomSpawnPoint(Role.SCP_106));
			}
		}

		public void OnPocketDimensionEnter(PlayerPocketDimensionEnterEvent ev)
		{
			if (plugin.Functions.IsInPocketDimension(ev.Attacker))
				ev.TargetPosition = ev.LastPosition;
			if (ev.Player.TeamRole.Team == Smod2.API.Team.SCP)
				plugin.Coroutines.Add(Timing.RunCoroutine(plugin.Functions.ReturnFromPocket(ev.Player, ev.LastPosition)));
		}

		public void OnPlayerHurt(PlayerHurtEvent ev)
		{
			if (ev.Player.TeamRole.Role != Role.SCP_106)
				return;
			ev.Damage *= ev.DamageType == DamageType.FRAG ? plugin.FragResistance : plugin.DamageReistance;

			if (ev.Player.GetHealth() > plugin.MaxLarryHp * 0.1f)
				plugin.Coroutines.Add(Timing.RunCoroutine(plugin.Functions.RealNHours(ev.Player)));
		}

		public void OnCallCommand(PlayerCallCommandEvent ev)
		{
			if (!plugin.Enabled)
				return;
			
			string command = ev.Command.ToLower();
			

			switch (command)
			{
				case "stalk":
				{
					if (!plugin.Stalk)
					{
						ev.ReturnMessage = "Stalking players is bad, and not enabled on this server!";
						return;
					}

					if (ev.Player.TeamRole.Role != Role.SCP_106)
					{
						ev.ReturnMessage = "Only Old-man Larry can stalk people.";
						return;
					}

					Scp106PlayerScript script =
						((GameObject) ev.Player.GetGameObject()).GetComponent<Scp106PlayerScript>();

					int cd = (int)plugin.CurrentStalkCd[ev.Player.PlayerId] - plugin.Server.Round.Duration;

					if (cd > 0)
					{
						ev.ReturnMessage = plugin.CooldownAnnounce.Replace("$time", cd.ToString());
						return;
					}

					if (!((GameObject) ev.Player.GetGameObject()).GetComponent<FallDamage>().isGrounded)
					{
						ev.ReturnMessage = "You must be on the ground to use this command.";
						return;
					}

					Vector pos = plugin.Functions.RandomPlayerLocation(script);
					if (pos == Vector.Zero)
					{
						ev.ReturnMessage = "No valid players found.";
						return;
					}

					plugin.Coroutines.Add(Timing.RunCoroutine(plugin.Functions.MovePortal(script, pos.ToVector3() - Vector3.up)));
					ev.ReturnMessage = "Portal moved. Cooldown started.";
					plugin.CurrentStalkCd[ev.Player.PlayerId] = plugin.StalkCooldown + plugin.Server.Round.Duration;
					plugin.PortalDelay = plugin.PortalCaptureDelay + plugin.Server.Round.Duration;
					return;
				}
				case "pocket":
				{
					if (!plugin.Pocket)
					{
						ev.ReturnMessage =
							"Larry's Dungeon is scary, and going there by choice is not allowed on this server.";
						return;
					}

					if (ev.Player.TeamRole.Role != Role.SCP_106)
					{
						ev.ReturnMessage = "Only Larry can decide when to go to the pocket dimension.";
						return;
					}

					if (plugin.Functions.IsInPocketDimension(ev.Player))
					{
						ev.ReturnMessage = "You are already in the dungeon!";
						return;
					}

					int cd = (int) plugin.CurrentPocketCd[ev.Player.PlayerId] - plugin.Server.Round.Duration;

					if (cd > 0)
					{
						ev.ReturnMessage = plugin.CooldownAnnounce.Replace("$time", cd.ToString());
						return;
					}
					
					lastPos = ev.Player.GetPosition();
					ev.Player.Teleport(plugin.PocketDimension);
					plugin.Coroutines.Add(Timing.RunCoroutine(plugin.Functions.ReturnFromPocket(ev.Player, lastPos)));
					plugin.CurrentPocketCd[ev.Player.PlayerId] = plugin.PocketCooldown + plugin.Server.Round.Duration;
					return;
				}
			}
		}

		public void OnPlayerDie(PlayerDeathEvent ev)
		{
			if (ev.Player.TeamRole.Role == Role.SCP_106)
				plugin.Scp106.Remove(ev.Player);
		}

		public void OnRoundStart(RoundStartEvent ev)
		{
			plugin.Coroutines.Add(Timing.RunCoroutine(plugin.Functions.CheckPositions()));
			plugin.Identities = Object.FindObjectsOfType<NetworkIdentity>().Where(f => f.GetComponent<Door>() != null).ToList();
		}

		public void OnRoundEnd(RoundEndEvent ev)
		{
			foreach (CoroutineHandle handle in plugin.Coroutines)
				Timing.KillCoroutines(handle);
			plugin.Coroutines.Clear();
			plugin.Scp106.Clear();
		}

		public void OnUpdate(UpdateEvent ev)
		{
			List<Player> players = plugin.Scp106;
			foreach (Player player in players)
			{
				if (player.TeamRole.Role != Role.SCP_106)
					plugin.Scp106.Remove(player);
				
				plugin.Functions.CheckDoorThingy(player);
			}
		}

		public void OnWaitingForPlayers(WaitingForPlayersEvent ev)
		{
			plugin.Scp106.Clear();
		}

		public void On106CreatePortal(Player106CreatePortalEvent ev)
		{
			if (ev.Position == plugin.PocketDimension)
				ev.Position = Vector.Zero;
			plugin.PortalDelay = plugin.PortalCaptureDelay + plugin.Server.Round.Duration;
		}
	}
}