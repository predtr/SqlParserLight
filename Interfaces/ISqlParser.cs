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
    }
}