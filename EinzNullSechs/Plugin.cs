using System.Collections.Generic;
using MEC;
using Smod2;
using Smod2.API;
using Smod2.Config;
using Smod2.Attributes;
using UnityEngine;
using UnityEngine.Networking;
using Random = System.Random;

namespace EinzNullSechs
{
	[PluginDetails(author = "Joker119", name = "EinzNullSechs", id = "joker.EinzNullSechs", description = "", version = "1.0.0",
		configPrefix = "ens", SmodMajor = 3, SmodMinor = 5, SmodRevision = 1)]

	public class EinzNullSechs : Plugin
	{
		public Methods Functions { get; private set; }

		[ConfigOption] public bool Enabled = true;
		[ConfigOption] public bool Portals = true;
		[ConfigOption] public bool Pocket = true;
		[ConfigOption] public bool Stalk = true;
		[ConfigOption] public bool AnnounceStalkReady = true;
		[ConfigOption] public float PocketDuration = 20f;
		[ConfigOption] public float StalkCooldown = 120f;
		[ConfigOption] public float PocketCooldown = 90f;
		[ConfigOption] public float FragResistance = 0.35f;
		[ConfigOption] public float DamageReistance = 0.75f;
		[ConfigOption] public float PortalCaptureDelay = 3.5f;
		[ConfigOption] public int[] IgnoredTeams = new[] { 0, 2, 6 };
		[ConfigOption] public int[] IgnoredRoles = new[] { 3 };
		[ConfigOption] public string InitialAnnounce =
			"SCP-106 has received several Quality of life re-works on this server. Press ~ to learn about them!";
		[ConfigOption] public string CooldownAnnounce = "That command is still on cooldown for $time seconds.";
		
		public readonly int[] AlwaysIgnore = new[] { -1, 5, 7 };
		public Dictionary<int, float> CurrentStalkCd = new Dictionary<int, float>();
		public Dictionary<int, float> CurrentPocketCd = new Dictionary<int, float>();
		public List<Player> Scp106 = new List<Player>();
		public Random Gen = new Random();
		public Vector PocketDimension = Vector.Down * 1997f;
		public List<CoroutineHandle> Coroutines = new List<CoroutineHandle>();
		public List<NetworkIdentity> Identities = new List<NetworkIdentity>();
		public Dictionary<int, List<NetworkIdentity>> Deleted = new Dictionary<int, List<NetworkIdentity>>();
		
		public float PortalDelay { get; set; }
		
		public int MaxLarryHp { get; set; }
		

		public override void Register()
		{
			AddEventHandlers(new EventHandlers(this));
			AddCommands(new[] { "" }, new Commands(this));

			Functions = new Methods(this);
		}

		public override void OnEnable()
		{
			Info(Details.name + " v." + Details.version + " has been enabled.");
		}

		public override void OnDisable()
		{
			Info(Details.name + " has been disabled.");
		}
	}
}