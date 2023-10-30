using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.IO;

using LibInput;

public static class Program 
{
	public static Event Event;

	private static int Main(string[] args) 
	{
		if (args.Length == 0 || (args.Length == 1 && (args[0] == "-h" || args[0] == "--help" || args[0] == "help"))) 
		{
			Console.WriteLine("Usage:\n\tgestures <file>");
			Console.WriteLine("\nExamples:");
			Console.WriteLine("\t#EventType Expression0=\"\" Expression1=\"\" Expression...=\"\" Command=\"\"");
			Console.WriteLine("\tKeyboardKey Expression=\"((KeyboardEvent)Program.Event).Key == KeyCode.Space\" Expression=\"((KeyboardEvent)Program.Event).State == KeyState.Pressed\" Command=\"gnome-terminal\"");
			return 0;
		}

		if (args.Length != 1) { Console.WriteLine("Invalid arguments."); return 1; }

		List<(EventType Type, string[] Expressions, string Command)> events = new List<(EventType Type, string[] Expressions, string Command)>();

		foreach (string line in 
			File.ReadAllLines(args[0])
			.Where(x => !string.IsNullOrEmpty(x) && x.TrimStart()[0] != '#')
		) 
		{
			List<string> expressions = new List<string>();
			string command = "";
			string type = "";

			for (int i = 0; i < line.Length; i++) 
			{
				if (type == "" && (line[i] == ' ' || line[i] == '\t')) { continue; }
				if (line[i] == ' ' || line[i] == '\t') { break; }
				type += line[i];
			}

			for (int index = line.IndexOf("Expression=\""); index != -1; index = line.IndexOf("Expression=\"", index + 1)) 
			{
				string expression = "";
				for (int i = index + "Expression=\"".Length; i < line.Length; i++) 
				{
					if (i == line.Length - 1) { break;}

					if (line[i] != '\\' && line[i + 1] == '"') { expression += line[i]; break; }
					if (line[i] == '\\' && line[i + 1] == '"') { continue; }
					expression += line[i];
				}

				expressions.Add(expression);
			}

			for (int i = line.IndexOf("Command=\"") + "Command=\"".Length; i < line.Length; i++) 
			{
				if (i == line.Length - 1) { break;}

				if (line[i] != '\\' && line[i + 1] == '"') { command += line[i]; break; }
				if (line[i] == '\\' && line[i + 1] == '"') { continue; }
				command += line[i];
			}

			events.Add((Enum.Parse<EventType>(type), expressions.ToArray(), command));
		}

		Mono.CSharp.Evaluator evaluator = new Mono.CSharp.Evaluator(
			new Mono.CSharp.CompilerContext(
				new Mono.CSharp.CompilerSettings(),
				new ReportPrinter()
			)
		);

		evaluator.ReferenceAssembly(Assembly.GetExecutingAssembly());
		evaluator.Run("using System");
		evaluator.Run("using System.Collections");
		evaluator.Run("using System.Collections.Generic");
		evaluator.Run("using System.Text");
		evaluator.Run("using System.Linq");
		evaluator.Run("using System.IO");
		evaluator.Run("using LibInput");

		while (true) 
		{
			if (!Event.TryGetEvent(out Event e)) { continue; }
			Program.Event = e;

			foreach ((EventType Type, string[] Expressions, string Command) eventDefinition in events) 
			{
				if (e.Type != eventDefinition.Type) { continue; }
				bool result = true;

				foreach (string exp in eventDefinition.Expressions) 
				{
					try { result &= (bool)evaluator.Evaluate(exp); }
					catch (ArgumentException) { Console.WriteLine($"Failed to resolve expression '{exp}' for event '{eventDefinition.Type}'."); return 1; }
					catch (InvalidCastException) { Console.WriteLine($"Failed to cast result to bool from expression '{exp}' for event '{eventDefinition.Type}'."); return 1; }
				}

				if (result) 
				{
					string command = eventDefinition.Command;
					string name = "";
					int length = 0;

					for (int i = 0; i < command.Length; i++) 
					{
						if (name == "" && (command[i] == ' ' || command[i] == '\t')) { length++; continue; }
						if (command[i] == ' ' || command[i] == '\t') { break; }
						name += command[i];
						length++;
					}

					Process.Start(name, command.Remove(0, length));
				}
			}
		}
	}
}

public class ReportPrinter : Mono.CSharp.ReportPrinter
{
	public override void Print(Mono.CSharp.AbstractMessage message, bool b) => Console.WriteLine(message);
}