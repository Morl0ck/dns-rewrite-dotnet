namespace DnsRewriteServer;

public record Config
{
    public Dictionary<string, string> RewriteEntries { get; set; } = [];
    public string ListenAddress { get; set; } = string.Empty;
    public string UpstreamDns { get; set; } = string.Empty;
}
