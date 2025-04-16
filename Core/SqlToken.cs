using System;

namespace SqlParserLib.Core
{
    /// <summary>
    /// Represents a token in a SQL statement
    /// </summary>
    public class SqlToken
    {
        /// <summary>
        /// The type of token
        /// </summary>
        public SqlTokenType Type { get; }

        /// <summary>
        /// The lexeme (raw text) of the token
        /// </summary>
        public string Lexeme { get; }

        /// <summary>
        /// The parsed literal value (for numbers, strings, keywords)
        /// </summary>
        public object Literal { get; }

        /// <summary>
        /// The line number where this token appears
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Creates a new SQL token
        /// </summary>
        public SqlToken(SqlTokenType type, string lexeme, object literal, int line)
        {
            Type = type;
            Lexeme = lexeme;
            Literal = literal;
            Line = line;
        }

        public override string ToString()
        {
            return $"{Type} {Lexeme} {(Literal != null ? Literal : "")}";
        }
    }

    /// <summary>
    /// Enum defining the types of SQL tokens
    /// </summary>
    public enum SqlTokenType
    {
        // Single-character tokens
        LEFT_PAREN, RIGHT_PAREN, COMMA, DOT, MINUS, PLUS, SEMICOLON, STAR,

        // One or two character tokens
        BANG, BANG_EQUAL, EQUAL, GREATER, GREATER_EQUAL, LESS, LESS_EQUAL, NOT_EQUAL,

        // Literals
        IDENTIFIER, STRING, NUMBER, PARAMETER,

        // Keywords
        KEYWORD,

        // End of file
        EOF
    }

    /// <summary>
    /// Enum defining SQL keywords
    /// </summary>
    public enum SqlKeyword
    {
        // Statement keywords
        SELECT, FROM, WHERE, JOIN, INNER, LEFT, RIGHT, FULL, OUTER, CROSS, ON, AS,
        GROUP, ORDER, BY, HAVING,

        // Logical operators
        AND, OR, NOT,

        // Function names
        COALESCE, ISNULL, DATEDIFF, DATEADD, CONVERT, CAST, COUNT, SUM, AVG, MIN, MAX,

        // CASE expression keywords
        CASE, WHEN, THEN, ELSE, END,

        // IS NULL check
        IS, NULL,

        // Date parts for functions like DATEDIFF
        DAY, MONTH, YEAR, HOUR, MINUTE, SECOND
    }
}