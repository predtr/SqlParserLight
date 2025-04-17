using System;
using System.Collections.Generic;
using System.Text;
using SqlParserLib.Core;
using SqlParserLib.Interfaces;

namespace SqlParserLib.Lexer
{
	/// <summary>
	/// SQL Lexer to tokenize SQL statements into a sequence of tokens
	/// that can be processed by the parser.
	/// </summary>
	public class SqlLexer : ISqlLexer
	{
		private string _source = string.Empty;
		private List<SqlToken> _tokens = new List<SqlToken>();
		private int _start = 0;
		private int _current = 0;
		private int _line = 1;
		private List<string> _errors = new List<string>();

		/// <summary>
		/// Gets any lexical errors that occurred during tokenization
		/// </summary>
		public IReadOnlyList<string> Errors => _errors;
		
		/// <summary>
		/// Dictionary of SQL keywords recognized by the lexer
		/// </summary>
		private static readonly Dictionary<string, SqlKeyword> _keywords = new Dictionary<string, SqlKeyword>(StringComparer.OrdinalIgnoreCase)
		{
			{ "SELECT", SqlKeyword.SELECT },
			{ "FROM", SqlKeyword.FROM },
			{ "WHERE", SqlKeyword.WHERE },
			{ "JOIN", SqlKeyword.JOIN },
			{ "INNER", SqlKeyword.INNER },
			{ "LEFT", SqlKeyword.LEFT },
			{ "RIGHT", SqlKeyword.RIGHT },
			{ "FULL", SqlKeyword.FULL },
			{ "OUTER", SqlKeyword.OUTER },
			{ "CROSS", SqlKeyword.CROSS },
			{ "ON", SqlKeyword.ON },
			{ "AND", SqlKeyword.AND },
			{ "OR", SqlKeyword.OR },
			{ "AS", SqlKeyword.AS },
			{ "GROUP", SqlKeyword.GROUP },
			{ "ORDER", SqlKeyword.ORDER },
			{ "BY", SqlKeyword.BY },
			{ "HAVING", SqlKeyword.HAVING },
			{ "COALESCE", SqlKeyword.COALESCE },
			{ "DATEDIFF", SqlKeyword.DATEDIFF },
			{ "DATEADD", SqlKeyword.DATEADD },
			{ "CONVERT", SqlKeyword.CONVERT },
			{ "CAST", SqlKeyword.CAST },
			{ "CASE", SqlKeyword.CASE },
			{ "WHEN", SqlKeyword.WHEN },
			{ "THEN", SqlKeyword.THEN },
			{ "ELSE", SqlKeyword.ELSE },
			{ "END", SqlKeyword.END },
			{ "IS", SqlKeyword.IS },
			{ "NULL", SqlKeyword.NULL },
			{ "NOT", SqlKeyword.NOT },
			{ "DAY", SqlKeyword.DAY },
			{ "MONTH", SqlKeyword.MONTH },
			{ "YEAR", SqlKeyword.YEAR },
			{ "HOUR", SqlKeyword.HOUR },
			{ "MINUTE", SqlKeyword.MINUTE },
			{ "SECOND", SqlKeyword.SECOND },
			{ "ISNULL", SqlKeyword.ISNULL },
			{ "COUNT", SqlKeyword.COUNT },
			{ "SUM", SqlKeyword.SUM },
			{ "AVG", SqlKeyword.AVG },
			{ "MIN", SqlKeyword.MIN },
			{ "MAX", SqlKeyword.MAX }
        };

		/// <summary>
		/// Tokenizes a SQL string into a list of tokens
		/// </summary>
		/// <param name="source">The SQL string to tokenize</param>
		/// <returns>A list of tokens</returns>
		public List<SqlToken> Tokenize(string source)
		{
			_source = source ?? throw new ArgumentNullException(nameof(source));
			_tokens = new List<SqlToken>();
			_errors = new List<string>();
			_start = 0;
			_current = 0;
			_line = 1;

			while (!IsAtEnd())
			{
				_start = _current;
				ScanToken();
			}

			_tokens.Add(new SqlToken(SqlTokenType.EOF, "", null, _line));
			return _tokens;
		}

		/// <summary>
		/// Scans the next token from the source string
		/// </summary>
		private void ScanToken()
		{
			char c = Advance();
			switch (c)
			{
				case '(': AddToken(SqlTokenType.LEFT_PAREN); break;
				case ')': AddToken(SqlTokenType.RIGHT_PAREN); break;
				case ',': AddToken(SqlTokenType.COMMA); break;
				case '.': AddToken(SqlTokenType.DOT); break;
				case '+': AddToken(SqlTokenType.PLUS); break;
				case '*': AddToken(SqlTokenType.STAR); break;
				case ';': AddToken(SqlTokenType.SEMICOLON); break;

				case '=': AddToken(SqlTokenType.EQUAL); break;
				case '!': AddToken(Match('=') ? SqlTokenType.BANG_EQUAL : SqlTokenType.BANG); break;
				case '<':
					if (Match('=')) AddToken(SqlTokenType.LESS_EQUAL);
					else if (Match('>')) AddToken(SqlTokenType.NOT_EQUAL);
					else AddToken(SqlTokenType.LESS);
					break;
				case '>': AddToken(Match('=') ? SqlTokenType.GREATER_EQUAL : SqlTokenType.GREATER); break;

				case '\'': String(); break;

				// Handle parameters (e.g., @paramName)
				case '@': Parameter(); break;

				case ' ':
				case '\r':
				case '\t':
					// Ignore whitespace
					break;

				case '\n':
					_line++;
					break;

				case '-':
					// Check for comment
					if (Match('-'))
					{
						// Comment until end of line
						while (Peek() != '\n' && !IsAtEnd()) Advance();
					}
					else
					{
						AddToken(SqlTokenType.MINUS);
					}
					break;

				case '/':
					// Check for block comment
					if (Match('*'))
					{
						BlockComment();
					}
					else
					{
						 // Treat as a division operator
						AddToken(SqlTokenType.IDENTIFIER, "/");
					}
					break;

				default:
					if (IsDigit(c))
					{
						Number();
					}
					else if (IsAlpha(c))
					{
						Identifier();
					}
					else
					{
						_errors.Add($"Unexpected character '{c}' at line {_line}");
					}
					break;
			}
		}

