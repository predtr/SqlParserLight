namespace SqlParserLib.AST
{
    /// <summary>
    /// Result class for column path search operations
    /// </summary>
    public class ColumnPathResult
    {
        /// <summary>
        /// The data field name that was searched for
        /// </summary>
        public string DataFieldName { get; set; }
        
        /// <summary>
        /// Whether the field was found
        /// </summary>
        public bool Found { get; set; }
        
        /// <summary>
        /// Whether the result is a direct column
        /// </summary>
        public bool IsColumn { get; set; }
        
        /// <summary>
        /// Whether the result is a column alias
        /// </summary>
        public bool IsAlias { get; set; }
        
        /// <summary>
        /// Whether the result is a parameter
        /// </summary>
        public bool IsParameter { get; set; }
        
        /// <summary>
        /// The table name that contains the field
        /// </summary>
        public string TableName { get; set; }
        
        /// <summary>
        /// The full data table path to the field
        /// </summary>
        public string DataTablePath { get; set; }

        /// <summary>
        /// Returns a string representation of the result
        /// </summary>
        public override string ToString()
        {
            if (!Found)
                return $"Field '{DataFieldName}' not found in SQL statement.";

            if (IsParameter)
                return $"Found parameter '{DataFieldName}'.";

            string type = IsColumn ? "column" : (IsAlias ? "alias" : "field");
            return $"Found {type} '{DataFieldName}' in table '{TableName}'. Path: {DataTablePath}";
        }
    }
}