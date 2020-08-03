using System.Data.SqlClient;

namespace SQLServer
{
  internal class SchemaDetail
  {
	public SchemaDetail(SqlDataReader reader)
	{
	  Table = reader["Table"].ToString();
	  ColumnName = reader["ColumnName"].ToString();
	  ColumnDescription = reader["ColumnDescription"].ToString();
	  ColumnType = reader["ColumnType"].ToString();
	  IsPrimaryKey = reader["isPrimaryKey"].ToString();
	  PrimaryTable = reader["PrimaryTable"].ToString();
	  PkColumnName = reader["PkColumnName"].ToString();
	}

	public SchemaDetail()
	{

	}

	public string Table { get; set; }
	public string ColumnName { get; set; }
	public string ColumnDescription { get; set; }
	public string ColumnType { get; set; }
	public string IsPrimaryKey { get; set; }
	public string PrimaryTable { get; set; }
	public string PkColumnName { get; set; }
  }
}