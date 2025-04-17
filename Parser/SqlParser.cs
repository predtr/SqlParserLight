using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlParserLib.AST;
using SqlParserLib.Core;
using SqlParserLib.Exceptions;
using SqlParserLib.Interfaces;
using SqlParserLib.Lexer;

namespace SqlParserLib.Parser
{
    /// <summary>
    /// Main SQL Parser class that orchestrates the parsing process
    /// </summary>
    public class SqlParser : ISqlParser
    {
        private readonly ISqlLexer _lexer;
        private List<string> _skippedExpressions = new List<string>();
        private readonly HashSet<string> _functionKeywords;

        /// <summary>
        /// Gets any expressions that were skipped during parsing
        /// </summary>
        public List<string> SkippedExpressions => _skippedExpressions;

        /// <summary>
        /// Initializes a new instance of the SqlParser class with the default lexer
        /// </summary>
        public SqlParser() : this(new SqlLexer())
        {
            // Configure supported function keywords
            _functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ISNULL", "COALESCE", "DATEDIFF", "DATEADD", "CONVERT", "CAST", "COUNT",
                "MIN", "MAX", "AVG", "SUM", "UPPER", "LOWER", "SUBSTRING", "LEN", "ABS"
            };
        }

        /// <summary>
        /// Initializes a new instance of the SqlParser class with a custom lexer
        /// </summary>
        public SqlParser(ISqlLexer lexer)
        {
            _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
            
            // Initialize common function names for better recognition
            _functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ISNULL", "COALESCE", "DATEDIFF", "DATEADD", "CONVERT", "CAST", "COUNT",
                "SUM", "AVG", "MIN", "MAX", "SUBSTRING", "CONCAT", "REPLACE", "GETDATE",
                "UPPER", "LOWER", "TRIM", "LTRIM", "RTRIM", "ROUND", "CEILING", "FLOOR",
                "NULLIF", "STUFF", "YEAR", "MONTH", "DAY", "STRING_AGG"
            };
        }

        /// <summary>
        /// Parse a SQL query and return a structured representation
        /// </summary>
        public SqlStatement Parse(string sql)
        {
            // Reset skipped expressions
            _skippedExpressions = new List<string>();

            // Tokenize the SQL statement
            List<SqlToken> tokens = _lexer.Tokenize(sql);

            // Create a new parser context to track the parsing state
            var context = new ParserContext(tokens);

            // Parse the SELECT statement
            var selectParser = new SelectStatementParser(_functionKeywords);
            return selectParser.Parse(context, _skippedExpressions);
        }
    }
}