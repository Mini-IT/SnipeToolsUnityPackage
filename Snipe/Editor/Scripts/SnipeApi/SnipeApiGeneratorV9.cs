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

		protected override void GenerateContextFactoryConstructor(StringBuilder sb)
		{
			Indent(sb, 2).AppendLine("public SnipeApiContextFactory(ISnipeManager tablesProvider, SnipeConfigBuilder configBuilder, ISnipeServices services)");
			Indent(sb, 3).AppendLine(": base(tablesProvider, configBuilder, services) { }");
		}

		protected override void GenerateContextFactoryCreateTables(StringBuilder sb)
		{
			Indent(sb, 2).AppendLine("public SnipeApiTables CreateSnipeApiTables() => new SnipeTables(_services);");
		}

		protected override void GenerateTablesConstructorSignature(StringBuilder sb)
		{
			Indent(sb, 2).AppendLine("public SnipeTables(ISnipeServices snipeServices) : base(snipeServices)");
		}
	}
}
