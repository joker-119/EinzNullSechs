using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MEC;
using ServerMod2.API;
using Smod2.API;
using UnityEngine;
using UnityEngine.Networking;

namespace EinzNullSechs
{
	public class Methods
	{
		private readonly EinzNullSechs plugin;
		public Methods(EinzNullSechs plugin) => this.plugin = plugin;

		public IEnumerator<float> ReturnFromPocket(Player player, Vector pos)
		{
			yield return Timing.WaitForSeconds(plugin.PocketDuration);
			if (IsInPocketDimension(player))
				player.Teleport(pos);
		}

		public IEnumerator<float> MovePortal(Scp106PlayerScript script, Vector3 pos)
		{
			script.NetworkportalPosition = pos;

			yield return Timing.WaitForSeconds(0.5f);
			script.portalPrefab.transform.position = pos;
			yield return Timing.WaitForSeconds(1.5f);
			script.CallCmdUsePortal();
		}

		public Vector RandomPlayerLocation(Scp106PlayerScript script)
		{
			List<Player> players = plugin.Server.GetPlayers().Where(p =>
				!plugin.IgnoredRoles.Contains((int) p.TeamRole.Role) &&
				!plugin.IgnoredTeams.Contains((int) p.TeamRole.Team) &&
				!plugin.AlwaysIgnore.Contains((int) p.TeamRole.Role)).ToList();

			if (!players.Any())
				return Vector.Zero;

			for (int i = 0; i < players.Count; i++)
			{
				int ran = plugin.Gen.Next(players.Count);
				Physics.Raycast(new Ray(players[ran].GetPosition().ToVector3(), -Vector3.up), out RaycastHit raycastHit,
					10f, script.teleportPlacementMask);
				if (IsInPocketDimension(players[ran]))
				{
					players.RemoveAt(ran);
					continue;
				}

				if (!raycastHit.point.Equals(Vector3.zero))
					return new Vector(raycastHit.point.x, raycastHit.point.y, raycastHit.point.z);
			}

			return Vector.Zero;
		}

		public IEnumerator<float> RealNHours(Player player)
		{
			for (;;)
			{
				if (player.GetHealth() > plugin.MaxLarryHp * 0.1f)
					break;
				foreach (Player ply in plugin.Server.GetPlayers())
					if (Vector.Distance(player.GetPosition(), ply.GetPosition()) < 2f &&
					    ply.TeamRole.Team != Smod2.API.Team.SCP)
					{
						plugin.Info($"Teleporting {ply.Name}");
						ply.Teleport(Vector.Down * 1997f);
					}

				yield return Timing.WaitForSeconds(0.5f);
			}
		}

		public IEnumerator<float> CheckPositions()
		{
			for (;;)
			{
				List<Player> players = plugin.Server.GetPlayers().Where(p => p.TeamRole.Role != Role.SPECTATOR).ToList();
				if (plugin.PortalDelay - plugin.Server.Round.Duration > 0)
				{
					yield return Timing.WaitForSeconds(1f);
					continue;
				}

				foreach (Player player in players)
				{
					foreach (Player ply in plugin.Scp106)
						if (Vector.Distance(player.GetPosition(), ply.Get106Portal()) < 4f &&
						    plugin.Gen.Next(1, 100) > 50 && !plugin.Functions.IsInPocketDimension(player) && player.TeamRole.Role != Role.SCP_106)
						{
							plugin.Info($"Capturing {player.Name}");
							player.Teleport(plugin.PocketDimension);
						}
				}

				yield return Timing.WaitForSeconds(1f);
			}
		}

		public void CheckDoorThingy(Player player)
		{
			GameObject ply = Extension.GetGameObject(player);
			NetworkConnection connection = ply.GetComponent<NetworkIdentity>().connectionToClient;
			foreach (NetworkIdentity ident in plugin.Identities)
			{
				if (Vector3.Distance(ident.transform.position, ply.transform.position) < 2.99f &&
				    !plugin.Deleted[player.PlayerId].Contains(ident))
				{
					plugin.Info($"{(int) player.TeamRole.Role}");
						if (player.TeamRole.Role != Role.SCP_106)
							return;
						plugin.Deleted[player.PlayerId].Add(ident);
						DoDoorThingy(connection, ident.gameObject, true);
				}

				foreach (NetworkIdentity id in plugin.Deleted[player.PlayerId])
					if (Vector3.Distance(id.transform.position, ply.transform.position) > 3.01f)
					{
						plugin.Deleted[player.PlayerId].Remove(id);
						DoDoorThingy(connection, id.gameObject, false);
					}
			}
		}

		private void DoDoorThingy(NetworkConnection conn, GameObject door, bool delete)
		{
			if (delete)
			{
				NetworkIdentity ident = door.GetComponent<NetworkIdentity>();
				Type type = typeof(NetworkServer).Assembly.GetTypes()
					.FirstOrDefault(p => p.Name.ToLower().Contains("objectdestroymessage"));
				if (type == null)
				{
					plugin.Error("No objectdestroymessage");
					return;
				}

				object instance = Activator.CreateInstance(type);
				const short num = 1;
				SetInstanceField(type, instance, "netId", ident.netId);
				InvokeInstanceMethod(typeof(NetworkConnection), conn, "Send", new object[]
				{
					num,
					instance
				});
			}
			else
			{
				NetworkIdentity ident = door.GetComponent<NetworkIdentity>();
				InvokeInstanceMethod(typeof(NetworkServer), GetInstanceField(typeof (NetworkServer), null, "s_Instance"), "SendSpawnMessage", new object[]
				{
					ident,
					conn
				});
			}
		}

		private static object GetInstanceField(Type type, object instance, string fieldName) =>
			type.GetField(fieldName,
				BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Public |
				BindingFlags.Static)?.GetValue(instance);

		private static void SetInstanceField(
			Type type,
			object instance,
			string fieldName,
			object value)
		{
			type.GetField(fieldName, BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.SetValue(instance, value);
		}

		private static void InvokeInstanceMethod(Type type, object instance, string methodName, object[] param)
		{
			type.GetMethod(methodName,
				BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Static |
				BindingFlags.Public)
				?.Invoke(instance, param);
		}

		public bool IsInPocketDimension(Player player) => Vector.Distance(player.GetPosition(), plugin.PocketDimension) < 30f;
	}

	static class Extension
	{
		public static GameObject GetGameObject(this Player player) => (GameObject) player.GetGameObject();
	}
}