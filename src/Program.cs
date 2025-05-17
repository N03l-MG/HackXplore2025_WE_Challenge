using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Data;
using ExcelDataReader;
using Newtonsoft.Json;

namespace HackXplore2025.src;

public class Program
{
	private const string BOMS_DIRECTORY = "HackXplore_AIXrefBOM/boms";
	private const string WE_DATA_DIRECTORY = "HackXplore_AIXrefBOM/Wuerth Elektronik Data Dump";
	private const string PYTHON_SCRIPT = "search_components.py";

	static Program()
	{
		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
	}

	private class WEComponent
	{
		// Common fields
		public string? Order_Code { get; set; }
		public string? Product_Unit { get; set; }
		public string? Product_Group { get; set; }
		public string? Product_Series { get; set; }
		public string? Product_Family { get; set; }
		public string? Mount { get; set; }
		public string? Url { get; set; }
		public double? Length { get; set; }
		public double? Width { get; set; }
		public double? Height { get; set; }

		// Resistor fields
		[JsonProperty("Resistance (Ohm)")]
		public double? Resistance { get; set; }
		
		[JsonProperty("Rated_Power (W)")]
		public double? Rated_Power { get; set; }
		
		[JsonProperty("Rated_Current (A)")]
		public double? Rated_Current { get; set; }

		// Inductor fields
		[JsonProperty("Inductance (µH)")]
		public double? Inductance { get; set; }
		
		[JsonProperty("DC_Resistance (Ohm)")]
		public double? DC_Resistance { get; set; }
		
		[JsonProperty("Saturation_Current (A)")]
		public double? Saturation_Current { get; set; }

		// Capacitor fields
		[JsonProperty("Capacitance (µF)")]
		public double? Capacitance { get; set; }
		
		[JsonProperty("Rated_Voltage (V)")]
		public double? Rated_Voltage { get; set; }
	}

	private static async Task Main(string[] args)
	{
		try
		{
			if (args.Length == 0)
			{
				Console.WriteLine("Please provide the path to the BOM file as an argument.");
				Console.WriteLine("Usage: program.exe boms/bom.xlsx");
				return;
			}

			string bomFile = args[0];
			if (!File.Exists(bomFile))
			{
				Console.WriteLine($"File not found: {bomFile}");
				return;
			}

			// 1. Read BOM
			var competitorComponents = ReadBOMFiles(bomFile);

			// 2. Enrich data using Python Google Search
			var enrichedComponents = await EnrichComponentsWithGoogleSearch(competitorComponents);

			// 3. Find matching WE components
			var matches = await FindWEMatches(enrichedComponents);

			// 4. Output results
			OutputResults(matches);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error: {ex.Message}");
		}
	}

	private static List<Component> ReadBOMFiles(string filePath)
	{
		var components = new List<Component>();

		using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
		using (var reader = ExcelReaderFactory.CreateReader(stream))
		{
			var result = reader.AsDataSet();
			foreach (DataTable table in result.Tables)
			{
				for (int row = 1; row < table.Rows.Count; row++)
				{
					var component = ParseComponent(table.Rows[row]);
					if (component != null)
						components.Add(component);
				}
			}
		}
		return components;
	}

	private static Component? ParseComponent(DataRow row)
	{
		try
		{
			var type = DetermineComponentType(row["Type"].ToString()!);

			return type.ToLower() switch
			{
				"resistor" => new Resistor
				{
					type = type,
					orderCode = row["Part Number"].ToString()!,
					manufacturer = row["Manufacturer"].ToString()!
				},
				"inductor" => new Inductor
				{
					type = type,
					orderCode = row["Part Number"].ToString()!,
					manufacturer = row["Manufacturer"].ToString()!
				},
				"capacitor" => new Capacitor
				{
					type = type,
					orderCode = row["Part Number"].ToString()!,
					manufacturer = row["Manufacturer"].ToString()!
				},
				_ => null,
			};

		}
		catch
		{
			return null;
		}
	}

	private static string DetermineComponentType(string type)
	{
		type = type.ToLower();
		if (type.Contains("res")) return "resistor";
		if (type.Contains("ind") || type.Contains("choke")) return "inductor"; 
		if (type.Contains("cap")) return "capacitor";
		return "unknown";
	}

