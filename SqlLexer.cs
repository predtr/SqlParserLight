using System;
using System.Collections.Generic;
using System.Text;

namespace SqlParserLib
{
	/// <summary>
	/// SQL Lexer to tokenize SQL statements
	/// </summary>
	public class SqlLexer
	{
		private string _source;
		private List<SqlToken> _tokens = new List<SqlToken>();
		private int _start = 0;
		private int _current = 0;
		private int _line = 1;

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
			{ "ISNULL", SqlKeyword.ISNULL } // Added ISNULL function
        };

		public List<SqlToken> Tokenize(string source)
		{
			_source = source;
			_tokens = new List<SqlToken>();
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

				// Handle parameters (new)
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
						// Just ignore unexpected characters for now
						// In a real implementation, you'd report an error
					}
					break;
			}
		}

		private void Parameter()
		{
			// Skip the @ that was already consumed
			_start = _current;

			// Parameter name can contain letters, digits, and underscores
			while (IsAlphaNumeric(Peek()) || Peek() == '_') Advance();

			// Extract parameter name without the @ prefix
			string parameterName = "@" + _source.Substring(_start, _current - _start);

			// Add token
			AddToken(SqlTokenType.PARAMETER, parameterName);
		}

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

			AddToken(SqlTokenType.NUMBER, double.Parse(_source.Substring(_start, _current - _start)));
		}

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
				// In a real implementation, you'd report an error
				return;
			}

			// Consume the closing quote
			Advance();

			// Extract the string without the quotes
			string value = _source.Substring(_start + 1, _current - _start - 2);
			AddToken(SqlTokenType.STRING, value);
		}

		private bool Match(char expected)
		{
			if (IsAtEnd()) return false;
			if (_source[_current] != expected) return false;

			_current++;
			return true;
		}

		private char Peek()
		{
			if (IsAtEnd()) return '\0';
			return _source[_current];
		}

		private char PeekNext()
		{
			if (_current + 1 >= _source.Length) return '\0';
			return _source[_current + 1];
		}

		private bool IsAlpha(char c)
		{
			return (c >= 'a' && c <= 'z') ||
				   (c >= 'A' && c <= 'Z') ||
					c == '_';
		}

		private bool IsAlphaNumeric(char c)
		{
			return IsAlpha(c) || IsDigit(c);
		}

		private bool IsDigit(char c)
		{
			return c >= '0' && c <= '9';
		}

		private bool IsAtEnd()
		{
			return _current >= _source.Length;
		}

		private char Advance()
		{
			return _source[_current++];
		}

		private void AddToken(SqlTokenType type)
		{
			AddToken(type, null);
		}

		private void AddToken(SqlTokenType type, object literal)
		{
			string text = _source.Substring(_start, _current - _start);
			_tokens.Add(new SqlToken(type, text, literal, _line));
		}
	}
}