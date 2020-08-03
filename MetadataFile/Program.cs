using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MetadataFile
{
  /// <summary>
  /// This class generates the models, vocabs, clue producers and crawler code. It uses the CluedIn Crawler Metadata File that contains standardized metadata about the source system to be crawled.
  /// Make sure to change the index used in the GetValues function to retrieve the correct values for tableNameValues, columnNameValues etc.
  /// Make sure you have all the necessary "using" statements and you use the correct contructor method in the Models (e.g. IDataReader might not be necessary or it may have to be changed alltogether, depending on the data source to be crawled). 
  /// Make sure to update the methods, depending on the data source: 
  ///		  Normalize 
  ///		  GetColumnType
  ///		  GetReader
  ///		  GetDataTypeFromColumnType
  /// </summary>
  class Program
  {
	#region configuration
	private const string path = @"..\..\Metadata.csv";
	private const string crawlerName = "CrawlerName";
	#endregion

	private static readonly List<string> fileLines = File.ReadAllLines(path).Select(l => l).ToList();
	private static readonly List<string> tableNameValues = GetValues(0);
	private static readonly List<string> cluedInVocabNameValues = GetValues(1);
	private static readonly List<string> customEntityNameValues = GetValues(2);
	private static readonly List<string> columnNameValues = GetValues(3);
	private static readonly List<string> displayValues = GetValues(4);
	private static readonly List<string> descriptionValues = GetValues(5);
	private static readonly List<string> identifierValues = GetValues(6);
	private static readonly List<string> cluedInDataTypeValues = GetValues(7);
	private static readonly List<string> primaryKeyColumnValues = GetValues(8);
	private static readonly List<string> columnTypeValues = GetValues(9);
	private static readonly List<string> foreignKeyColumnValues = GetValues(10);



	static void Main(string[] args)
	{
	  CreateModels();
	  CreateVocabs();
	  CreateClueProducers();
	  CreateCrawlerCode();
	}

	private static void CreateModels()
	{
	  foreach (var table in tableNameValues.Distinct())
	  {
		List<string> modelList = new List<string>();

		var indexes = Enumerable.Range(0, fileLines.Count)
			   .Where(i => fileLines[i].Split(',')[0] == table)
			   .ToList();

		modelList.Add($"namespace CluedIn.Crawling.{crawlerName}.Core.Models");
		modelList.Add("{");
		modelList.Add("using System;");
        modelList.Add("using System.ComponentModel;");
		modelList.Add("using System.Data;");
        modelList.Add($"using CluedIn.Crawling.{crawlerName}.Core;");
		modelList.Add("");

		modelList.Add($@"[DisplayName(""{table}"")]");
		modelList.Add($"public class {Normalize(table)} : Base{crawlerName}Entity");
		modelList.Add("{");
		modelList.Add($"public {Normalize(table)} (IDataReader reader) : base (reader)");
		modelList.Add("{");
		modelList.Add("if (reader == null)");
		modelList.Add("{");
		modelList.Add("throw new ArgumentNullException(nameof(reader));");
		modelList.Add("}");

		foreach (var i in indexes)
		{
		  var normalizedColumnValue = Normalize(columnNameValues[i]);

		  modelList.Add($@"{normalizedColumnValue} = {GetReader(columnNameValues[i], i)};");
		}
		modelList.Add("}");

		modelList.Add("");

		foreach (var i in indexes)
		{
		  var normalizedColumnValue = Normalize(columnNameValues[i]);

		  modelList.Add($@"public {GetColumnType(i)} {normalizedColumnValue} " + "{ get; private set; }");
		}

		modelList.Add("}");
		modelList.Add("");
		modelList.Add("}");

		File.WriteAllLines($@"..\..\FilesOutput\models\{Normalize(table)}.cs", modelList.ToArray());
	  }
	}

	private static void CreateVocabs()
	{
	  foreach (var table in tableNameValues)
	  {
		List<string> vocabList = new List<string>();

		var indexes = Enumerable.Range(0, fileLines.Count)
			   .Where(i => fileLines[i].Split(',')[0] == table)
			   .ToList();

		vocabList.Add("using CluedIn.Core.Data;");
		vocabList.Add("using CluedIn.Core.Data.Vocabularies;");
		vocabList.Add("");
		vocabList.Add($"namespace CluedIn.Crawling.{crawlerName}.Vocabularies");
		vocabList.Add("{");

		vocabList.Add($"public class {Normalize(table)}Vocabulary : SimpleVocabulary");
		vocabList.Add("{");
		vocabList.Add($"public {Normalize(table)}Vocabulary()");
		vocabList.Add("{");
		vocabList.Add($@"VocabularyName = ""{crawlerName} {Normalize(table)}""; // TODO: Set value");
		vocabList.Add($@"KeyPrefix = ""{char.ToLower(crawlerName[0]) + crawlerName.Substring(1)}.{table}""; // TODO: Set value");
		vocabList.Add($@"KeySeparator = ""."";");
		vocabList.Add($@"Grouping = {GetGrouping(table, indexes.First())}; // TODO: Check value");
		vocabList.Add("");
		vocabList.Add($@"AddGroup(""{GetGrouping(table, indexes.First()).Replace("EntityType.", "").Replace("\"", "")} Details"", group =>");
		vocabList.Add("{");

		foreach (var i in indexes)
		{
		  var normalizedColumnValue = Normalize(columnNameValues[i]);

		  vocabList.Add($@"{normalizedColumnValue} = group.Add(new VocabularyKey(""{Char.ToLowerInvariant(columnNameValues[i][0]) + Normalize(columnNameValues[i]).Substring(1)}"", VocabularyKeyDataType.{GetDataType(i)}, VocabularyKeyVisiblity.{GetVisibility(i)}){GetDisplayName(displayValues[i])}{GetDescription(descriptionValues[i])});");
		}

		vocabList.Add("});");
		vocabList.Add("}");
		vocabList.Add("");


		foreach (var i in indexes)
		{
		  var normalizedColumnValue = Normalize(columnNameValues[i]);

		  vocabList.Add($@"public VocabularyKey {normalizedColumnValue} " + "{ get; private set; }");
		}

		vocabList.Add("}");
		vocabList.Add("}");

		File.WriteAllLines($@"..\..\FilesOutput\vocabs\{Normalize(table)}Vocabulary.cs", vocabList.ToArray());
	  }
	}

	private static void CreateClueProducers()
	{
	  foreach (var table in tableNameValues)
	  {
		List<string> cluedProdList = new List<string>();

		var indexes = Enumerable.Range(0, fileLines.Count)
			   .Where(i => fileLines[i].Split(',')[0] == table)
			   .ToList();

		string entityCode = GetEntityCode(indexes);


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

		cluedProdList.Add($"public class {Normalize(table)}ClueProducer : BaseClueProducer<{Normalize(table)}>");
		cluedProdList.Add("{");
		cluedProdList.Add($"private readonly IClueFactory _factory;");
		cluedProdList.Add("");
		cluedProdList.Add($"public {Normalize(table)}ClueProducer(IClueFactory factory)" + @"
							{
								_factory = factory;
							}");

		cluedProdList.Add("");
		cluedProdList.Add($"protected override Clue MakeClueImpl({Normalize(table)} input, Guid id)");
		cluedProdList.Add("{");
		cluedProdList.Add("");
		cluedProdList.Add($@"var clue = _factory.Create({GetEntityType(table, indexes.First())}"", {entityCode}, id);

							var data = clue.Data.EntityData;

							");
		cluedProdList.Add("");
		cluedProdList.Add("//add edges");
		cluedProdList.Add("");

		foreach (var i in indexes)
		{
		  if (!string.IsNullOrEmpty(foreignKeyColumnValues[i])) //we have a foreign key somewhere
		  {
			var tableColumnPair = foreignKeyColumnValues[i].Split('.');
			var primaryTable = tableColumnPair[0];
			var primaryTableKeyColumnName = tableColumnPair[1];

			var normalizedColumnName = Normalize(columnNameValues[i]);

			var primaryTableIndexes = Enumerable.Range(0, fileLines.Count)
			   .Where(j => fileLines[j].Split(',')[0] == primaryTable)
			   .ToList();

			var entityType = "FILL_IN";
			if (primaryTableIndexes.Count == 0) //the primary table is not in the list
			  entityType = GetEntityType(primaryTable, 987654321);
			else
			  entityType = GetEntityType(primaryTable, primaryTableIndexes.FirstOrDefault());

			cluedProdList.Add($@"if(input.{normalizedColumnName} != null && !string.IsNullOrEmpty(input.{normalizedColumnName}.ToString()))");
			cluedProdList.Add("{");

			cluedProdList.Add($@"_factory.CreateOutgoingEntityReference(clue, {entityType}, EntityEdgeType.AttachedTo, input.{normalizedColumnName}, input.{normalizedColumnName}.ToString());");

			cluedProdList.Add("}");
		  }
		}

		cluedProdList.Add("");
		cluedProdList.Add(@"if (!data.OutgoingEdges.Any())
			                _factory.CreateEntityRootReference(clue, EntityEdgeType.PartOf);
							");
		cluedProdList.Add("");

		cluedProdList.Add($"var vocab = new {Normalize(table)}Vocabulary();");

		cluedProdList.Add("");


		foreach (var i in indexes)
		{
		  var columnValue = Normalize(columnNameValues[i]);

		
		  cluedProdList.Add($@"data.Properties[vocab.{columnValue}] = input.{columnValue}.PrintIfAvailable();");
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



		File.WriteAllLines($@"..\..\FilesOutput\clueproducers\{Normalize(table)}ClueProducer.cs", cluedProdList.ToArray());
	  }
	}

	private static void CreateCrawlerCode()
	{
	  List<string> crawlerCodeList = new List<string>();

	  foreach (var table in tableNameValues.Distinct())
	  {

		var indexes = Enumerable.Range(0, fileLines.Count)
			   .Where(i => fileLines[i].Split(',')[0] == table)
			   .ToList();


		crawlerCodeList.Add($@"foreach (var obj in client.GetObject<{Normalize(table)}>())");

		crawlerCodeList.Add("{");


		crawlerCodeList.Add("yield return obj;");
		crawlerCodeList.Add("}");
		crawlerCodeList.Add("");
	  }
	  File.WriteAllLines($@"..\..\FilesOutput\crawlerCode.cs", crawlerCodeList.ToArray());

	}


	private static string GetGrouping (string table, int firstIndexOfTable)
	{
	  if(firstIndexOfTable == 987654321)
		return $@"""{Normalize(table)}""";


	  if (!string.IsNullOrEmpty(cluedInVocabNameValues[firstIndexOfTable]) && cluedInVocabNameValues[firstIndexOfTable] != "NULL") //todo: check if it's null or blank in the metadata file
		return $"EntityType.{cluedInVocabNameValues[firstIndexOfTable]}";
	  else if (!string.IsNullOrEmpty(customEntityNameValues[firstIndexOfTable]) && customEntityNameValues[firstIndexOfTable] != "NULL") //todo: check if it's null or blank in the metadata file
		return $@"""{customEntityNameValues[firstIndexOfTable]}""";

	  return $@"""{Normalize(table)}""";
	}

	private static string GetEntityType(string table, int firstIndexOfTable)
	{
	  var grouping = GetGrouping(table, firstIndexOfTable);

	  if (grouping.Contains("EntityType."))
		return grouping;
	  else
	  {
		var entityType = grouping.Replace("EntityType.", "").Insert(1, "/");
		return entityType;
	  }
	}

	private static string GetEntityCode(List<int> indexes)
	{
	  var pkIndexes = indexes.Where(i => !string.IsNullOrEmpty(primaryKeyColumnValues[i])).ToArray();


	  if (pkIndexes.Count() == 1)
		return "$" + "\"{input." + Normalize(columnNameValues[pkIndexes.First()]) + "}\"";

	  if (pkIndexes.Count() > 1)
	  {
		var entityCode = "$" + "\"";
		foreach (var index in pkIndexes)
		{
		  var dot = ".";

		  if (index == pkIndexes.Last())
			dot = "";

		  entityCode += "{input." + $"{Normalize(columnNameValues[pkIndexes.First(i => i == index)])}" + "}" + dot;
		}


		return entityCode + "\"";
	  }

	  return "FILL_IN";

	}

	/// <summary>
	/// TODO: change this depending on the namings in the metadata file
	/// </summary>
	/// <param name="name"></param>
	/// <returns></returns>
	private static string Normalize(string name)
	{
	  name = name.ToLower();
	  TextInfo txtInfo = new CultureInfo("en-us", false).TextInfo;
	  name = txtInfo.ToTitleCase(name);
	  name = name.Replace(" ", "").Replace("/", "").Replace("_", "");

	  return name;
	}

	private static List<string> GetValues(int position)
	{
	  return File.ReadAllLines(path).Select(l => l.Split(',')[position]).ToList();

	}

	/// <summary>
	/// TODO: Change the cases to match the types in the metadata file
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	private static string GetColumnType(int index)
	{
	  if (!string.IsNullOrEmpty(primaryKeyColumnValues[index]))
		return "string";

	  var type = columnTypeValues[index];

	  switch (type)
	  {
		case "int":
		  return "int";
		case "float":
		  return "decimal";
		case "datetime2":
		  return "DateTime";
		default:
		  return "string";
	  }
	}

	/// <summary>
	/// TODO: Configure this depending on what data source we crawl
	/// </summary>
	/// <param name="value"></param>
	/// <param name="index"></param>
	/// <returns></returns>
	private static string GetReader(string value, int index)
	{
	  var type = GetColumnType(index);
	  switch (type)
	  {
		case "string":
		  return $@"GetStringValue(""{value}"")";
		default:
		  return $@"GetNullableValue<{type}>(""{value}"")";
	  }
	}

	private static string GetDataType(int index)
	{
	  if(cluedInDataTypeValues.Count > 0)
	  {
		if(!string.IsNullOrEmpty(cluedInDataTypeValues[index]) && cluedInDataTypeValues[index] != "NULL") //TODO: check if it's NULL in the metadata file
		{
		  if (!string.IsNullOrEmpty(identifierValues[index]) && identifierValues[index] != "NULL" && identifierValues[index].Equals("yes", StringComparison.InvariantCultureIgnoreCase))
			return "Identifier";
		  return cluedInDataTypeValues[index];
		}
		else
		{
		  return GetDataTypeFromColumnType(index);
		}
	  }
	  return GetDataTypeFromColumnType(index);
	}

	/// <summary>
	/// TODO: Change the cases to match the types in the metadata file
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	private static string GetDataTypeFromColumnType(int index)
	{
	  if (!string.IsNullOrEmpty(identifierValues[index]) && identifierValues[index] != "NULL")
		return "Identifier";

	  var type = columnTypeValues[index];

	  switch (type)
	  {
		case "int":
		case "smallint":
		case "bigint":
		  return "Integer";
		case "bit":
		  return "Boolean";
		case "DateTime":
		case "datetime":
		  return "DateTime";
		case "decimal":
		case "float":
		  return "Number";
		case "money":
		  return "Money";
		default:
		  return "Text";
	  }
	}

	private static string GetVisibility(int index)
	{
	  if (!string.IsNullOrEmpty(primaryKeyColumnValues[index]))
		return "HiddenInFrontendUI";

	  return "Visible";
	}

	private static string GetDisplayName(string value)
	{
	  if (string.IsNullOrEmpty(value) || value == "NULL") return "";
	  else
	  {
		return $@".WithDisplayName(""{value}"")";
	  }
	}

	private static string GetDescription(string value)
	{
	  if (string.IsNullOrEmpty(value) || value == "NULL") return "";
	  else
	  {
		return $@".WithDescription(""{value}"")";
	  }
	}

  }

}
