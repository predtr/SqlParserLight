using System;
using System.Collections.Generic;
using System.Text;

namespace SqlParserLib
{
	/// <summary>
	/// Context class to maintain state during parsing
	/// </summary>
	public class ParserContext
	{
		private readonly List<SqlToken> _tokens;
		private int _current = 0;

		public ParserContext(List<SqlToken> tokens)
		{
			_tokens = tokens;
		}

		/// <summary>
		/// Gets the current position in the token stream
		/// </summary>
		public int CurrentPosition => _current;

		/// <summary>
		/// Sets the current position in the token stream
		/// </summary>
		public void SetPosition(int position)
		{
			_current = Math.Max(0, Math.Min(position, _tokens.Count - 1));
		}

		/// <summary>
		/// Consumes the current token in peek mode (for looking ahead)
		/// </summary>
		public SqlToken PeekConsume()
		{
			int oldPosition = _current;
			if (!IsAtEnd()) _current++;
			SqlToken token = _tokens[oldPosition];
			return token;
		}

		/// <summary>
		/// Gets the token text between two positions
		/// </summary>
		public string GetTokensBetween(int start, int end)
		{
			StringBuilder sb = new StringBuilder();

			// Ensure valid range
			start = Math.Max(0, start);
			end = Math.Min(_tokens.Count - 1, end);

			for (int i = start; i < end; i++)
			{
				if (i > start) sb.Append(" ");
				sb.Append(_tokens[i].Lexeme);
			}

			return sb.ToString();
		}

		public bool IsAtEnd()
		{
			return Current().Type == SqlTokenType.EOF;
		}

		public SqlToken Current()
		{
			return _tokens[_current];
		}

		public SqlToken Previous()
		{
			return _tokens[_current - 1];
		}

		public SqlToken Consume()
		{
			if (!IsAtEnd()) _current++;
			return Previous();
		}

		public bool Match(SqlTokenType type)
		{
			if (Current().Type != type) return false;
			Consume();
			return true;
		}

		public bool MatchKeyword(SqlKeyword keyword)
		{
			if (Current().Type != SqlTokenType.KEYWORD ||
				(SqlKeyword)Current().Literal != keyword)
				return false;

			Consume();
			return true;
		}

		public void Expect(SqlTokenType type)
		{
			if (Current().Type != type)
			{
				throw new ParseException($"Expected {type} but got {Current().Type} at line {Current().Line}");
			}

			Consume();
		}

		public void ExpectKeyword(SqlKeyword keyword)
		{
			if (Current().Type != SqlTokenType.KEYWORD ||
				(SqlKeyword)Current().Literal != keyword)
			{
				throw new ParseException($"Expected keyword {keyword} but got {Current().Lexeme} at line {Current().Line}");
			}

			Consume();
		}

		/// <summary>
		/// Gets current token and looks ahead by N positions without consuming
		/// </summary>
		public SqlToken LookAhead(int positions)
		{
			int pos = _current + positions;
			if (pos >= _tokens.Count)
				return new SqlToken(SqlTokenType.EOF, "", null, -1); // Return EOF for beyond end

			return _tokens[pos];
		}

		public bool IsJoinKeyword()
		{
			if (Current().Type != SqlTokenType.KEYWORD) return false;

			SqlKeyword keyword = (SqlKeyword)Current().Literal;
			return keyword == SqlKeyword.JOIN ||
				   keyword == SqlKeyword.INNER ||
				   keyword == SqlKeyword.LEFT ||
				   keyword == SqlKeyword.RIGHT ||
				   keyword == SqlKeyword.FULL ||
				   keyword == SqlKeyword.CROSS;
		}

		public bool IsLogicalOperator()
		{
			if (Current().Type != SqlTokenType.KEYWORD) return false;

			SqlKeyword keyword = (SqlKeyword)Current().Literal;
			return keyword == SqlKeyword.AND || keyword == SqlKeyword.OR;
		}

		public bool IsAndOperator()
		{
			return Current().Type == SqlTokenType.KEYWORD &&
				   (SqlKeyword)Current().Literal == SqlKeyword.AND;
		}

		public bool IsOrOperator()
		{
			return Current().Type == SqlTokenType.KEYWORD &&
				   (SqlKeyword)Current().Literal == SqlKeyword.OR;
		}

		public bool IsClauseKeyword()
		{
			if (Current().Type != SqlTokenType.KEYWORD) return false;

			SqlKeyword keyword = (SqlKeyword)Current().Literal;
			return keyword == SqlKeyword.WHERE ||
				   keyword == SqlKeyword.GROUP ||
				   keyword == SqlKeyword.HAVING ||
				   keyword == SqlKeyword.ORDER;
		}

		public bool IsNextTokenInSelectList()
		{
			// Check if the next token indicates we're still in the SELECT list
			// This helps distinguish between column aliases and the next column in the list
			return Current().Type == SqlTokenType.COMMA ||
				   (Current().Type == SqlTokenType.KEYWORD &&
					((SqlKeyword)Current().Literal == SqlKeyword.FROM ||
					 (SqlKeyword)Current().Literal == SqlKeyword.WHERE));
		}
	}

	public class ParseException : Exception
	{
		public ParseException(string message) : base(message) { }
	}
}