using System;

namespace SqlParserLib
{
	/// <summary>
	/// Represents a token in the SQL lexical analysis
	/// </summary>
	public class SqlToken
	{
		public SqlTokenType Type { get; }
		public string Lexeme { get; }
		public object Literal { get; }
		public int Line { get; }

		public SqlToken(SqlTokenType type, string lexeme, object literal, int line)
		{
			Type = type;
			Lexeme = lexeme;
			Literal = literal;
			Line = line;
		}

		public override string ToString()
		{
			return $"{Type} {Lexeme} {Literal}";
		}
	}

	public enum SqlTokenType
	{
		// Single-character tokens
		LEFT_PAREN, RIGHT_PAREN, COMMA, DOT, MINUS, PLUS, STAR, SEMICOLON,

		// One or two character tokens
		EQUAL, BANG, BANG_EQUAL, NOT_EQUAL,
		LESS, LESS_EQUAL, GREATER, GREATER_EQUAL,

		// Literals
		IDENTIFIER, STRING, NUMBER,

		// Keywords
		KEYWORD,

		// Logical operators
		AND, OR,

		// Parameters (new)
		PARAMETER,

		EOF
	}

	public enum SqlKeyword
	{
		SELECT, FROM, WHERE, JOIN, INNER, LEFT, RIGHT, FULL, OUTER, CROSS,
		ON, AND, OR, AS, GROUP, ORDER, BY, HAVING,
		COALESCE, DATEDIFF, CASE, WHEN, THEN, ELSE, END, IS, NULL, NOT,
		DAY, MONTH, YEAR, HOUR, MINUTE, SECOND, ISNULL // Added ISNULL
	}
}