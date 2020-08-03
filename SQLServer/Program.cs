using CsvHelper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SQLServer
{
  /// <summary>
  /// This class generates the models, vocabs, clue producers and crawler code. It uses a script that gets the metadata about the SQL server tables to be crawled.
  /// The script can be used by the customer which has enough privileges to get the metadata from the SQL server. They can generate a metadata csv file, case in which we will use the GetSchemaDetails (string filePath) method.
  /// Some methods (e.g. Normalize, GetReader etc.) may need to be modified in order to correctly generate the files.
  /// </summary>
  class Program
  {

    private const string crawlerName = "CrawlerName";

    private static readonly List<SchemaDetail> details = GetSchemaDetails("Metadata.csv");
    private static readonly List<string> tableNameValues = details.Select(d => d.Table).ToList();
    private static readonly List<string> columnNameValues = details.Select(d => d.ColumnName).ToList();
    private static readonly List<string> descriptionValues = details.Select(d => d.ColumnDescription).ToList();
    private static readonly List<string> columnTypeValues = details.Select(d => d.ColumnType).ToList();
    private static readonly List<string> isPrimaryKeyValues = details.Select(d => d.IsPrimaryKey).ToList();
    private static readonly List<string> primaryTableValues = details.Select(d => d.PrimaryTable).ToList();
    private static readonly List<string> primarykeyColumnNameValues = details.Select(d => d.PkColumnName).ToList();

    private static readonly string longestColumnName = columnNameValues.OrderByDescending(c => c.Length).First();


    static void Main(string[] args)
	{
      CreateModels();
      CreateVocabs();
      CreateClueProducers();
      CreateCrawlerCode();
    }

    private static List<SchemaDetail> GetSchemaDetails()
    {
      var details = new List<SchemaDetail>();

      using (var connection = new SqlConnection("Server=localhost;Database=AdventureWorks2017;Trusted_Connection=True;"))
      {
        connection.Open();

        using (var cmd = new SqlCommand() { CommandTimeout = 0 })
        {
          cmd.Connection = connection;
          cmd.CommandText = $@"

          select schema_name(tab.schema_id) + '.' + tab.name as [Table],
	      col.name as ColumnName,
	      sep.value as ColumnDescription,
	      types.name as ColumnType,
	      indexes.index_id as isPrimaryKey,
	      schema_name(tab_fk.schema_id) + '.' + tab_fk.name as PrimaryTable,
	      fks.name as PkColumnName

	      from sys.tables tab
	      inner join sys.columns col
		      on col.object_id = tab.object_id	
	
	      left outer join sys.index_columns index_cols
		      on index_cols.object_id = col.object_id
              and index_cols.index_column_id = col.column_id
		      and index_cols.column_id = col.column_id
		      and index_cols.key_ordinal != 0

	      left outer join sys.indexes indexes
		      on indexes.object_id = index_cols.object_id
		      and indexes.index_id = index_cols.index_id
		      and indexes.is_primary_key = 1

	      left outer join sys.foreign_key_columns foreign_cols
		      on foreign_cols.parent_object_id = tab.object_id
		      and foreign_cols.parent_column_id = col.column_id
	      left outer join sys.tables tab_fk
		      on tab_fk.object_id = foreign_cols.referenced_object_id
	      left outer join sys.columns fks
		      on fks.object_id = tab_fk.object_id
		      and fks.column_id = foreign_cols.referenced_column_id

	            left outer join sys.systypes types 
	                on types.xusertype = col.user_type_id
	            left join sys.extended_properties sep
		            on tab.object_id = sep.major_id
                    and col.column_id = sep.minor_id
		            and sep.name = 'MS_Description'
		            and sep.class_desc = 'OBJECT_OR_COLUMN'

	            where schema_name(tab.schema_id) not like '%dbo%' 
	            order by schema_name(tab.schema_id) + '.' + tab.name, col.column_id	
          ";


          using (var reader = cmd.ExecuteReader(CommandBehavior.Default))
          {
            while (reader.Read())
            {
              var sc = new SchemaDetail(reader);
              details.Add(sc);
            }
          }
        }
        connection.Close();
      }

      return details;
    }

    private static List<SchemaDetail> GetSchemaDetails(string filePath)
    {
      var details = new List<SchemaDetail>();

      using (var reader = new StreamReader(filePath))
      {
        using (var csv = new CsvReader(reader, CultureInfo.CurrentCulture))
        {
          details = csv.GetRecords<SchemaDetail>().ToList();
        }
      }

      return details;
    }


    private static void CreateModels()
    {
      foreach (var table in tableNameValues.Distinct())
      {
        List<string> modelList = new List<string>();

        var indexes = Enumerable.Range(0, details.Count)
               .Where(i => details[i].Table == table)
               .ToList();

        modelList.Add($"namespace CluedIn.Crawling.{crawlerName}.Core.Models");

        modelList.Add("{");
        modelList.Add("using System;");
        modelList.Add("using System.ComponentModel;");
        modelList.Add("using System.Data;");
        modelList.Add($"using CluedIn.Crawling.{crawlerName}.Core;");
        modelList.Add("");

        modelList.Add($@"[DisplayName(""{table}"")]");
        modelList.Add($"public class {Normalize(table)} : BaseSqlEntity");
        modelList.Add("{");
        modelList.Add($"public {Normalize(table)} (IDataReader reader) : base (reader)");
        modelList.Add("{");
        modelList.Add("if (reader == null)");
        modelList.Add("{");
        modelList.Add("throw new ArgumentNullException(nameof(reader));");
        modelList.Add("}");

        foreach (var i in indexes)
        {
          var numberOfSpaces = "   "; //3 spaces for the longest name

          var normalizedColumnValue = Normalize(columnNameValues[i]);

          var difSpaces = longestColumnName.Length - normalizedColumnValue.Length;
          if (difSpaces > 0)
          {
            for (int j = 0; j < difSpaces; j++)
            {
              numberOfSpaces += " ";
            }
          }

          modelList.Add($@"{normalizedColumnValue}{numberOfSpaces}= {GetReader(columnNameValues[i], i)};");
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

        var indexes = Enumerable.Range(0, details.Count)
               .Where(i => details[i].Table == table)
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
        vocabList.Add($@"VocabularyName = ""{Normalize(table)}""; // TODO: Set value");
        vocabList.Add($@"KeyPrefix = ""{char.ToLower(crawlerName[0]) + crawlerName.Substring(1)}.{Normalize(table)}""; // TODO: Set value");
        vocabList.Add($@"KeySeparator = ""."";");
        vocabList.Add($@"Grouping = ""{Normalize(table)}""; // TODO: Set value");
        vocabList.Add("");
        vocabList.Add($@"AddGroup(""{Normalize(table)} Details"", group =>");
        vocabList.Add("{");

        foreach (var i in indexes)
        {
          var numberOfSpaces = "   "; //3 spaces for the longest name

          var normalizedColumnValue = Normalize(columnNameValues[i]);

          var difSpaces = longestColumnName.Length - normalizedColumnValue.Length;
          if (difSpaces > 0)
          {
            for (int j = 0; j < difSpaces; j++)
            {
              numberOfSpaces += " ";
            }
          }

          vocabList.Add($@"{normalizedColumnValue}{numberOfSpaces}= group.Add(new VocabularyKey(""{Char.ToLowerInvariant(columnNameValues[i][0]) + Normalize(columnNameValues[i]).Substring(1)}"", VocabularyKeyDataType.{GetDataType(i)}, VocabularyKeyVisibility.{GetVisibility(i)}){GetDisplayName(columnNameValues[i])}{GetDescription(i)});");

          
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

    private static string GetDescription(int i)
    {
      if (string.IsNullOrEmpty(descriptionValues[i]))
        return "";

      return $@".WithDescription(""{descriptionValues[i]}"")";
    }

    private static void CreateClueProducers()
    {
      foreach (var table in tableNameValues)
      {
        List<string> cluedProdList = new List<string>();

        var indexes = Enumerable.Range(0, details.Count)
               .Where(i => details[i].Table == table)
               .ToList();

        string entityCode = GetEntityCode(indexes);


        cluedProdList.Add("using CluedIn.Core.Data;");
        cluedProdList.Add("using CluedIn.Core.Data.Vocabularies;");
        cluedProdList.Add("using CluedIn.Crawling.Factories;");
        cluedProdList.Add("using CluedIn.Crawling.Helpers;");
        cluedProdList.Add($"using CluedIn.Crawling.{crawlerName}.Vocabularies;");
        cluedProdList.Add($"using CluedIn.Crawling.{crawlerName}.Core.Models;");
        cluedProdList.Add($"using CluedIn.Crawling.{crawlerName}.Core;");
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
        cluedProdList.Add($@"var clue = _factory.Create(""/{Normalize(table)}"", {entityCode}, id);

							var data = clue.Data.EntityData;

							");
        cluedProdList.Add("");
        cluedProdList.Add($"data.Name = input.{GetDataName(indexes)};");
        cluedProdList.Add("");
        cluedProdList.Add(GetExtraCodes(table, indexes));
        cluedProdList.Add("");
        cluedProdList.Add("//add edges");
        cluedProdList.Add("");

        foreach (var i in indexes)
        {
          if(!string.IsNullOrEmpty(primaryTableValues[i])) //we have a foreign key somewhere
          {
            var primaryTable = primaryTableValues[i];
            var primaryTableKeyColumnName = primarykeyColumnNameValues[i];

            cluedProdList.Add($@"if(input.{columnNameValues[i]} != null && !string.IsNullOrEmpty(input.{columnNameValues[i]}.ToString()))");
            cluedProdList.Add("{");

            cluedProdList.Add($@"_factory.CreateOutgoingEntityReference(clue, ""/{Normalize(primaryTable)}"", EntityEdgeType.AttachedTo, input.{columnNameValues[i]}, input.{columnNameValues[i]}.ToString());");

            cluedProdList.Add("}");

          }

        }


        cluedProdList.Add("");
        cluedProdList.Add(@"if (!data.OutgoingEdges.Any())
                          {
			                _factory.CreateEntityRootReference(clue, EntityEdgeType.PartOf);
                          }
							");
        cluedProdList.Add("");

        cluedProdList.Add($"var vocab = new {Normalize(table)}Vocabulary();");

        cluedProdList.Add("");


        foreach (var i in indexes)
        {
          var numberOfSpaces = "   "; //3 spaces for the longest name
          var columnValue = Normalize(columnNameValues[i]);

          var difSpaces = longestColumnName.Length - columnValue.Length;
          if (difSpaces > 0)
          {
            for (int j = 0; j < difSpaces; j++)
            {
              numberOfSpaces += " ";
            }
          }
          cluedProdList.Add($@"data.Properties[vocab.{columnValue}]{numberOfSpaces}= input.{columnValue}.PrintIfAvailable();");
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

        var indexes = Enumerable.Range(0, details.Count)
               .Where(i => details[i].Table == table)
               .ToList();


        crawlerCodeList.Add($@"foreach (var obj in client.GetObject<{Normalize(table)}>())");

        crawlerCodeList.Add("{");


        foreach (var i in indexes)
        {
          if (!string.IsNullOrEmpty(primaryTableValues[i])) //we have a foreign key somewhere
          {
            var primaryTable = primaryTableValues[i];
            var primaryTableKeyColumnName = primarykeyColumnNameValues[i];

            var notBaseTable = true;
            while (notBaseTable)
            {
              var baseDetail = details.FirstOrDefault(d => d.Table == primaryTable && d.ColumnName == primaryTableKeyColumnName);
              if (baseDetail != null && string.IsNullOrEmpty(baseDetail.PrimaryTable)) //this means that this is actually the base table because the primary key is not a foreign key
              {
                notBaseTable = false;
                crawlerCodeList.Add($@"if (!string.IsNullOrEmpty(obj.{columnNameValues[i]}?.ToString()))");
                crawlerCodeList.Add("{");

                if(string.IsNullOrEmpty(isPrimaryKeyValues[i]) && GetColumnType(i) == "string")
                {
                  crawlerCodeList.Add($@"foreach (var subObj in client.GetObject<{Normalize(primaryTable)}>(" + "$" + $@"""WHERE {primaryTableKeyColumnName} = " + "'{" + $"obj.{columnNameValues[i]}" + @"}'""" + "))");

                }
                else
                {
                  crawlerCodeList.Add($@"foreach (var subObj in client.GetObject<{Normalize(primaryTable)}>(" + "$" + $@"""WHERE {primaryTableKeyColumnName} = " + "{" + $"obj.{columnNameValues[i]}" + @"}""" + "))");

                }

                crawlerCodeList.Add("{");
                crawlerCodeList.Add("yield return subObj;");
                crawlerCodeList.Add("}");
                crawlerCodeList.Add("}");
                crawlerCodeList.Add("");
              }
              else
              {
                primaryTable = baseDetail.PrimaryTable;
                primaryTableKeyColumnName =  baseDetail.PkColumnName;
              }
            }
          }

        }

        crawlerCodeList.Add("yield return obj;");
        crawlerCodeList.Add("}");
        crawlerCodeList.Add("");
      }





      File.WriteAllLines($@"..\..\FilesOutput\crawlerCode\crawlerCode.cs", crawlerCodeList.ToArray());
    }

    //private static string Normalize(string name)
    //{
    //  name = $"{char.ToUpperInvariant(name[0])}{name.Substring(1)}";
    //  name = name.Replace(" ", "").Replace("/", "").Replace("_", "").Replace(".", "");

    //  return name;
    //}

    private static string Normalize(string name)
    {
      name = name.Replace("dbo.", "");
      name = name.ToLower();
      TextInfo txtInfo = new CultureInfo("en-us", false).TextInfo;
      name = txtInfo.ToTitleCase(name);
      name = name.Replace(" ", "").Replace("/", "").Replace("_", "");

      return name;
    }

    private static string GetReader(string value, int index)
    {
      if (!string.IsNullOrEmpty(isPrimaryKeyValues[index]))
        return $@"reader[""{value}""].ToString()";


      var type = GetColumnType(index);
      switch (type)
      {
        case "string":
          return $@"reader.GetStringValue(""{value}"")";
        default:
          return $@"reader.GetNullableValue<{type}>(""{value}"")";
      }
    }

    private static string GetColumnType(int index)
    {

      if (!string.IsNullOrEmpty(isPrimaryKeyValues[index]))
        return "string";

      var type = columnTypeValues[index];

      switch (type)
      {
        case "bigint":
          return "long?";
        case "bit":
          return "bool?";
        //case "varbinary":
        //  return "????";
        case "decimal":
        case "money":
        case "smallmoney":
        case "numeric":
          return "decimal?";
        case "float":
          return "double?";
        case "int":
        case "smallint":
          return "int?";
        case "tinyint":
          return "byte?";
        default:
          return "string";
      }
    }

    private static string GetDataType(int index)
    {
      var type = columnTypeValues[index];
      var column = columnNameValues[index];

      switch (type)
      {
        case "int":
        case "smallint":
        case "bigint":
          return "Integer";
        case "bit":
          return "Boolean";
        case "datetime":
          return "DateTime";
        case "decimal":
        case "float":
        case "numeric":
          return "Number";
        case "money":
        case "smallmoney":
          return "Money";
        default:
          return "Text";
      }
    }

    private static string GetVisibility(int index)
    {
      if (!string.IsNullOrEmpty(isPrimaryKeyValues[index]))
        return "HiddenInFrontendUI";

      return "Visible";
    }

    //private static object GetDisplayName(string column)
    //{
    //  //column = column.Replace("_", "");
    //  return $@".WithDisplayName(""{Regex.Replace(column, @"((?<=\p{Ll})\p{Lu})|((?!\A)\p{Lu}(?>\p{Ll}))", " $0")}"")";
    //}

    private static object GetDisplayName(string column)
    {
      column = column.ToLower();
      TextInfo txtInfo = new CultureInfo("en-us", false).TextInfo;
      column = txtInfo.ToTitleCase(column);
      column = column.Replace("_", " ");

      return $@".WithDisplayName(""{column}"")";
    }

    private static string GetEntityCode(List<int> indexes, bool ignoreRowGuid = false)
    {
      if (!ignoreRowGuid)
      {
        var rowGuidIndexExists = indexes.Any(i => columnNameValues[i].Equals("rowguid", StringComparison.InvariantCultureIgnoreCase));

        if (rowGuidIndexExists)
        {
          var rowGuidIndex = indexes.First(i => columnNameValues[i].Equals("rowguid", StringComparison.InvariantCultureIgnoreCase));

          return "$" + "\"{input." + Normalize(columnNameValues[rowGuidIndex]) + "}\"";
        }
      }


      var pkIndexes = indexes.Where(i => !string.IsNullOrEmpty(isPrimaryKeyValues[i])).ToArray();

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

    private static string GetDataName(List<int> indexes)
    {
      if (indexes.Any(i => columnNameValues[i].Equals("name", StringComparison.InvariantCultureIgnoreCase)))
        return "Name";

      return "FILL_IN";
    }

    private static string GetExtraCodes(string table, List<int> indexes)
    {
      var rowGuidIndexExists = indexes.Any(i => columnNameValues[i].Equals("rowguid", StringComparison.InvariantCultureIgnoreCase));

      if (!rowGuidIndexExists)
      {
        return "";
      }

      var pkIndexes = indexes.Where(i => !string.IsNullOrEmpty(isPrimaryKeyValues[i])).ToArray();

      return $@"data.Codes.Add(new EntityCode(""/{Normalize(table)}"", {crawlerName}Constants.CodeOrigin, " + GetEntityCode(indexes, true) + "));";

    }


  }
}