		/// <summary>
		/// Processes a block comment (/* ... */)
		/// </summary>
		private void BlockComment()
		{
			// Keep consuming until we find the closing */
			while (!IsAtEnd() && !(Peek() == '*' && PeekNext() == '/'))
			{
				if (Peek() == '\n') _line++;
				Advance();
			}

			// If we reached the end of the file without closing the comment
			if (IsAtEnd())
			{
				_errors.Add($"Unterminated block comment at line {_line}");
				return;
			}

			// Consume the closing */
			Advance(); // *
			Advance(); // /
		}

		/// <summary>
		/// Processes a SQL parameter (e.g. @paramName)
		/// </summary>
		private void Parameter()
		{
			// Skip the @ that was already consumed
			_start = _current - 1; // Include the @ in the parameter name

			// Parameter name can contain letters, digits, and underscores
			while (IsAlphaNumeric(Peek()) || Peek() == '_') Advance();

			// Extract parameter name with the @ prefix
			string parameterName = _source.Substring(_start, _current - _start);

			// Add token
			AddToken(SqlTokenType.PARAMETER, parameterName);
		}

		/// <summary>
		/// Processes a SQL identifier (table name, column name, etc.)
		/// </summary>
		private void Identifier()
		{
			while (IsAlphaNumeric(Peek())) Advance();

			string text = _source.Substring(_start, _current - _start);

			// Check if it's a keyword
			if (_keywords.TryGetValue(text, out var keyword))
			{
				AddToken(SqlTokenType.KEYWORD, keyword);
			}
			else
			{
				AddToken(SqlTokenType.IDENTIFIER);
			}
		}

		/// <summary>
		/// Processes a numeric literal
		/// </summary>
		private void Number()
		{
			while (IsDigit(Peek())) Advance();

			// Look for a decimal part
			if (Peek() == '.' && IsDigit(PeekNext()))
			{
				// Consume the "."
				Advance();

				// Keep consuming digits
				while (IsDigit(Peek())) Advance();
			}

			// Parse the number
			string numberStr = _source.Substring(_start, _current - _start);
			if (double.TryParse(numberStr, out double value))
			{
				AddToken(SqlTokenType.NUMBER, value);
			}
			else
			{
				_errors.Add($"Invalid number format '{numberStr}' at line {_line}");
				AddToken(SqlTokenType.NUMBER, 0); // Default to 0 on error
			}
		}

		/// <summary>
		/// Processes a string literal
		/// </summary>
		private void String()
		{
			// Keep consuming characters until we reach the closing quote
			while (Peek() != '\'' && !IsAtEnd())
			{
				if (Peek() == '\n') _line++;
				Advance();
			}

			// Unterminated string
			if (IsAtEnd())
			{
				_errors.Add($"Unterminated string at line {_line}");
				return;
			}

			// Consume the closing quote
			Advance();

			// Extract the string without the quotes
			string value = _source.Substring(_start + 1, _current - _start - 2);
			AddToken(SqlTokenType.STRING, value);
		}

		/// <summary>
		/// Checks if the next character matches the expected character
		/// </summary>
		private bool Match(char expected)
		{
			if (IsAtEnd()) return false;
			if (_source[_current] != expected) return false;

			_current++;
			return true;
		}

		/// <summary>
		/// Peeks at the current character without consuming it
		/// </summary>
		private char Peek()
		{
			if (IsAtEnd()) return '\0';
			return _source[_current];
		}

		/// <summary>
		/// Peeks at the next character without consuming it
		/// </summary>
		private char PeekNext()
		{
			if (_current + 1 >= _source.Length) return '\0';
			return _source[_current + 1];
		}

		/// <summary>
		/// Checks if a character is alphabetic
		/// </summary>
		private bool IsAlpha(char c)
		{
			return (c >= 'a' && c <= 'z') ||
				   (c >= 'A' && c <= 'Z') ||
				   c == '_';
		}

		/// <summary>
		/// Checks if a character is alphanumeric
		/// </summary>
		private bool IsAlphaNumeric(char c)
		{
			return IsAlpha(c) || IsDigit(c);
		}

		/// <summary>
		/// Checks if a character is a digit
		/// </summary>
		private bool IsDigit(char c)
		{
			return c >= '0' && c <= '9';
		}

		/// <summary>
		/// Checks if we've reached the end of the source string
		/// </summary>
		private bool IsAtEnd()
		{
			return _current >= _source.Length;
		}

		/// <summary>
		/// Advances the current position and returns the character
		/// </summary>
		private char Advance()
		{
			return _source[_current++];
		}

		/// <summary>
		/// Adds a token to the token list
		/// </summary>
		private void AddToken(SqlTokenType type)
		{
			// Pass null explicitly as the literal value
			AddToken(type, null);
		}

		/// <summary>
		/// Adds a token with a literal value to the token list
		/// </summary>
		private void AddToken(SqlTokenType type, object literal)
		{
			// Create the token with the extracted text and provided literal value
			string text = _source.Substring(_start, _current - _start);
			_tokens.Add(new SqlToken(type, text, literal, _line));
		}
	}
}