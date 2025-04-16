using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SqlParserLib.Core;
using SqlParserLib.Lexer;

namespace SqlParserLib.Tests
{
    public class SqlLexerTests
    {
        [Fact]
        public void Tokenize_SimpleSelect_ReturnsCorrectTokens()
        {
            // Arrange
            var lexer = new SqlLexer();
            var sql = "SELECT id, name FROM users";
            
            // Act
            List<SqlToken> tokens = lexer.Tokenize(sql);
            
            // Assert
            Assert.NotNull(tokens);
            Assert.NotEmpty(tokens);
            
            // Check that we have the expected tokens
            Assert.Equal("SELECT", tokens[0].Value);
            Assert.Equal("id", tokens[1].Value);
            Assert.Equal(",", tokens[2].Value);
            Assert.Equal("name", tokens[3].Value);
            Assert.Equal("FROM", tokens[4].Value);
            Assert.Equal("users", tokens[5].Value);
        }
        
        [Fact]
        public void Tokenize_WithParameters_ReturnsCorrectTokens()
        {
            // Arrange
            var lexer = new SqlLexer();
            var sql = "SELECT * FROM users WHERE age > @minAge";
            
            // Act
            List<SqlToken> tokens = lexer.Tokenize(sql);
            
            // Assert
            Assert.NotNull(tokens);
            
            // Find parameter token
            var paramToken = tokens.FirstOrDefault(t => t.Value == "@minAge");
            Assert.NotNull(paramToken);
        }
        
        [Fact]
        public void Tokenize_WithStringLiteral_HandlesQuotesCorrectly()
        {
            // Arrange
            var lexer = new SqlLexer();
            var sql = "SELECT * FROM users WHERE name = 'John''s'";
            
            // Act
            List<SqlToken> tokens = lexer.Tokenize(sql);
            
            // Assert
            Assert.NotNull(tokens);
            
            // Find string literal token
            var stringToken = tokens.FirstOrDefault(t => t.Value == "'John''s'");
            Assert.NotNull(stringToken);
        }
    }
}