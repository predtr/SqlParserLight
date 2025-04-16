using System;

namespace SqlParserLib.Exceptions
{
    /// <summary>
    /// Represents an error that occurs during SQL parsing.
    /// </summary>
    public class ParseException : Exception
    {
        /// <summary>
        /// Gets the line number where the parse error occurred.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Gets the column position where the parse error occurred.
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// Gets any token or text that could not be parsed.
        /// </summary>
        public string? InvalidToken { get; }

        /// <summary>
        /// Initializes a new instance of the ParseException class.
        /// </summary>
        /// <param name="message">The error message</param>
        public ParseException(string message) : base(message)
        {
            Line = -1;
            Column = -1;
        }

        /// <summary>
        /// Initializes a new instance of the ParseException class with line information.
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="line">The line number where the error occurred</param>
        public ParseException(string message, int line) : base(message)
        {
            Line = line;
            Column = -1;
        }

        /// <summary>
        /// Initializes a new instance of the ParseException class with line and column information.
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="line">The line number where the error occurred</param>
        /// <param name="column">The column position where the error occurred</param>
        public ParseException(string message, int line, int column) : base(message)
        {
            Line = line;
            Column = column;
        }

        /// <summary>
        /// Initializes a new instance of the ParseException class with token information.
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="line">The line number where the error occurred</param>
        /// <param name="invalidToken">The token that could not be parsed</param>
        public ParseException(string message, int line, string invalidToken) : base(message)
        {
            Line = line;
            Column = -1;
            InvalidToken = invalidToken;
        }

        /// <summary>
        /// Initializes a new instance of the ParseException class with an inner exception.
        /// </summary>
        /// <param name="message">The error message</param>
        /// <param name="innerException">The inner exception</param>
        public ParseException(string message, Exception innerException) : base(message, innerException)
        {
            Line = -1;
            Column = -1;
        }
    }
}