using System;
using System.Collections.Generic;
using System.Text;
using SqlParserLib.Core;
using SqlParserLib.Exceptions;

namespace SqlParserLib.Parser
{
    /// <summary>
    /// Maintains context during SQL parsing operations
    /// </summary>
    public class ParserContext
    {
        private readonly List<SqlToken> _tokens;
        private int _currentPosition = 0;

        /// <summary>
        /// Gets the current token position
        /// </summary>
        public int CurrentPosition => _currentPosition;

        /// <summary>
        /// Gets the internal token list - used for advanced peeking operations
        /// </summary>
        public List<SqlToken> Tokens => _tokens;

        /// <summary>
        /// Creates a new parser context with the given tokens
        /// </summary>
        public ParserContext(List<SqlToken> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        }

        /// <summary>
        /// Checks if we've reached the end of the token stream
        /// </summary>
        public bool IsAtEnd()
        {
            return Current().Type == SqlTokenType.EOF;
        }

        /// <summary>
        /// Gets the current token without advancing
        /// </summary>
        public SqlToken Current()
        {
            if (_currentPosition >= _tokens.Count)
                return new SqlToken(SqlTokenType.EOF, "", null, -1);
                
            return _tokens[_currentPosition];
        }

        /// <summary>
        /// Returns the token at the specified position ahead of the current position
        /// </summary>
        public SqlToken LookAhead(int count)
        {
            int position = _currentPosition + count;
            if (position >= _tokens.Count)
                return new SqlToken(SqlTokenType.EOF, "", null, -1);
                
            return _tokens[position];
        }

        /// <summary>
        /// Advances to the next token and returns it
        /// </summary>
        public SqlToken Consume()
        {
            if (!IsAtEnd())
                _currentPosition++;
                
            return _tokens[_currentPosition - 1];
        }

        /// <summary>
        /// Peeks at the next token without permanently advancing the position
        /// </summary>
        public SqlToken PeekConsume()
        {
            if (_currentPosition >= _tokens.Count - 1)
                return new SqlToken(SqlTokenType.EOF, "", null, -1);
            
            // Save current position
            int originalPosition = _currentPosition;
            
            // Temporarily advance and get the token
            _currentPosition++;
            SqlToken token = _tokens[_currentPosition - 1];
            
            // Restore original position
            _currentPosition = originalPosition;
            
            return token;
        }

        /// <summary>
        /// Sets the current position to a specific index
        /// </summary>
        public void SetPosition(int position)
        {
            if (position < 0 || position >= _tokens.Count)
                throw new ArgumentOutOfRangeException(nameof(position));
                
            _currentPosition = position;
        }

        /// <summary>
        /// Returns true and advances if the current token is a keyword with the specified value
        /// </summary>
        public bool MatchKeyword(SqlKeyword keyword)
        {
            if (Current().Type == SqlTokenType.KEYWORD && 
                Current().Literal != null && (SqlKeyword)Current().Literal == keyword)
            {
                Consume();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Returns true and advances if the current token is of the specified type
        /// </summary>
        public bool Match(SqlTokenType type)
        {
            if (Current().Type == type)
            {
                Consume();
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Verifies that the current token is a keyword with the specified value, then advances
        /// </summary>
        public void ExpectKeyword(SqlKeyword keyword)
        {
            if (Current().Type != SqlTokenType.KEYWORD || 
                Current().Literal == null || (SqlKeyword)Current().Literal != keyword)
            {
                throw new ParseException(
                    $"Expected keyword {keyword} but found '{Current().Lexeme}'", 
                    Current().Line);
            }
            
            Consume();
        }

        /// <summary>
        /// Verifies that the current token is of the specified type, then advances
        /// </summary>
        public void Expect(SqlTokenType type)
        {
            if (Current().Type != type)
            {
                throw new ParseException(
                    $"Expected {type} but found '{Current().Lexeme}'", 
                    Current().Line);
            }
            
            Consume();
        }

        /// <summary>
        /// Gets all tokens between specified start and end positions
        /// </summary>
        public string GetTokensBetween(int startPos, int endPos)
        {
            var builder = new StringBuilder();
            
            for (int i = startPos; i < endPos && i < _tokens.Count; i++)
            {
                builder.Append(_tokens[i].Lexeme).Append(" ");
            }
            
            return builder.ToString().Trim();
        }

        /// <summary>
        /// Gets the token at a specific position without changing the current position
        /// </summary>
        public SqlToken GetTokenAt(int position)
        {
            if (position < 0 || position >= _tokens.Count)
                return new SqlToken(SqlTokenType.EOF, "", null, -1);
                
            return _tokens[position];
        }

        /// <summary>
        /// Checks if the current token is a JOIN keyword
        /// </summary>
        public bool IsJoinKeyword()
        {
            if (Current().Type != SqlTokenType.KEYWORD || Current().Literal == null)
                return false;
                
            SqlKeyword keyword = (SqlKeyword)Current().Literal;
            return keyword == SqlKeyword.JOIN || 
                   keyword == SqlKeyword.INNER || 
                   keyword == SqlKeyword.LEFT || 
                   keyword == SqlKeyword.RIGHT || 
                   keyword == SqlKeyword.FULL || 
                   keyword == SqlKeyword.CROSS;
        }

        /// <summary>
        /// Checks if the next token is part of the column list (not FROM or comma)
        /// </summary>
        public bool IsNextTokenInSelectList()
        {
            return Current().Type == SqlTokenType.COMMA || 
                  (Current().Type == SqlTokenType.KEYWORD && 
                   Current().Literal != null &&
                   (SqlKeyword)Current().Literal == SqlKeyword.FROM);
        }
    }
}