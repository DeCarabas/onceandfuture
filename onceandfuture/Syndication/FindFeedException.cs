namespace OnceAndFuture.Syndication
{
    using System;

    class FindFeedException : Exception
    {
        public FindFeedException(string message, params object[] args) : base(String.Format(message, args)) { }
    }
}
