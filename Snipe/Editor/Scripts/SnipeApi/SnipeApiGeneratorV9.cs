using System;
using System.Globalization;
using System.Text;

namespace MiniIT.Snipe.Unity.Editor
{
	/// <summary>
	/// Generates a strongly-typed <c>SnipeApiService.cs</c> bindings file from API specs JSON
	/// for Snipe v9.0 and newer.
	/// </summary>
	public class SnipeApiGeneratorV9 : SnipeApiGenerator
	{
		protected override void GenerateExtraUsings(StringBuilder sb)
		{
			sb.AppendLine("using MiniIT.Snipe;");
		}

		protected override void GenerateContextFactory(StringBuilder sb, MetagenRoot root)
		{
			Indent(sb, 1).AppendLine("public sealed class SnipeApiContextFactory : AbstractSnipeApiContextFactory, ISnipeContextFactory");
			Indent(sb, 1).AppendLine("{");
			Indent(sb, 2).AppendLine("public SnipeApiContextFactory(ISnipeManager manager, SnipeOptionsBuilder optionsBuilder)");
			Indent(sb, 3).AppendLine(": base(manager, optionsBuilder, manager.Services) { }");
			sb.AppendLine();
			double timezoneHours = ParseTimeZoneHours(root);
			Indent(sb, 2).Append("public override TimeSpan GetServerTimeZoneOffset() => TimeSpan.FromHours(")
				.Append(timezoneHours.ToString("F1", CultureInfo.InvariantCulture)).AppendLine(");");
			sb.AppendLine();
			Indent(sb, 2).AppendLine("public override AbstractSnipeApiService CreateSnipeApiService(ISnipeCommunicator communicator, AuthSubsystem auth) => new SnipeApiService(communicator, auth);");
			Indent(sb, 1).AppendLine("}");
			sb.AppendLine();
			Indent(sb, 1).AppendLine("public sealed class SnipeApiTablesFactory : AbstractSnipeApiTablesFactory");
			Indent(sb, 1).AppendLine("{");
			Indent(sb, 2).AppendLine("public SnipeApiTablesFactory(ISnipeServices services, SnipeOptionsBuilder optionsBuilder)");
			Indent(sb, 3).AppendLine(": base(services, optionsBuilder) { }");
			sb.AppendLine();
			Indent(sb, 2).AppendLine("public override SnipeApiTables CreateSnipeApiTables()");
			Indent(sb, 2).AppendLine("{");
			Indent(sb, 3).AppendLine("EnsureDefaultTablesUrls();");
			Indent(sb, 3).AppendLine("return new SnipeTables(_services, TablesOptions);");
			Indent(sb, 2).AppendLine("}");
			Indent(sb, 1).AppendLine("}");
			sb.AppendLine();
		}

		private static double ParseTimeZoneHours(MetagenRoot root)
		{
			double timezoneHours = 3.0;
			if (string.IsNullOrEmpty(root.timeZone))
				return timezoneHours;
			if (root.timeZone.StartsWith("+") || root.timeZone.StartsWith("-"))
			{
				string sign = root.timeZone.Substring(0, 1);
				string hoursStr = root.timeZone.Substring(1, 2);
				string minutesStr = root.timeZone.Length > 3 ? root.timeZone.Substring(3, 2) : "00";
				if (int.TryParse(hoursStr, out int hours) && int.TryParse(minutesStr, out int minutes))
				{
					timezoneHours = hours + (minutes / 60.0);
					if (sign == "-")
						timezoneHours = -timezoneHours;
				}
			}
			return timezoneHours;
		}

		protected override void GenerateTablesConstructorSignature(StringBuilder sb)
		{
			Indent(sb, 2).AppendLine("public SnipeTables(ISnipeServices snipeServices, TablesOptions tablesOptions) : base(snipeServices, tablesOptions)");
		}

		protected override void GenerateServiceClassConstructor(StringBuilder sb)
		{
			Indent(sb, 2).AppendLine("public SnipeApiService(ISnipeCommunicator communicator, AuthSubsystem auth)");
		}
	}
}
