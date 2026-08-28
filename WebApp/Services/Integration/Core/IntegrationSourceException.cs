using System;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration
{
    public class IntegrationSourceException : Exception
    {
        public IntegrationSourceException(IntegrationSource source, int? statusCode, string message)
            : base(message)
        {
            Source = source;
            StatusCode = statusCode;
        }

        public new IntegrationSource Source { get; }
        public int? StatusCode { get; }
    }
}
