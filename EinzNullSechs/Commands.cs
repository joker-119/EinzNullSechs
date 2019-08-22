using Smod2.Commands;

namespace EinzNullSechs
{
	internal class Commands : ICommandHandler
	{
		private readonly EinzNullSechs plugin;
		public Commands(EinzNullSechs plugin) => this.plugin = plugin;

		public string[] OnCall(ICommandSender sender, string[] args)
		{
			return new[] { "" };
		}

		public string GetUsage() => "";

		public string GetCommandDescription() => "";
	}
}