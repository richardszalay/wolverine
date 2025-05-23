using JasperFx.Core;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Wolverine.ComplianceTests;
using Wolverine.Tracking;
using Xunit;

namespace Wolverine.Pubsub.Tests.ConventionalRouting;

public class end_to_end_with_conventional_routing_with_prefix : IDisposable
{
    private readonly IHost _receiver;
    private readonly IHost _sender;

    public end_to_end_with_conventional_routing_with_prefix()
    {
        _sender = WolverineHost.For(opts =>
        {
            opts
                .UsePubsubTesting()
                .AutoProvision()
                .AutoPurgeOnStartup()
                .EnableDeadLettering()
                .EnableSystemEndpoints()
                .PrefixIdentifiers("shazaam")
                .UseConventionalRouting();

            opts.DisableConventionalDiscovery();

            opts.ServiceName = "Sender";
        });

        _receiver = WolverineHost.For(opts =>
        {
            opts
                .UsePubsubTesting()
                .AutoProvision()
                .AutoPurgeOnStartup()
                .EnableDeadLettering()
                .EnableSystemEndpoints()
                .PrefixIdentifiers("shazaam")
                .UseConventionalRouting();

            opts.ServiceName = "Receiver";
        });
    }

    public void Dispose()
    {
        _sender?.Dispose();
        _receiver?.Dispose();
    }

    [Fact]
    public async Task send_from_one_node_to_another_all_with_conventional_routing()
    {
        var session = await _sender
            .TrackActivity()
            .AlsoTrack(_receiver)
            .IncludeExternalTransports()
            .Timeout(30.Seconds())
            .SendMessageAndWaitAsync(new RoutedMessage());

        var received = session
            .AllRecordsInOrder()
            .Where(x => x.Envelope.Message?.GetType() == typeof(RoutedMessage))
            .Single(x => x.MessageEventType == MessageEventType.Received);

        received.ServiceName.ShouldBe("Receiver");
        received.Envelope.Destination.ShouldBe(new Uri($"{PubsubTransport.ProtocolName}://wolverine/shazaam.routed"));
    }
}