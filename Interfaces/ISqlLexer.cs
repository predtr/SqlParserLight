using System.Collections.Generic;
using SqlParserLib.Core;

namespace SqlParserLib.Interfaces
{
    /// <summary>
    /// Interface for SQL lexical analyzer components
    /// </summary>
    public interface ISqlLexer
    {
        /// <summary>
        /// Tokenize a SQL string into a list of tokens
        /// </summary>
        /// <param name="source">The SQL string to tokenize</param>
        /// <returns>A list of tokens</returns>
        List<SqlToken> Tokenize(string source);
    }
}