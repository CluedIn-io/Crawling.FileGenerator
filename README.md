# Documentation for the FileGenerator projects.

This repository contains 3 projects which can be used to generate crawling c# files based on data sources. Make sure to right-click on the project that you wish to run and click "Set as Startup Project".

## MetadataFile project

This project generates the files necessary for a CluedIn crawler to run (models, vocabularies, clue producers, and crawler code). These files will be copy-pasted in the crawler that has been generated (see http://documentation.cluedin.net/docs/10-Integration/build-integration.html). Please note that the crawlerCode file will not be copy-pasted, but its content will be copy-pasted in the `GetData` method of the Crawler class in the crawler (Crawling project).

**The project uses the standard CluedIn Crawler Metadata File**. This Excel spreadsheet is sent to the customer so that they sent it back to us filled in with the necessary fields. The customer can generate the metadata for the data source(s) automatically, and then have people work on the CluedIn Crawler Metadata File manually. The more details they fill in the file, the better crawler we will be able to generate and less manual work will be necessary further on.

The file contains the following columns:

* Entity / Table / Record / View	
* "CluedIn Vocabulary Name. Does this describe a real entity that you deal with and it is part of a CluedIn Vocabulary? (e.g. person, organization, product etc.). If yes, fill in here."
* "Custom Vocabulary Name. Does this describe a real entity that you deal with? (e.g. student, product, class etc.). If yes, and if considered important enough to have a custom vocabulary, enter the name here. Do not fill in if you already put a CluedIn Vocabulary for this table"
* Attribute / Column / Field
* "Display Name. Does the field have a better display name? Fill in if possible"
* "Description. What does this field mean? Fill in if possible"	
* Does this field contain an ID or GUID?
* "CluedIn DataType. What kind of data does this field hold? Write fields or choose them from the Dropdown if known/available. If not, leave blank or fill in a custom one. Try to be consistent"
* Unique Identifier Is this field unique or part of an unique combination of fields?
* System Data Type
* "Relationship. If the column is a primary key in another table, write it like this: PRIMARY_TABLE.FieldName. ####No need to fill this in if the relationships can be provided in a separate sheet or file."

The file must be converted from .xls to CSV and have deleted the two first rows (leaving just the pure metadata in).


***

### TO DO

**Depending on what the customer fills in and how the data source is, you might need to modify the following methods:**

* `Normalize(string name)`. Used to normalize the table names and column names. Depending on how the source names are, you might need to add extra code to bring them in a PascalCase format.
* `GetColumnType(int index)`. Converts the source data type (e.g. datetime2, nvarchar etc.) into a C# type (e.g. DateTime, string). These original source types vary from system to system
* `GetDataTypeFromColumnType(int index)`. Converts the source data type (e.g. datetime2, nvarchar etc.) into a CluedIn VocabularyKeyDataType (e.g. Text, Integer, Identifier, Money etc.)
* `GetReader(string value, int index)`. Returns the name of a function which is used to set a value to the field in the model's class' constructor. This is crucial for retrieving the entity objects from the actual source into the crawler's C# object (_Models_). This may vary broadly, depending on the data source. As a general good practise, you should write something basic here at the beginning, just for the crawler not to crash, and then when you know more about how to integrate with the data source, you can write the code here to generate the necessary code in the crawler. Then you must write the implementation of the method(s) in the actual crawler. For example, the base version of this generator generates two types of methods. For `string` types, it generates the method `GetStringValue("_originalSourceColumnName_")`, and for other types, it generates `GetNullableValue<_columnType_>("_originalSourceColumnName_")`. These methods will be then implemented in C# code in the crawler. **Very important!!!** - In some cases, the instantiation of each Model may be done differently (for example, by using attributes that match the original source name). This will have to be modified accordingly in the generator, in the `CreateModels()` function. 

#### Other things to pay attention to: 
* `GetDisplayName(string value)` - It can happen that the customer does not provide the display names. In this case, this method may be modified to generate them (if possible), by adding spaces in the right places.
* Create the folder structure (FilesOutput -> models; vocabs; clueproducers)
* Make sure the csv file is processed correctly. Pay attention to the `.Skip(0)` method, if it's used.
* Make sure to update the `crawlerName` constant to match the name of the crawler you generated initially for the data source.
* Make sure you create the _BaseCrawlerNameEntity_ in your crawler solution, with the correct constructors. This class may not even be needed, depending on the data source you're crawling.


## CSVFileGenerator

This project generates the files necessary for a CluedIn crawler to run (models, vocabularies, clue producers). These files will be copy-pasted in the crawler that has been generated (see http://documentation.cluedin.net/docs/10-Integration/build-integration.html). 

The project iterates through CSV files with sample data in them and generates the files. 

The CSVFileGenerator project works in a very similar manner to the MetadataFile project. Please keep in mind the things mentioned in the _**TO DO**_ and _**Other things to pay attention to**_ sections. Also keep in mind other methods 

## SQLServer

This project generates the files necessary for a CluedIn crawler to run (models, vocabularies, clue producers, crawlerCode). These files will be copy-pasted in the crawler that has been generated (see http://documentation.cluedin.net/docs/10-Integration/build-integration.html). Please note that the crawlerCode file will not be copy-pasted, but its content will be copy-pasted in the `GetData` method of the Crawler class in the crawler (Crawling project).

The project retrieves the metadata from a SQL server. For this, it needs a connection string. If the connection string is not possible, it can also use a csv file that is structured in the same way as it would have been retrieved from the database. This csv file must be provided by the customer, who can run a script that can be found in the project (SQLServerMetadataExtractionScript). Note that this script may need further modifications in order to work in the customer's environment and to include the tables that actually need to be crawled.


The SQLServer project works in a very similar manner to the MetadataFile project. Please keep in mind the things mentioned in the _**TO DO**_ and _**Other things to pay attention to**_ sections. Also keep in mind other methods 
