using System.Net;
using System.Net.Sockets;
using System.Text;
using SenderPro.Core;

var tests = new (string Name, Func<Task> Run)[]
{
    ("CSV round trip preserves Arabic, quotes, commas and multiline fields", CsvRoundTrip),
    ("CSV rejects broken headers, rows, booleans and quotes", CsvRejects),
    ("Duplicate import preserves unsubscribe state", ImportDuplicates),
    ("Data persists and interrupted campaigns recover without automatic resend", Persistence),
    ("Corrupt data is rejected and remains intact", CorruptData),
    ("Campaign sends personalized mail once and skips duplicates and opt-outs", CampaignFiltering),
    ("Preview never invokes transport", Preview),
    ("Opt-out on a duplicate address excludes every copy", DuplicateOptOut),
    ("Recipient failure is reported and later recipients still execute", FailedRecipient),
    ("Cancellation stops pending recipients and replay is prohibited", Cancellation),
    ("SMTP transport completes real protocol exchange with local test server", SmtpIntegration)
};
var failed = 0;
foreach (var (name, run) in tests)
{
    try { await run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failed++; Console.Error.WriteLine("FAIL " + name + "\n" + ex); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} checks passed");
return failed == 0 ? 0 : 1;

static void Require(bool value, string detail)
{ if (!value) throw new Exception(detail); }

static Task CsvRoundTrip()
{
    var c = new Contact { Name = "زياد، \"اختبار\"\nسطر تاني", Email = "person@example.com", Phone = "+201234567890", Group = "a,b", Unsubscribed = true };
    var result = ContactCsv.Read("\uFEFF" + ContactCsv.Write([c])).Single();
    Require(result.Name == c.Name && result.Email == c.Email && result.Phone == c.Phone && result.Group == c.Group && result.Unsubscribed, "Round trip changed data");
    Require(ContactCsv.Read("name,email,phone,group,unsubscribed\n").Count == 0, "Header-only CSV must be empty");
    return Task.CompletedTask;
}

static Task CsvRejects()
{
    foreach (var text in new[] { "name,email\nx,y", "name,email,phone,group,unsubscribed\nx,y", "name,email,phone,group,unsubscribed\nx,y,z,g,maybe", "name,email,phone,group,unsubscribed\n\"unclosed", "name,email,phone,group,unsubscribed\n\"x\"oops,y,z,g,false" })
    {
        var rejected = false;
        try { ContactCsv.Read(text); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Malformed CSV was accepted");
    }
    return Task.CompletedTask;
}

static Task ImportDuplicates()
{
    List<Contact> existing = [new() { Email = "ONE@example.com", Unsubscribed = true }];
    Require(ContactCsv.Merge(existing, [new() { Email = "one@example.com" }, new() { Email = "two@example.com" }]) == 1, "Incorrect merge count");
    Require(existing.Count == 2 && existing[0].Unsubscribed, "Opt-out lost");
    ContactCsv.Merge(existing, [new() { Email = "two@example.com", Unsubscribed = true }]);
    Require(existing[1].Unsubscribed, "Imported opt-out lost");
    return Task.CompletedTask;
}

static Task Persistence()
{
    var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    try
    {
        var store = new DataStore(Path.Combine(folder, "data.json"));
        var data = store.Load(); data.Contacts.Add(new() { Name = "زياد", Email = "one@example.com" });
        data.Campaigns.Add(new() { State = "running", Results = [new() { Recipient = "one@example.com", Status = "accepted" }] });
        store.Save(data); store.Save(data);
        var loaded = store.Load();
        Require(loaded.Contacts.Single().Name == "زياد", "Contact not persisted");
        Require(loaded.Campaigns.Single().State == "interrupted" && loaded.Campaigns.Single().Results.Count == 1, "Interrupted campaign incorrectly recovered");
        Require(File.Exists(store.FilePath + ".bak"), "Backup missing");
        Require(!File.ReadAllText(store.FilePath).Contains("password", StringComparison.OrdinalIgnoreCase), "Password field must not be persisted");
    }
    finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    return Task.CompletedTask;
}

static Task CorruptData()
{
    var path = Path.GetTempFileName();
    try
    {
        File.WriteAllText(path, "broken");
        var rejected = false;
        try { new DataStore(path).Load(); } catch (System.Text.Json.JsonException) { rejected = true; }
        Require(rejected && File.ReadAllText(path) == "broken", "Corrupt data overwritten or silently accepted");
    }
    finally { File.Delete(path); }
    return Task.CompletedTask;
}

static async Task CampaignFiltering()
{
    var transport = new RecordingTransport(); var campaign = new CampaignRecord { Subject = "Hello {name}", Body = "{email}/{group}" };
    await new CampaignRunner(transport).RunAsync(campaign,
        [new() { Name = "Ziad", Email = "one@example.com", Group = "Friends" }, new() { Email = "ONE@example.com" },
         new() { Email = "no@example.com", Unsubscribed = true }, new() { Email = "invalid" }], false, 0, _ => { }, default);
    Require(transport.Sent.Single() == ("one@example.com", "Hello Ziad", "one@example.com/Friends"), "Personalization or deduplication failed");
    Require(campaign.State == "completed" && campaign.Results.Count(r => r.Status == "skipped") == 3 && campaign.Results.Count(r => r.Status == "accepted") == 1, "Incorrect result classification");
}

static async Task Preview()
{
    var transport = new RecordingTransport(); var campaign = new CampaignRecord();
    await new CampaignRunner(transport).RunAsync(campaign, [new() { Email = "one@example.com" }], true, 0, _ => { }, default);
    Require(transport.Sent.Count == 0 && campaign.Results.Single().Status == "preview", "Preview sent mail");
}

static async Task FailedRecipient()
{
    var transport = new RecordingTransport { FailFirst = true }; var campaign = new CampaignRecord();
    await new CampaignRunner(transport).RunAsync(campaign, [new() { Email = "one@example.com" }, new() { Email = "two@example.com" }], false, 0, _ => { }, default);
    Require(campaign.Results[0].Status == "failed" && campaign.Results[1].Status == "accepted", "Failure handling lost later recipients");
}

static async Task DuplicateOptOut()
{
    var transport = new RecordingTransport(); var campaign = new CampaignRecord();
    await new CampaignRunner(transport).RunAsync(campaign,
        [new() { Email = "one@example.com" }, new() { Email = "ONE@example.com", Unsubscribed = true }],
        false, 0, _ => { }, default);
    Require(transport.Sent.Count == 0 && campaign.Results.All(r => r.Status == "skipped"), "Duplicate email bypassed opt-out");
}

static async Task Cancellation()
{
    using var cancel = new CancellationTokenSource();
    var transport = new RecordingTransport(); var campaign = new CampaignRecord(); var runner = new CampaignRunner(transport);
    await runner.RunAsync(campaign, [new() { Email = "one@example.com" }, new() { Email = "two@example.com" }], false, 0, _ => cancel.Cancel(), cancel.Token);
    Require(campaign.State == "cancelled" && transport.Sent.Count == 1, "Cancellation sent pending mail");
    var rejected = false;
    try { await runner.RunAsync(campaign, [], false, 0, _ => { }, default); } catch (InvalidOperationException) { rejected = true; }
    Require(rejected, "Replayed existing campaign");
}

static async Task SmtpIntegration()
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var received = new StringBuilder();
    var server = Task.Run(async () =>
    {
        using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
        using var stream = connection.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 localhost test SMTP");
        var inData = false;
        while (await reader.ReadLineAsync(timeout.Token) is { } line)
        {
            if (inData)
            {
                if (line == ".") { inData = false; await writer.WriteLineAsync("250 queued"); }
                else received.AppendLine(line);
            }
            else if (line.StartsWith("EHLO") || line.StartsWith("HELO")) await writer.WriteLineAsync("250 localhost");
            else if (line.StartsWith("MAIL FROM") || line.StartsWith("RCPT TO")) await writer.WriteLineAsync("250 OK");
            else if (line == "DATA") { inData = true; await writer.WriteLineAsync("354 send content"); }
            else if (line == "QUIT") { await writer.WriteLineAsync("221 bye"); break; }
            else await writer.WriteLineAsync("250 OK");
        }
    }, timeout.Token);
    try
    {
        // Plaintext is used only for this loopback fixture. Production defaults to STARTTLS.
        var transport = new SmtpTransport(new() { Host = "127.0.0.1", Port = port, EnableTls = false, FromAddress = "sender@example.com" }, "");
        await transport.SendAsync("receiver@example.com", "Integration subject", "Integration body", timeout.Token);
        await server.WaitAsync(timeout.Token);
        Require(received.ToString().Contains("Integration subject") && received.ToString().Contains("Integration body") && received.ToString().Contains("receiver@example.com"), "SMTP payload was not transmitted");
    }
    finally { listener.Stop(); }
}

sealed class RecordingTransport : IEmailTransport
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];
    public bool FailFirst { get; init; }
    private int calls;
    public Task SendAsync(string recipient, string subject, string body, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (FailFirst && calls++ == 0) throw new System.Net.Mail.SmtpException("Fixture rejection");
        Sent.Add((recipient, subject, body)); return Task.CompletedTask;
    }
}
