using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using B3.Umdf.Book;
using B3.Umdf.FixConflated;

BenchmarkSwitcher.FromAssemblies([typeof(FixApplicationMessageWriterBenchmarks).Assembly]).Run(args);

// Direct encode hot path: FixApplicationMessageWriter builds the raw FIX
// tag=value frame with no conflation/queueing involved. Mirrors how
// FixConflatedMarketDataPublisher.EmitTrade / FlushBuffered call it once a
// batch of entries has already been assembled.
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class FixApplicationMessageWriterBenchmarks
{
    private static readonly FixApplicationSessionHeader Header = new(
        "B3UMDFC",
        "CLIENT01",
        1,
        new DateTimeOffset(2026, 8, 25, 13, 30, 0, TimeSpan.Zero));

    private static readonly FixMarketDataInstrument Instrument = new(
        "PETR4",
        1234,
        priceScale: 2,
        deliverToCompId: "LUX00");

    private static readonly FixMarketDataSnapshotRequest SnapshotRequest = new("bench-snap", Instrument);

    [Params(1, 10, 50)]
    public int EntryCount;

    private FixApplicationMessageWriter _writer = null!;
    private FixMarketDataIncrementalEntry[] _incrementalEntries = null!;
    private OrderBook _book = null!;

    [GlobalSetup]
    public void Setup()
    {
        _writer = new FixApplicationMessageWriter();

        _incrementalEntries = new FixMarketDataIncrementalEntry[EntryCount];
        for (int i = 0; i < EntryCount; i++)
        {
            _incrementalEntries[i] = new FixMarketDataIncrementalEntry(
                (i & 1) == 0 ? FixMdUpdateAction.New : FixMdUpdateAction.Change,
                FixMdEntryType.Bid,
                Header.SendingTime,
                FixMarketDataEntryFields.Price | FixMarketDataEntryFields.Size | FixMarketDataEntryFields.OrderId,
                Price: 2800 + i,
                Size: 100 + i,
                OrderId: (ulong)(500 + i));
        }

        _book = new OrderBook(Instrument.SecurityId);
        for (int i = 0; i < EntryCount; i++)
        {
            var entry = new OrderBookEntry
            {
                OrderId = (ulong)(500 + i),
                Side = (i & 1) == 0 ? BookSideType.Bid : BookSideType.Ask,
                SecurityId = _book.SecurityId,
                Price = 2800 + i,
                Quantity = 100 + i,
            };
            _book.GetSide(entry.Side).Add(in entry);
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _writer.Dispose();

    [Benchmark(Description = "WriteIncrementalRefresh: N MD entries")]
    public int WriteIncrementalRefresh()
        => _writer.WriteIncrementalRefresh(Header, Instrument, _incrementalEntries).Length;

    [Benchmark(Description = "WriteSnapshotFullRefresh: N book levels")]
    public int WriteSnapshotFullRefresh()
        => _writer.WriteSnapshotFullRefresh(Header, SnapshotRequest, _book).Length;
}

// Full conflation + encode hot path: book delta events flow through the
// publisher's lock-free queues, get conflated per (SecurityId, Side) bucket,
// and are flushed (encoded + handed to the sink) in one pass. Exercises the
// same code path as production's periodic FlushIfDue() worker loop, but
// with StartBackgroundWorker=false so the benchmark controls flush timing.
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class FixConflatedMarketDataPublisherBenchmarks
{
    private sealed class NullSink : IFixApplicationMessageSink
    {
        public static readonly NullSink Instance = new();
        public void OnMessage(ReadOnlyMemory<byte> message) { }
    }

    private sealed class SequentialHeaderProvider : IFixApplicationHeaderProvider
    {
        private int _seqNum;

        public FixApplicationSessionHeader NextHeader(DateTimeOffset sendingTime)
            => new("B3UMDFC", "CLIENT01", Interlocked.Increment(ref _seqNum), sendingTime);
    }

    private sealed class StaticInstrumentResolver : IFixMarketDataInstrumentResolver
    {
        private readonly Dictionary<ulong, FixMarketDataInstrument> _instruments;

        public StaticInstrumentResolver(Dictionary<ulong, FixMarketDataInstrument> instruments)
            => _instruments = instruments;

        public bool TryResolve(ulong securityId, out FixMarketDataInstrument instrument)
            => _instruments.TryGetValue(securityId, out instrument);
    }

    [Params(64, 512)]
    public int SymbolCount;

    [Params(4)]
    public int UpdatesPerSymbol;

    private FixConflatedMarketDataPublisher _publisher = null!;
    private OrderBook[] _books = null!;

    [GlobalSetup]
    public void Setup()
    {
        var instruments = new Dictionary<ulong, FixMarketDataInstrument>();
        _books = new OrderBook[SymbolCount];
        for (int s = 0; s < SymbolCount; s++)
        {
            ulong securityId = (ulong)(1000 + s);
            instruments[securityId] = new FixMarketDataInstrument($"SYM{s}", securityId, priceScale: 2);
            _books[s] = new OrderBook(securityId);
        }

        _publisher = new FixConflatedMarketDataPublisher(
            NullSink.Instance,
            new SequentialHeaderProvider(),
            new StaticInstrumentResolver(instruments),
            new FixConflatedMarketDataOptions { StartBackgroundWorker = false });
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Re-populate the pending-delta queues before each measured
        // invocation; FlushNow() drains them, so state doesn't roll over.
        for (int s = 0; s < SymbolCount; s++)
        {
            OrderBook book = _books[s];
            for (int u = 0; u < UpdatesPerSymbol; u++)
            {
                var entry = new OrderBookEntry
                {
                    OrderId = (ulong)(1 + u),
                    Side = BookSideType.Bid,
                    SecurityId = book.SecurityId,
                    Price = 2800 + u,
                    Quantity = 100 + u,
                };
                _publisher.OnOrderUpdated(book, in entry);
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _publisher.Dispose();

    [Benchmark(Description = "Conflate + encode: SymbolCount x UpdatesPerSymbol book deltas")]
    public void FlushNow() => _publisher.FlushNow();
}
