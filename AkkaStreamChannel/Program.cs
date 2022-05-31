using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Channels;
using Akka.Streams.Dsl;

using System.Threading.Channels;

using var system = ActorSystem.Create("system");
using var materializer = system.Materializer();

Channel<int> channel = Channel.CreateUnbounded<int>();

var list = Enumerable.Range(1, 100000);

var channelSource = ChannelSource.FromReader<int>(channel.Reader);

var task = channelSource
    .GroupedWithin(1000, TimeSpan.FromSeconds(1))
    .RunWith(Sink.Ignore<IEnumerable<int>>(), materializer);

var workActor = system.ActorOf(WorkActor.Props(channel.Writer), "workActor");

foreach (var item in list)
{
    workActor.Tell(item);
}

await task.ContinueWith((result) =>
{
    if (result.IsCanceled)
        Console.WriteLine("Task was canceled");
    else if (result.IsFaulted)
    {
        var status = result.Status;
        Console.WriteLine($"ChannelSource Task failed with exception {result.Exception.InnerException}, but channel is still alive with {channel.Reader.Count} items");

    }
    else
        Console.WriteLine("Task completed");
});


Console.Read();

public class WorkActor : ReceiveActor
{
    private readonly ChannelWriter<int> _writer;

    public WorkActor(ChannelWriter<int> writer)
    {
        _writer = writer;
        Receive<int>(async item =>
        {
            _writer.TryWrite(item);
        });
    }

    public static Props Props(ChannelWriter<int> writer)
    {
        return Akka.Actor.Props.Create(() => new WorkActor(writer));
    }
}