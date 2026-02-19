using System.Text;

namespace MiniIT.Snipe.Unity.Editor
{
	/// <summary>
	/// Generates a strongly-typed <c>SnipeApiService.cs</c> bindings file from API specs JSON
	/// for Snipe v8
	/// </summary>
	public class SnipeApiGeneratorV8 : SnipeApiGenerator
	{
		// protected override void GenerateLegacyResponseEvents(StringBuilder sb, MetagenResponse response, MetagenModule module)
		// {
		// 	if (response.msgType is "chat.msg" or "clan.msg")
		// 	{
		// 		string msgType = response.msgType;
		// 		string callName = response.callName;
		// 		response.msgType += ".legacy";
		// 		response.callName = "Msg";
		// 		GenerateResponseEvent(sb, response, module, callName);
		//
		// 		// Restore values for later use
		// 		response.msgType = msgType;
		// 		response.callName = callName;
		// 	}
		// 	else if (module.name == "Room")
		// 	{
		// 		string msgType = response.msgType;
		// 		string callName = response.callName;
		// 		response.msgType += ".legacy";
		// 		response.callName = response.callName.Replace("Room", "");
		// 		GenerateResponseEvent(sb, response, module, callName);
		//
		// 		// Restore values for later use
		// 		response.msgType = msgType;
		// 		response.callName = callName;
		// 	}
		// }

		protected override bool IsLegacyResponse(MetagenResponse response, MetagenModule module, out string legacyCallName, out string legacyEventName)
		{
			if (response.msgType is "chat.msg" or "clan.msg")
			{
				legacyCallName = "Msg";
				legacyEventName = "OnMsg";
				return true;
			}

			// if (module.name == "Room")
			// {
			// 	legacyCallName = response.callName.Replace("Room", "");
			// 	legacyEventName = legacyCallName;
			// 	return true;
			// }

			legacyCallName = null;
			legacyEventName = null;
			return false;
		}

		protected override void GenerateLegacyMessageReceived(StringBuilder sb, MetagenResponse response, MetagenModule module)
		{
			if (response.msgType is "chat.msg" or "clan.msg")
			{
				Indent(sb, 4).Append("OnMsg").Append("?.Invoke(");
				bool firstParam = true;
				foreach (var field in response.fields)
				{
					if (!firstParam)
					{
						sb.Append(", ");
					}
					firstParam = false;

					sb.Append(field.name);
				}
				sb.AppendLine(");");
			}
			// else if (module.name == "Room")
			// {
			// 	string legacyCallName = response.callName.Replace("Room", "");
			// 	Indent(sb, 4).Append(legacyCallName).Append("?.Invoke(");
			// 	bool firstParam = true;
			// 	foreach (var field in response.fields)
			// 	{
			// 		if (!firstParam)
			// 		{
			// 			sb.Append(", ");
			// 		}
			// 		firstParam = false;
			// 		sb.Append(field.name);
			// 	}
			// 	sb.AppendLine(");");
			// }
		}

		protected override void GenerateLegacyInvocationNoFields(StringBuilder sb, MetagenResponse response, MetagenModule module)
		{
			if (response.msgType is "chat.msg" or "clan.msg")
			{
				Indent(sb, 4).Append("OnMsg").AppendLine("?.Invoke(errorCode);");
			}
			// else if (module.name == "Room")
			// {
			// 	string legacyCallName = response.callName.Replace("Room", "");
			// 	Indent(sb, 4).Append(legacyCallName).AppendLine("?.Invoke(errorCode);");
			// }
		}
	}
}
