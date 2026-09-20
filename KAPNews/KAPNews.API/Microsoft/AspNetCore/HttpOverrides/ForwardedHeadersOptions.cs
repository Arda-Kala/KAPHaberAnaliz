// KAPNews.API.Program.cs






namespace Microsoft.AspNetCore.HttpOverrides
{
    internal class ForwardedHeadersOptions : Builder.ForwardedHeadersOptions
    {
        public ForwardedHeaders ForwardedHeaders { get; set; }
    }
}