	private static async Task<List<Component>> EnrichComponentsWithGoogleSearch(List<Component> components)
	{
		// Call Python script for Google Search API
		var enrichedData = new List<Component>();

		foreach (var component in components)
		{
			var startInfo = new ProcessStartInfo
			{
				FileName = "python",
				Arguments = $"{PYTHON_SCRIPT} \"{component.orderCode}\"",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using (var process = Process.Start(startInfo))
			{
				var output = await process!.StandardOutput.ReadToEndAsync();
				// Parse JSON response from Python
				var searchData = JsonConvert.DeserializeObject<dynamic>(output);
				// Update component with search results
			}
		}

		return enrichedData;
	}

	private static async Task<List<(Component Competitor, Component WE)>> FindWEMatches(List<Component> components)
	{
		var matches = new List<(Component Competitor, Component WE)>();
		
		// Load WE component data from JSON files
		var weComponents = await LoadWEComponents();

		foreach (var component in components)
		{
			var match = FindBestMatch(component, weComponents);
			matches.Add((component, match)!);
		}

		return matches;
	}


	private static string DetermineComponentTypeFromFilename(string filename)
	{
		filename = filename.ToLower();
		if (filename.Contains("resistor")) return "resistor";
		if (filename.Contains("inductor")) return "inductor";
		if (filename.Contains("capacitor")) return "capacitor";
		return "unknown";
	}
	
	private static async Task<List<Component>> LoadWEComponents()
	{
		var components = new List<Component>();
		var jsonSettings = new JsonSerializerSettings
		{
			Error = (sender, args) =>
			{
				Console.WriteLine($"JSON Error: {args.ErrorContext.Error.Message}");
				args.ErrorContext.Handled = true;
			},
			NullValueHandling = NullValueHandling.Ignore,
			MissingMemberHandling = MissingMemberHandling.Ignore
		};

		foreach (var file in Directory.GetFiles(WE_DATA_DIRECTORY, "*.json"))
		{
			try
			{
				var json = await File.ReadAllTextAsync(file);
				var componentType = DetermineComponentTypeFromFilename(file);

				// Clean JSON before parsing
				json = CleanInvalidJson(json);

				var jsonComponents = JsonConvert.DeserializeObject<List<WEComponent>>(json, jsonSettings);

				if (jsonComponents == null)
				{
					Console.WriteLine($"No components found in {file}");
					continue;
				}

				foreach (var weComp in jsonComponents.Where(c => c != null))
				{
					try
					{
						var component = ConvertWEComponent(weComp, componentType);
						if (component != null)
						{
							components.Add(component);
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"Error converting component {weComp.Order_Code}: {ex.Message}");
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error parsing {file}: {ex.Message}");
			}
		}
	    return components;
	}

	private static string CleanInvalidJson(string json)
	{
		// Basic JSON cleaning
		return json.Replace("\r", "")
				.Replace("\n", "")
				.Replace("\t", "")
				.Replace("\\", "\\\\");
	}

	private static Component? ConvertWEComponent(WEComponent weComp, string type)
	{
		return type.ToLower() switch
		{
			"resistor" => new Resistor
			{
				id = weComp.Order_Code!,
				type = "resistor",
				orderCode = weComp.Order_Code!,
				manufacturer = "Würth Elektronik",
				url = weComp.Url!,
				resistance = weComp.Resistance ?? 0,
				ratedPower = weComp.Rated_Power ?? 0,
				ratedCurrent = weComp.Rated_Current ?? 0,
				length = weComp.Length ?? 0,
				width = weComp.Width ?? 0,
				height = weComp.Height ?? 0,
				mount = weComp.Mount!,
				series = weComp.Product_Series!
			},
			"inductor" => new Inductor
			{
				id = weComp.Order_Code!,
				type = "inductor",
				orderCode = weComp.Order_Code!,
				manufacturer = "Würth Elektronik",
				url = weComp.Url!,
				inductance = weComp.Inductance ?? 0,
				ratedCurrent = weComp.Rated_Current ?? 0,
				length = weComp.Length ?? 0,
				height = weComp.Height ?? 0,
				diameter = weComp.Width ?? 0, // Using width as diameter
				mount = weComp.Mount!,
				series = weComp.Product_Series!
			},
			"capacitor" => new Capacitor
			{
				id = weComp.Order_Code!,
				type = "capacitor",
				orderCode = weComp.Order_Code!,
				manufacturer = "Würth Elektronik",
				url = weComp.Url!,
				ratedVoltage = weComp.Rated_Voltage ?? 0,
				capacitance = weComp.Capacitance ?? 0,
				length = weComp.Length ?? 0,
				pitch = weComp.Width ?? 0, // Using width as pitch
				diameter = weComp.Height ?? 0, // Using height as diameter
				mount = weComp.Mount!,
				family = weComp.Product_Family!
			},
			_ => null,
		};

	}

	private static Component? FindBestMatch(Component competitor, List<Component> weComponents)
	{
		// Filter by component type
		var candidates = weComponents.Where(w => w.type == competitor.type).ToList();
		if (!candidates.Any()) return null;

		Component? bestMatch = null;
		double bestScore = double.MinValue;

		foreach (var candidate in candidates)
		{
			var score = CalculateMatchScore(competitor, candidate);
			if (score > bestScore)
			{
				bestScore = score;
				bestMatch = candidate;
			}
		}
		return bestMatch;
	}

	private static double CalculateMatchScore(Component comp1, Component comp2)
	{
		double score = 0;

		switch (comp1)
		{
			case Resistor r1 when comp2 is Resistor r2:
				score += CompareValues(r1.resistance, r2.resistance, 0.1) * 0.4;
				score += CompareValues(r1.ratedPower, r2.ratedPower, 0.1) * 0.3;
				score += CompareValues(r1.ratedCurrent, r2.ratedCurrent, 0.1) * 0.3;
				break;

			case Inductor i1 when comp2 is Inductor i2:
				score += CompareValues(i1.inductance, i2.inductance, 0.1) * 0.4;
				score += CompareValues(i1.ratedCurrent, i2.ratedCurrent, 0.1) * 0.3;
				score += CompareValues(i1.DCResistance, i2.DCResistance, 0.1) * 0.3;
				break;

			case Capacitor c1 when comp2 is Capacitor c2:
				score += CompareValues(c1.capacitance, c2.capacitance, 0.1) * 0.4;
				score += CompareValues(c1.ratedVoltage, c2.ratedVoltage, 0.1) * 0.3;
				score += CompareValues(c1.rippleCurrent, c2.rippleCurrent, 0.1) * 0.3;
				break;
		}

		// Add physical dimensions comparison
		score += ComparePhysicalDimensions(comp1, comp2) * 0.2;

		return score;
	}

	private static double CompareValues(double val1, double val2, double tolerance)
	{
		if (val1 == 0 || val2 == 0) return 0;
		
		var ratio = val1 / val2;
		if (ratio < 1) ratio = 1 / ratio;
		
		return ratio <= (1 + tolerance) ? 1.0 : 1.0 / ratio;
	}

	private static double ComparePhysicalDimensions(Component comp1, Component comp2)
	{
		var lengthScore = CompareValues(comp1.length, comp2.length, 0.2);
		var widthScore = CompareValues(
			(comp1 as dynamic)?.width ?? 0, 
			(comp2 as dynamic)?.width ?? 0, 
			0.2);
		var heightScore = CompareValues(
			(comp1 as dynamic)?.height ?? 0, 
			(comp2 as dynamic)?.height ?? 0, 
			0.2);

		return (lengthScore + widthScore + heightScore) / 3;
	}

	private static void OutputResults(List<(Component Competitor, Component WE)> matches)
	{
		// Output to CSV in the same format as example_results.csv
		using var writer = new StreamWriter("results.csv");
		writer.WriteLine("competitor_pn,competitor,product_category,alternative_pn");
		foreach (var (competitor, we) in matches)
		{
			writer.WriteLine($"{competitor.orderCode},{competitor.manufacturer},{competitor.type},{we.orderCode}");

		}
	}
}
