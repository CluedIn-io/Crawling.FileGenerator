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