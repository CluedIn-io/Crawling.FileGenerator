using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CSVFileGenerator
{
  /// <summary>
  /// This class generates the models, vocabs and clue producers from CSV files with data in them
  /// </summary>
  class Program
  {
	private static readonly List<string> filePaths = Directory.GetFiles($@"..\..\CSVs\").ToList();
	private const string crawlerName = "CrawlerName";


	static void Main(string[] args)
	{
	  CreateModels();
	  CreateVocabs();
	  CreateClueProducers();
	}

	private static void CreateModels()
	{
	  foreach (var path in filePaths)
	  {

		var table = path.Substring(path.LastIndexOf('\\')).Replace("\\", "").Replace(".csv", "");
		table = NormalizeTable(table);

		var columnNames = GetColumnNames(path);

		var longestColumnName = columnNames.OrderByDescending(c => c.Length).First();

		List<string> modelList = new List<string>();

		modelList.Add($"namespace CluedIn.Crawling.{crawlerName}.Core.Models");
		modelList.Add("{");
		modelList.Add("using System;");
		modelList.Add("using System.Collections.Generic;");
		modelList.Add("using System.Data;");
		modelList.Add("");

		modelList.Add($"public class {NormalizeTable(table)} : {crawlerName}Entity");
		modelList.Add("{");
		modelList.Add($"public {NormalizeTable(table)} (Dictionary<string, string> dic) : base (dic)");
		modelList.Add("{");
		modelList.Add("if (dic == null)");
		modelList.Add("{");
		modelList.Add("throw new ArgumentNullException(nameof(dic));");
		modelList.Add("}");
		modelList.Add("");

		foreach (var column in columnNames)
		{
		  var numberOfSpaces = "   "; //3 spaces for the longest name

		  var difSpaces = longestColumnName.Length - column.Length;
		  if (difSpaces > 0)
		  {
			for (int j = 0; j < difSpaces; j++)
			{
			  numberOfSpaces += " ";
			}
		  }

		  modelList.Add($@"{column}{numberOfSpaces}= {GetTypedGetFunction(column)};");
		}
		modelList.Add("}");

		modelList.Add("");

		foreach (var column in columnNames)
		{

		  modelList.Add($@"public {GetColumnType(column)} {column} " + "{ get; private set; }");
		}

		modelList.Add("}");
		modelList.Add("");
		modelList.Add("}");

		File.WriteAllLines($@"..\..\FilesOutput\models\{table}.cs", modelList.ToArray());
	  }
	}

	private static void CreateVocabs()
	{
	  foreach (var path in filePaths)
	  {

		var table = path.Substring(path.LastIndexOf('\\')).Replace("\\", "").Replace(".csv", "");
		table = NormalizeTable(table);

		var columnNames = GetColumnNames(path);

		var longestColumnName = columnNames.OrderByDescending(c => c.Length).First();

		List<string> vocabList = new List<string>();

		vocabList.Add("using CluedIn.Core.Data;");
		vocabList.Add("using CluedIn.Core.Data.Vocabularies;");
		vocabList.Add("");
		vocabList.Add($"namespace CluedIn.Crawling.{crawlerName}.Vocabularies");
		vocabList.Add("{");

		vocabList.Add($"public class {table}Vocabulary : SimpleVocabulary");
		vocabList.Add("{");
		vocabList.Add($"public {table}Vocabulary()");
		vocabList.Add("{");
		vocabList.Add($@"VocabularyName = ""{crawlerName} {table}""; // TODO: Set value");
		vocabList.Add($@"KeyPrefix = ""{char.ToLower(crawlerName[0]) + crawlerName.Substring(1)}.{table.ToLower()}""; // TODO: Set value");
		vocabList.Add($@"KeySeparator = ""."";");
		vocabList.Add($@"Grouping = EntityType.Organization; // TODO: Set value");
		vocabList.Add("");
		vocabList.Add($@"AddGroup(""{table} Details"", group =>");
		vocabList.Add("{");

		foreach (var column in columnNames)
		{
		  var numberOfSpaces = "   "; //3 spaces for the longest name

		  var difSpaces = longestColumnName.Length - column.Length;
		  if (difSpaces > 0)
		  {
			for (int j = 0; j < difSpaces; j++)
			{
			  numberOfSpaces += " ";
			}
		  }

		  vocabList.Add($@"{column}{numberOfSpaces}= group.Add(new VocabularyKey(""{column}"", VocabularyKeyDataType.{GetDataType(column)}, VocabularyKeyVisiblity.{GetVisibility(column)}){GetDisplayName(column)});");
		}

		vocabList.Add("});");
		vocabList.Add("}");
		vocabList.Add("");

		foreach (var column in columnNames)
		{
		  vocabList.Add($@"public VocabularyKey {column} " + "{ get; private set; }");
		}

		vocabList.Add("}");
		vocabList.Add("}");

		File.WriteAllLines($@"..\..\FilesOutput\vocabs\{table}Vocabulary.cs", vocabList.ToArray());
	  }
	}

	private static void CreateClueProducers()
	{
	  foreach (var path in filePaths)
	  {

		var table = path.Substring(path.LastIndexOf('\\')).Replace("\\", "").Replace(".csv", "");
		table = NormalizeTable(table);

		var columnNames = GetColumnNames(path);

		var longestColumnName = columnNames.OrderByDescending(c => c.Length).First();

		List<string> cluedProdList = new List<string>();

		cluedProdList.Add("using CluedIn.Core.Data;");
		cluedProdList.Add("using CluedIn.Core.Data.Vocabularies;");
		cluedProdList.Add("using CluedIn.Crawling.Factories;");
		cluedProdList.Add("using CluedIn.Crawling.Helpers;");
		cluedProdList.Add($"using CluedIn.Crawling.{crawlerName}.Vocabularies;");
		cluedProdList.Add($"using CluedIn.Crawling.{crawlerName}.Core.Models;");
		cluedProdList.Add("using CluedIn.Core;");
		cluedProdList.Add("using RuleConstants = CluedIn.Core.Constants.Validation.Rules;");
		cluedProdList.Add("using System.Linq;");
		cluedProdList.Add("using System;");
		cluedProdList.Add("");
		cluedProdList.Add($"namespace CluedIn.Crawling.{crawlerName}.ClueProducers");
		cluedProdList.Add("{");

		cluedProdList.Add($"public class {table}ClueProducer : BaseClueProducer<{table}>");
		cluedProdList.Add("{");
		cluedProdList.Add($"private readonly IClueFactory _factory;");
		cluedProdList.Add("");
		cluedProdList.Add($"public {table}ClueProducer(IClueFactory factory)" + @"
							{
								_factory = factory;
							}");

		cluedProdList.Add("");
		cluedProdList.Add($"protected override Clue MakeClueImpl({table} input, Guid id)");
		cluedProdList.Add("{");
		cluedProdList.Add("");
		cluedProdList.Add($@"var clue = _factory.Create(/{table}, input.FILL_IN, id);

							var data = clue.Data.EntityData;

							");
		cluedProdList.Add("");
		cluedProdList.Add(@"if (!data.OutgoingEdges.Any())
			                _factory.CreateEntityRootReference(clue, EntityEdgeType.PartOf);
							");
		cluedProdList.Add("");

		cluedProdList.Add($"var vocab = new {NormalizeTable(table)}Vocabulary();");

		cluedProdList.Add("");


		foreach (var column in columnNames)
		{
		  var numberOfSpaces = "   "; //3 spaces for the longest name

		  var difSpaces = longestColumnName.Length - column.Length;
		  if (difSpaces > 0)
		  {
			for (int j = 0; j < difSpaces; j++)
			{
			  numberOfSpaces += " ";
			}
		  }

		  cluedProdList.Add($@"data.Properties[vocab.{column}]{numberOfSpaces}= input.{column}.PrintIfAvailable();");
		}
		cluedProdList.Add("");
		cluedProdList.Add(@"clue.ValidationRuleSuppressions.AddRange(new[]
							{
								RuleConstants.METADATA_001_Name_MustBeSet,
								RuleConstants.PROPERTIES_001_MustExist,
								RuleConstants.METADATA_002_Uri_MustBeSet,
								RuleConstants.METADATA_003_Author_Name_MustBeSet,
								RuleConstants.METADATA_005_PreviewImage_RawData_MustBeSet
							});");
		cluedProdList.Add("");
		cluedProdList.Add("return clue;");
		cluedProdList.Add("}");
		cluedProdList.Add("}");
		cluedProdList.Add("}");
		cluedProdList.Add("");
		cluedProdList.Add("");


		File.WriteAllLines($@"..\..\FilesOutput\clueproducers\{table}ClueProducer.cs", cluedProdList.ToArray());
	  }
	}

	private static string NormalizeTable(string table)
	{

	  var tbl = table;
	  TextInfo txtInfo = new CultureInfo("en-us", false).TextInfo;
	  tbl = txtInfo.ToTitleCase(tbl);
	  return tbl;
	}

	private static List<string> GetColumnNames(string path)
	{
	  return File.ReadAllLines(path)[0].Split(',').Select(c => c.Split(new[] { "_" }, StringSplitOptions.RemoveEmptyEntries).Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1, s.Length - 1)).Aggregate(string.Empty, (s1, s2) => s1 + s2)).Select(c => Regex.Replace(c.Replace(" ", ""), @"[^0-9a-zA-Z]+", "")).ToList();
	}

	private static string GetTypedGetFunction(string column)
	{
	  if (column.EndsWith("date", StringComparison.InvariantCultureIgnoreCase))
		return $@"GetColumnValue<DateTime>(""{column}"")";

	  if (column.EndsWith("ind", StringComparison.InvariantCultureIgnoreCase))
		return $@"GetColumnValue<bool>(""{column}"")";

	  if (column.EndsWith("count", StringComparison.InvariantCultureIgnoreCase))
		return $@"GetColumnValue<int>(""{column}"")";

	  if (column.EndsWith("value", StringComparison.InvariantCultureIgnoreCase))
		return $@"GetColumnValue<decimal>(""{column}"")";


	  return $@"GetColumnValue<string>(""{column}"")";

	}


	private static string GetColumnType(string column)
	{
	  if (column.EndsWith("date", StringComparison.InvariantCultureIgnoreCase))
		return "DateTime";

	  if (column.EndsWith("ind", StringComparison.InvariantCultureIgnoreCase))
		return "bool";

	  if (column.EndsWith("count", StringComparison.InvariantCultureIgnoreCase))
		return "int";

	  if (column.EndsWith("value", StringComparison.InvariantCultureIgnoreCase))
		return "decimal";


	  return "string";

	}

	private static string GetDataType(string column)
	{

	  if (column.EndsWith("date", StringComparison.InvariantCultureIgnoreCase))
		return "DateTime";

	  if (column.EndsWith("ind", StringComparison.InvariantCultureIgnoreCase))
		return "Boolean";

	  if (column.EndsWith("count", StringComparison.InvariantCultureIgnoreCase))
		return "Integer";

	  if (column.EndsWith("value", StringComparison.InvariantCultureIgnoreCase))
		return "Number";

	  return "Text";
	}

	private static string GetVisibility(string column)
	{
	  if (column.EndsWith("id", StringComparison.InvariantCultureIgnoreCase))
		return "HiddenInFrontendUI";

	
	  return "Visible";
	}

	private static object GetDisplayName(string column)
	{
	  column = column.Replace("_", "");
	  return $@".WithDisplayName(""{Regex.Replace(column, "([a-z](?=[A-Z]|[0-9])|[A-Z](?=[A-Z][a-z]|[0-9])|[0-9](?=[^0-9]))", "$1 ")}"")";
	}

  }

}
