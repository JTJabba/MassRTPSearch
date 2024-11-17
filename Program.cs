using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;

namespace MassRTPSearch;

class Program
{
    private static DatabaseContext _db = null!;

    private static readonly Channel<TimeSpan> _tokenBucket = Channel.CreateBounded<TimeSpan>(
        new BoundedChannelOptions(Config.MaxRequestsPerMin)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    static async Task Main(string[] args)
    {
        ConfigLoader.Load();

        // Setup database
        var dbOptions = new DbContextOptionsBuilder<DatabaseContext>()
            .UseSqlite("Data Source=rtp_cache.db")
            .Options;
        _db = new DatabaseContext(dbOptions);
        await _db.Database.EnsureCreatedAsync();

        if (args.Length != 1)
        {
            Console.WriteLine("Usage: MassRTPSearch <input-file>");
            Console.WriteLine("Make sure to set the PERPLEXITY_API_KEY environment variable");
            return;
        }

        var apiKey = Environment.GetEnvironmentVariable("PERPLEXITY_API_KEY");
        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("Error: PERPLEXITY_API_KEY environment variable is not set");
            return;
        }

        // Start token bucket filler
        _ = FillTokenBucket();

        var inputFile = args[0];
        var client = new PerplexityClient(apiKey);
        var results = new List<GameRtp>();
        var semaphore = new SemaphoreSlim(Config.MaxConcurrentRequests);
        var tasks = new List<Task>();

        var games = File.ReadAllLines(inputFile)
            .Where(game => !string.IsNullOrWhiteSpace(game))
            .ToList();

        foreach (var game in games)
        {
            tasks.Add(ProcessGame(game, client, semaphore, results));
        }

        await Task.WhenAll(tasks);

        var sortedResults = results.OrderByDescending(r => r.MinRtp);
        await File.WriteAllLinesAsync("rtp_results.csv", 
            new[] { "Game,Min RTP %,Max RTP %" }
            .Concat(sortedResults.Select(r => 
                $"{r.Name},{r.MinRtp:F2},{r.MaxRtp:F2}")));

        Console.WriteLine("Results written to rtp_results.csv");
    }

    static async Task ProcessGame(string game, PerplexityClient client, SemaphoreSlim semaphore, List<GameRtp> results)
    {
        try
        {
            await semaphore.WaitAsync();
            
            // Check cache before waiting for rate limit token
            var cached = await _db.SearchResults
                .FirstOrDefaultAsync(r => 
                    r.GameTitle == game && 
                    r.PerplexityModel == Config.Model);

            if (cached != null)
            {
                Console.WriteLine($"Cache hit for {game}");
                lock (results)
                {
                    results.Add(new GameRtp(game, cached.ReportedMinRtp, cached.ReportedMaxRtp));
                }
                return;
            }
            
            // Only wait for token if we need to make an API call
            await _tokenBucket.Reader.ReadAsync();
            
            Console.WriteLine($"Processing: {game}");
            
            try
            {
                var rtp = await GetGameRtp(client, game);
                lock (results)
                {
                    results.Add(new GameRtp(game, rtp.MinRtp, rtp.MaxRtp));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing {game}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex}");
                lock (results)
                {
                    results.Add(new GameRtp(game, 0, 0));
                }
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    static async Task FillTokenBucket()
    {
        var writer = _tokenBucket.Writer;
        var interval = TimeSpan.FromMinutes(1.0) / Config.MaxRequestsPerMin;
        
        while (true)
        {
            await writer.WriteAsync(interval);
            await Task.Delay(interval);
        }
    }

    static async Task<(decimal MinRtp, decimal MaxRtp)> GetGameRtp(PerplexityClient client, string game)
    {
        // Check cache first
        var cached = await _db.SearchResults
            .FirstOrDefaultAsync(r => 
                r.GameTitle == game && 
                r.PerplexityModel == Config.Model);

        if (cached != null)
        {
            Console.WriteLine($"Cache hit for {game}");
            return (cached.ReportedMinRtp, cached.ReportedMaxRtp);
        }

        var messages = new List<Message>
        {
            new Message("system", "You are a casino game expert. Respond with the RTP (Return to Player) " +
                "percentage (100 = 100%) for the specified game in this exact format: 'RTP: <min>-<max>' or 'RTP: <fixed>' " +
                "if there's only one value. Use the minimum RTP if multiple configurations exist. " +
                "Make sure the response contains the RTP line unless you cannot find it. " +
                "Restate it at the end of your response in the EXACT format: 'RTP: <min>-<max>' or 'RTP: <fixed>'"),
            new Message("user", $"What is the RTP of {game}?")
        };

        var request = new PerplexityRequest(
            Model: Config.Model,
            Messages: messages,
            Temperature: 0
        );

        try
        {
            var response = await client.CompleteAsync(request);
            var rtpResponse = response.Choices[0].Message.Content;

            Console.WriteLine($"API Response for {game}: {rtpResponse}");

            // Find the last RTP line using regex
            var matches = System.Text.RegularExpressions.Regex.Matches(rtpResponse, @"RTP:\s*(\d+\.?\d*)-?(\d+\.?\d*)?");
            if (matches.Count == 0)
                return (0, 0);

            // Use the last match
            var lastMatch = matches[^1];
            
            // If second group is empty, it's a fixed RTP
            var minRtp = decimal.Parse(lastMatch.Groups[1].Value);
            var maxRtp = lastMatch.Groups[2].Success 
                ? decimal.Parse(lastMatch.Groups[2].Value) 
                : minRtp;

            // Save to cache
            await _db.SearchResults.AddAsync(new SearchResultsModel
            {
                GameTitle = game,
                SearchDate = DateTime.UtcNow,
                PerplexityModel = Config.Model,
                ReportedMinRtp = minRtp,
                ReportedMaxRtp = maxRtp
            });
            await _db.SaveChangesAsync();

            return (minRtp, maxRtp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing {game}: {ex}");
            return (0, 0);
        }
    }
}

public record GameRtp(string Name, decimal MinRtp, decimal MaxRtp);
