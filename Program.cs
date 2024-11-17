using System.Net;
using DNS.Client;
using DNS.Client.RequestResolver;
using DNS.Protocol;
using DNS.Protocol.ResourceRecords;
using DNS.Server;
using DnsRewriteServer;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var config = configuration.Get<Config>();

if (config == null)
{
    Console.WriteLine("Config is null");
    return;
}

var listenEndpoint = IPEndPoint.Parse(config.ListenAddress);
var upstreamEndpoint = IPEndPoint.Parse(config.UpstreamDns);

var upstreamDns = new DnsClient(upstreamEndpoint);

var server = new DnsServer(new LocalRequestResolver(config, upstreamDns));

server.Listening += (sender, e) => Console.WriteLine("Listening on {0}", listenEndpoint);
server.Errored += (sender, e) => Console.WriteLine(e.Exception.Message);

await server.Listen(listenEndpoint);

return;

public class LocalRequestResolver(Config config, DnsClient? upstreamDns = null) : IRequestResolver
{

    public async Task<IResponse> Resolve(IRequest request, CancellationToken cancellationToken = default)
    {
        IResponse response = Response.FromRequest(request);

        foreach (Question question in response.Questions)
        {
            if (config.RewriteEntries.TryGetValue(question.Name.ToString(), out string? ip))
            {
                IResourceRecord record = new IPAddressResourceRecord(
                    question.Name, IPAddress.Parse(ip));
                response.AnswerRecords.Add(record);
                response.ResponseCode = ResponseCode.NoError;

                Console.WriteLine($"Rewrote {question.Name} to {ip}");
            }
            else if (upstreamDns != null)
            {
                var upstreamResponse = await upstreamDns.Resolve(question.Name, RecordType.A, cancellationToken);
                foreach (var record in upstreamResponse.AnswerRecords)
                {
                    response.AnswerRecords.Add(record);
                }
                response.RecursionAvailable = upstreamResponse.RecursionAvailable;
                response.AuthorativeServer = upstreamResponse.AuthorativeServer;
                response.ResponseCode = upstreamResponse.ResponseCode;
            }
            else
            {
                response.ResponseCode = ResponseCode.ServerFailure;
            }
        }

        return response;
    }
}
