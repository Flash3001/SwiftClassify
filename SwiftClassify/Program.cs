using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SwiftClassify
{
	static class MainClass
	{
		const string SWIFT_PROTOCOL = @"SWIFT_PROTOCOL\(""(?<i>[\w\d]+)""\)\n@protocol\s(?<n>[\w\d]+)";
		const string SWIFT_CLASSE = @"SWIFT_CLASS\(""(?<i>[\w\d]+)""\)\n@interface\s(?<n>[\w\d]+)"; // \s:\s([\w\d]+)

		const string API_PROTOCOL = @"\s\[Protocol, Model\]\n\sinterface\s{0}\s";
		const string API_CLASSE = @"\s\[(?<b>BaseType\(typeof\([\w\d]+\))\)\]\n\sinterface\s{0}\s";

		const string MAPPING = @"(?<o>[\w\d]+)+=(?<n>[\w\d]+)";

		static String StringApi;
		static StringBuilder SbApi;
		static Dictionary<string, string> Mappings;

		public static void Main(string[] args)
		{
			try
			{
				if (args.Length < 2)
				{
					Console.WriteLine("Usage: SwiftClassify PATH_TO_SWIFT.H PATH_TO_API_DEFINITION.CS PATH_TO_NAMING_MAP.TXT");
					Console.ReadLine();
					return;
				}

				// Mappings
				ParseMapping(args.Length >= 3 ? File.ReadAllText(args[2]) : null);

				// Parse
				SbApi = new StringBuilder(StringApi = File.ReadAllText(args[1]));
				ModifyApi(GetItens(File.ReadAllText(args[0])));

				// Save
				File.WriteAllText(args[1].Replace(".cs", "New.cs"), SbApi.ToString());

				// Ok
				Console.WriteLine("Done");
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}

			Console.ReadLine();
		}

		public static void ParseMapping(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
			{
				Mappings = new Dictionary<string, string>();
			}
			else
			{
				var matches = new Regex(MAPPING).Matches(input)
											 .GetAllMatches()
												.Where(c => c.Success);
				foreach (var item in matches)
				{
					Console.WriteLine($"{item.Groups["o"].Value}, {item.Groups["n"].Value}");
				}

				Mappings = matches.ToDictionary(k => k.Groups["o"].Value, v => v.Groups["n"].Value);
			}
		}

		public static Interface[] GetItens(string input)
		{
			// Protocols
			var protocols = from c in new Regex(SWIFT_PROTOCOL).Matches(input).GetAllMatches()
				            where c.Success
							select new Protocol()
							{
								Name = c.Groups["n"].Value,
								CompiledName = c.Groups["i"].Value
							} as Interface;

			// Classes
			var classes = from c in new Regex(SWIFT_CLASSE).Matches(input).GetAllMatches()
						  where c.Success
						  select new Classe()
						  {
							  Name = c.Groups["n"].Value,
							  CompiledName = c.Groups["i"].Value
						  } as Interface;

			return protocols.Union(classes).ToArray();
		}

		public static void ModifyApi(Interface[] itens)
		{
			foreach (var item in itens)
			{
				item.Replace();
			}
		}

		public static Match[] GetAllMatches(this MatchCollection matches)
		{
			Match[] matchArray = new Match[matches.Count];
			matches.CopyTo(matchArray, 0);

			return matchArray;
		}


		public abstract class Interface
		{
			public string Name { get; set; }
			public string CompiledName { get; set; }

			protected string FinalName
			{
				get
				{
					if (Mappings.Keys.Contains(Name)) return Mappings[Name];

					return Name;
				}
			}

			public abstract void Replace();
		}

		public class Protocol : Interface
		{
			public override void Replace()
			{
				string oldValue = new Regex(string.Format(API_PROTOCOL, FinalName)).Match(StringApi).Value;
				string newValue = oldValue.Replace("Protocol", $@"Protocol(Name = ""{CompiledName}"")");
				SbApi.Replace(oldValue, newValue);
			}
		}

		public class Classe : Interface
		{
			public override void Replace()
			{
				var regex = new Regex(string.Format(API_CLASSE, FinalName)).Match(StringApi);
				var baseType = regex.Groups["b"].Value;

				string oldValue = regex.Value;
				string newValue = oldValue.Replace(baseType, $@"{baseType}, Name = ""{CompiledName}""");

				SbApi.Replace(oldValue, newValue);
			}
		}
	}
}
