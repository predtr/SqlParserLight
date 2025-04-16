using SqlParserLib.AST;

namespace SqlParserLib.Interfaces
{
    /// <summary>
    /// Interface for SQL parser components
    /// </summary>
    public interface ISqlParser
    {
        /// <summary>
        /// Parse a SQL string into a structured representation
        /// </summary>
        /// <param name="sql">The SQL string to parse</param>
        /// <returns>A structured representation of the SQL</returns>
        SqlStatement Parse(string sql);
        
        /// <summary>
        /// Finds a column or alias in the SQL statement and generates a path from its table to the main table
        /// </summary>
        /// <param name="statement">The parsed SQL statement</param>
        /// <param name="dataFieldName">The column or alias name to search for</param>
        /// <param name="mainTable">Optional main table name to validate against</param>
        /// <returns>A result containing whether the field was found and its path</returns>
        ColumnPathResult GetColumnPath(SqlStatement statement, string dataFieldName, string mainTable = null);
    }
}