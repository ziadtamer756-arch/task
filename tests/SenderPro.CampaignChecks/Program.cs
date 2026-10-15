using System.Net;
using System.Text.Json;
using SenderPro.Core;

var checks = new (string Name, Func<Task> Run)[]
{
    ("Original five-column CSV remains readable", LegacyCsv),
    ("CSV preserves Telegram private/group IDs as text", TelegramCsvRoundTrip),
    ("XLSX preserves Telegram IDs and old five-column workbooks", TelegramWorkbookRoundTrip),
    ("Import fills missing channel IDs without clearing them or opt-outs", MergeChannelDetails),
    ("Old saved data receives default messaging configuration", LegacyData),
    ("SMTP persists running before its first network request", SmtpStartsBeforeSending),
    ("Social campaigns persist running before their first network request", SocialStartsBeforeSending),
    ("A failed start checkpoint prevents SMTP and social sends", FailedStartPreventsSending),
    ("A failed progress checkpoint stops further recipients", FailedProgressStopsSending),
    ("Social preview invokes neither send nor connection checks", SocialPreview),
    ("Channel recipients are normalized and validated", RecipientValidation),
    ("SMTP honors an opt-out outside the selected group", SmtpGlobalOptOut),
    ("Both social channels honor opt-outs outside the selected group", SocialGlobalOptOut),
    ("Social campaigns exclude duplicate, invalid and opted-out targets", SocialFiltering),
    ("Telegram renders body variables for each contact", TelegramPersonalization),
    ("WhatsApp template parameters are rendered separately for each contact", WhatsAppTemplatePersonalization),
    ("WhatsApp service-window text is an explicit separate mode", WhatsAppServiceWindow),
    ("Provider acceptance records its message ID and timestamp", ProviderReceipt),
    ("A typed recipient rejection is recorded and later recipients continue", TypedFailure),
    ("Authentication and rate limits pause pending recipients without retry", GlobalProviderFailuresPause),
    ("Unexpected network failures cannot expose a Telegram token", UnexpectedFailureRedaction),
    ("Typed network and invalid-response failures remain unconfirmed", TypedUncertainFailure),
    ("In-flight cancellation retains the attempted SMTP recipient", SmtpInFlightCancellation),
    ("In-flight cancellation retains attempted social recipients", SocialInFlightCancellation),
    ("Social cancellation stops pending recipients and prohibits replay", SocialCancellation),
    ("SMTP cancellation stops pending recipients and prohibits replay", SmtpCancellation)
};

var failures = 0;
foreach (var (name, run) in checks)
{
    try { await run(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + "\n" + ex); }
}
Console.WriteLine($"{checks.Length - failures}/{checks.Length} campaign checks passed");
return failures == 0 ? 0 : 1;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string TemporaryFolder() => Path.Combine(Path.GetTempPath(), "SenderPro-campaign-" + Guid.NewGuid());

static Task LegacyCsv()
{
    var csv = "name,email,phone,group,unsubscribed\r\nزياد,ziad@example.com,+201001234567,العملاء,true\r\n";
    var contact = ContactCsv.Read(csv).Single();
    Require(contact.Name == "زياد" && contact.TelegramChatId == "" && contact.Unsubscribed,
        "An original CSV must load without a Telegram column or loss of opt-out");
    return Task.CompletedTask;
}

static Task TelegramCsvRoundTrip()
{
    Contact[] contacts = [new() { Name = "زياد، \"تجربة\"\nسطر", Email = "ziad@example.com",
        Phone = "+201001234567", TelegramChatId = "1234567890123456", Group = "العملاء", Unsubscribed = true },
        new() { Name = "مجموعة", TelegramChatId = "-1001234567890" }];
    var csv = ContactCsv.Write(contacts);
    Require(csv.Split('\n')[0].Contains("telegram_chat_id"), "Export omits the channel ID column");
    var loaded = ContactCsv.Read("\uFEFF" + csv);
    Require(loaded[0].TelegramChatId == contacts[0].TelegramChatId && loaded[1].TelegramChatId == contacts[1].TelegramChatId,
        "Telegram IDs changed during CSV round trip");
    Require(loaded[0].Name == contacts[0].Name && loaded[0].Phone == contacts[0].Phone && loaded[0].Unsubscribed,
        "New channel export changed original contact data");
    return Task.CompletedTask;
}

static Task TelegramWorkbookRoundTrip()
{
    var folder = TemporaryFolder(); Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "contacts.xlsx");
    try
    {
        ContactWorkbook.Write(path, [new() { Name = "زياد", Email = "ziad@example.com", Phone = "+201001234567",
            TelegramChatId = "-1001234567890", Unsubscribed = true }]);
        var loaded = ContactWorkbook.Read(path).Single();
        Require(loaded.TelegramChatId == "-1001234567890" && loaded.Phone == "+201001234567" && loaded.Unsubscribed,
            "XLSX changed text IDs, phone or opt-out");
        using (var workbook = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(path, true))
        {
            Require(!new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(workbook).Any(), "Export is invalid OpenXML");
            var worksheet = workbook.WorkbookPart!.WorksheetParts.Single().Worksheet;
            var headerRow = worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>().First();
            var header = headerRow.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>()
                .Single(c => c.InnerText == "telegram_chat_id");
            var column = new string(header.CellReference!.Value!.TakeWhile(char.IsLetter).ToArray());
            foreach (var cell in worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>()
                         .Where(c => new string(c.CellReference!.Value!.TakeWhile(char.IsLetter).ToArray()) == column).ToList())
                cell.Remove();
            worksheet.Save();
        }
        loaded = ContactWorkbook.Read(path).Single();
        Require(loaded.TelegramChatId == "" && loaded.Email == "ziad@example.com" && loaded.Unsubscribed,
            "An original five-column workbook no longer imports");
    }
    finally { Directory.Delete(folder, true); }
    return Task.CompletedTask;
}

static Task MergeChannelDetails()
{
    var existing = new List<Contact> { new() { Email = "ZIAD@example.com", Phone = "+201001234567", Unsubscribed = true } };
    var added = ContactCsv.Merge(existing, [new() { Email = "ziad@example.com", TelegramChatId = "123456789" }]);
    Require(added == 0 && existing.Count == 1 && existing[0].TelegramChatId == "123456789" && existing[0].Unsubscribed,
        "Merging an existing email must fill its missing Telegram ID and preserve opt-out");
    ContactCsv.Merge(existing, [new() { Email = "ziad@example.com", TelegramChatId = "" }]);
    Require(existing[0].TelegramChatId == "123456789" && existing[0].Unsubscribed,
        "An older import cleared the channel ID or opt-out");
    return Task.CompletedTask;
}

static Task LegacyData()
{
    var folder = TemporaryFolder(); Directory.CreateDirectory(folder);
    var path = Path.Combine(folder, "data.json");
    try
    {
        File.WriteAllText(path, """
            {"Contacts":[{"Name":"زياد","Email":"ziad@example.com","Unsubscribed":true}],
            "Templates":[],"Campaigns":[{"Name":"Old campaign","State":"running","Results":[]}],"Smtp":{}}
            """);
        var data = new DataStore(path).Load();
        Require(data.Contacts.Single().TelegramChatId == "" && data.Contacts.Single().Unsubscribed,
            "Old data did not receive a safe empty Telegram ID");
        Require(data.Campaigns.Single().Channel == "smtp" && data.Campaigns.Single().State == "interrupted",
            "Old campaigns lost their SMTP default or interrupted state");
        Require(data.Messaging is not null && !string.IsNullOrWhiteSpace(data.Messaging.WhatsAppApiVersion),
            "Old data has no usable messaging defaults");
    }
    finally { Directory.Delete(folder, true); }
    return Task.CompletedTask;
}

static async Task SmtpStartsBeforeSending()
{
    var folder = TemporaryFolder();
    try
    {
        var record = new CampaignRecord(); var data = new AppData { Campaigns = [record] };
        var store = new DataStore(Path.Combine(folder, "data.json")); var checkpoints = 0;
        var transport = new RecordingEmailTransport { OnSend = () => RequireRunningFile(store.FilePath, "smtp") };
        await new CampaignRunner(transport).RunAsync(record, [new() { Email = "ziad@example.com" }], false, 0,
            _ => { }, default, onStarted: () => { checkpoints++; store.Save(data); });
        Require(checkpoints == 1 && transport.Sent.Count == 1 && record.State == "completed",
            "SMTP did not checkpoint exactly once before sending");
    }
    finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
}

static async Task SocialStartsBeforeSending()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        var folder = TemporaryFolder();
        try
        {
            var record = CampaignFor(kind); var data = new AppData { Campaigns = [record] };
            var store = new DataStore(Path.Combine(folder, "data.json")); var checkpoints = 0;
            var transport = new RecordingSocialTransport(kind) { OnSend = _ => RequireRunningFile(store.FilePath,
                kind == ChannelKind.Telegram ? "telegram" : "whatsapp") };
            await new SocialCampaignRunner(transport, kind).RunAsync(record, [ContactFor(kind)], false, 0,
                _ => { }, default, onStarted: () => { checkpoints++; store.Save(data); });
            Require(checkpoints == 1 && transport.Sent.Count == 1 && record.State == "completed",
                $"{kind} did not checkpoint exactly once before sending");
            Require(store.Load().Campaigns.Single().State == "interrupted",
                "A process restart must treat a checkpointed but unfinished campaign as interrupted");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}

static void RequireRunningFile(string path, string channel)
{
    Require(File.Exists(path), "First request began before its checkpoint was written");
    using var document = JsonDocument.Parse(File.ReadAllText(path));
    var saved = document.RootElement.GetProperty("Campaigns")[0];
    Require(saved.GetProperty("State").GetString() == "running" && saved.GetProperty("Channel").GetString() == channel,
        "The first request began while persisted state/channel was incorrect");
}

static async Task FailedStartPreventsSending()
{
    var smtp = new RecordingEmailTransport(); var smtpRecord = new CampaignRecord();
    var thrown = false;
    try
    {
        await new CampaignRunner(smtp).RunAsync(smtpRecord, [new() { Email = "ziad@example.com" }], false, 0,
            _ => { }, default, onStarted: () => throw new IOException("Fixture checkpoint unavailable"));
    }
    catch (IOException) { thrown = true; }
    Require(thrown && smtp.Sent.Count == 0 && smtpRecord.State == "failed", "SMTP sent despite an unsaved start checkpoint");
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        var transport = new RecordingSocialTransport(kind); var record = CampaignFor(kind); thrown = false;
        try
        {
            await new SocialCampaignRunner(transport, kind).RunAsync(record, [ContactFor(kind)], false, 0,
                _ => { }, default, onStarted: () => throw new IOException("Fixture checkpoint unavailable"));
        }
        catch (IOException) { thrown = true; }
        Require(thrown && transport.Sent.Count == 0 && record.State == "failed", $"{kind} sent despite an unsaved start checkpoint");
    }
}

static async Task SocialPreview()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        var transport = new RecordingSocialTransport(kind); var record = CampaignFor(kind);
        await new SocialCampaignRunner(transport, kind).RunAsync(record, [ContactFor(kind)], true, 0, _ => { }, default);
        Require(transport.Sent.Count == 0 && transport.ConnectionChecks == 0, $"{kind} preview performed a network operation");
        Require(record.Results.Single().Status == "preview" && record.State == "completed" && record.Mode == "preview",
            $"{kind} preview has incorrect report state");
    }
}

static async Task FailedProgressStopsSending()
{
    var smtp = new RecordingEmailTransport(); var smtpRecord = new CampaignRecord(); var thrown = false;
    try
    {
        await new CampaignRunner(smtp).RunAsync(smtpRecord,
            [new() { Email = "one@example.com" }, new() { Email = "two@example.com" }], false, 0,
            _ => throw new IOException("Fixture progress checkpoint unavailable"), default);
    }
    catch (IOException) { thrown = true; }
    Require(thrown && smtp.Sent.Count == 1 && smtpRecord.State == "failed" && smtpRecord.Results.Count == 1,
        "SMTP continued despite being unable to save its first accepted result");
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        var transport = new RecordingSocialTransport(kind); var record = CampaignFor(kind); thrown = false;
        try
        {
            await new SocialCampaignRunner(transport, kind).RunAsync(record,
                [ContactFor(kind), ContactFor(kind, 1)], false, 0,
                _ => throw new IOException("Fixture progress checkpoint unavailable"), default);
        }
        catch (IOException) { thrown = true; }
        Require(thrown && transport.Sent.Count == 1 && record.State == "failed" && record.Results.Count == 1,
            $"{kind} continued despite being unable to save its first accepted result");
    }
}

static Task RecipientValidation()
{
    Require(SocialCampaignRunner.RecipientFor(new() { TelegramChatId = " -1001234567890 " }, ChannelKind.Telegram) == "-1001234567890",
        "Telegram target was not trimmed");
    Require(SocialCampaignRunner.RecipientFor(new() { Phone = "+20 (100) 123-4567" }, ChannelKind.WhatsApp) == "201001234567",
        "WhatsApp target was not normalized to international digits");
    Require(SocialCampaignRunner.IsValidRecipient("123456789", ChannelKind.Telegram) &&
        SocialCampaignRunner.IsValidRecipient("-1001234567890", ChannelKind.Telegram), "Valid Telegram IDs rejected");
    Require(SocialCampaignRunner.IsValidRecipient("201001234567", ChannelKind.WhatsApp), "Valid WhatsApp number rejected");
    foreach (var kind in Enum.GetValues<ChannelKind>())
        foreach (var bad in new[] { "", " ", "https://example.com", "123\r\nAuthorization: secret" })
            Require(!SocialCampaignRunner.IsValidRecipient(bad, kind), $"Unsafe {kind} target accepted");
    return Task.CompletedTask;
}

static async Task SmtpGlobalOptOut()
{
    var transport = new RecordingEmailTransport(); var record = new CampaignRecord();
    await new CampaignRunner(transport).RunAsync(record, [new() { Email = "ziad@example.com", Group = "selected" }], false, 0,
        _ => { }, default, globalBlockedRecipients: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ZIAD@example.com" });
    Require(transport.Sent.Count == 0 && record.Results.Single().Status == "skipped", "Global SMTP opt-out was bypassed by group selection");
}

static async Task SocialGlobalOptOut()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        var transport = new RecordingSocialTransport(kind); var record = CampaignFor(kind); var contact = ContactFor(kind);
        var blocked = new HashSet<string> { SocialCampaignRunner.RecipientFor(contact, kind) };
        await new SocialCampaignRunner(transport, kind).RunAsync(record, [contact], false, 0, _ => { }, default,
            globalBlockedRecipients: blocked);
        Require(transport.Sent.Count == 0 && record.Results.Single().Status == "skipped", $"Global {kind} opt-out was bypassed");
    }
}

static async Task SocialFiltering()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        var transport = new RecordingSocialTransport(kind); var record = CampaignFor(kind);
        var optedOut = ContactFor(kind, 1); optedOut.Unsubscribed = true;
        await new SocialCampaignRunner(transport, kind).RunAsync(record,
            [ContactFor(kind), ContactFor(kind), ContactFor(kind, 1), optedOut, new() { Name = "No target" }],
            false, 0, _ => { }, default);
        Require(transport.Sent.Count == 1 && record.Results.Count(r => r.Status == "skipped") == 4 && record.State == "completed",
            $"{kind} filtering sent a duplicate, invalid or opted-out target");
    }
}

static async Task TelegramPersonalization()
{
    var transport = new RecordingSocialTransport(ChannelKind.Telegram);
    var record = CampaignFor(ChannelKind.Telegram); record.Body = "مرحبًا {name}|{email}|{phone}|{group}|{telegram_chat_id}";
    var one = ContactFor(ChannelKind.Telegram); var two = ContactFor(ChannelKind.Telegram, 1);
    await new SocialCampaignRunner(transport, ChannelKind.Telegram).RunAsync(record, [one, two], false, 0, _ => { }, default);
    Require(transport.Sent[0].Body == $"مرحبًا {one.Name}|{one.Email}|{one.Phone}|{one.Group}|{one.TelegramChatId}" &&
        transport.Sent[1].Body == $"مرحبًا {two.Name}|{two.Email}|{two.Phone}|{two.Group}|{two.TelegramChatId}",
        "Telegram body was not independently personalized");
}

static async Task WhatsAppTemplatePersonalization()
{
    var transport = new RecordingSocialTransport(ChannelKind.WhatsApp); var record = CampaignFor(ChannelKind.WhatsApp);
    record.WhatsAppTemplate = new() { Name = "welcome", LanguageCode = "ar", BodyParameters = ["{name}", "{group}", "{phone}"] };
    var one = ContactFor(ChannelKind.WhatsApp); var two = ContactFor(ChannelKind.WhatsApp, 1);
    await new SocialCampaignRunner(transport, ChannelKind.WhatsApp).RunAsync(record, [one, two], false, 0, _ => { }, default);
    Require(transport.Sent.All(m => m.WhatsAppMode == WhatsAppMessageMode.Template), "Template campaign changed mode");
    Require(transport.Sent[0].WhatsAppTemplate!.BodyParameters.SequenceEqual([one.Name, one.Group, one.Phone]) &&
        transport.Sent[1].WhatsAppTemplate!.BodyParameters.SequenceEqual([two.Name, two.Group, two.Phone]),
        "WhatsApp template parameters reused another contact's values");
    Require(transport.Sent.All(m => m.WhatsAppTemplate!.Name == "welcome" && m.WhatsAppTemplate.LanguageCode == "ar"),
        "Template metadata changed");
    Require(record.WhatsAppTemplate.BodyParameters.SequenceEqual(["{name}", "{group}", "{phone}"]),
        "Rendering changed the persisted template source");
}

static async Task WhatsAppServiceWindow()
{
    var transport = new RecordingSocialTransport(ChannelKind.WhatsApp); var record = CampaignFor(ChannelKind.WhatsApp);
    record.WhatsAppMode = WhatsAppMessageMode.ServiceWindowText; record.WhatsAppTemplate = null; record.Body = "أهلًا {name}";
    var contact = ContactFor(ChannelKind.WhatsApp);
    await new SocialCampaignRunner(transport, ChannelKind.WhatsApp).RunAsync(record, [contact], false, 0, _ => { }, default);
    Require(transport.Sent.Single().WhatsAppMode == WhatsAppMessageMode.ServiceWindowText &&
        transport.Sent.Single().Body == "أهلًا " + contact.Name, "Explicit service-window text was not preserved");
}

static async Task ProviderReceipt()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        var transport = new RecordingSocialTransport(kind); var record = CampaignFor(kind);
        await new SocialCampaignRunner(transport, kind).RunAsync(record, [ContactFor(kind)], false, 0, _ => { }, default);
        var result = record.Results.Single();
        Require(result.Status == "accepted" && result.ProviderMessageId == "fixture-1" && result.At == RecordingSocialTransport.AcceptedAt,
            $"{kind} provider acceptance details were not recorded");
    }
}

static async Task TypedFailure()
{
    var transport = new RecordingSocialTransport(ChannelKind.Telegram)
    { FailFirst = new ChannelTransportException(ChannelErrorKind.Rejected, "Fixture recipient rejected", HttpStatusCode.Forbidden, 403) };
    var record = CampaignFor(ChannelKind.Telegram);
    await new SocialCampaignRunner(transport, ChannelKind.Telegram).RunAsync(record,
        [ContactFor(ChannelKind.Telegram), ContactFor(ChannelKind.Telegram, 1)], false, 0, _ => { }, default);
    Require(record.Results.Count == 2 && record.Results[0].Status == "failed" && record.Results[1].Status == "accepted" && record.State == "completed",
        "A typed recipient rejection prevented a later recipient from executing");
}

static async Task UnexpectedFailureRedaction()
{
    const string secret = "fixture-secret-that-must-never-appear";
    var transport = new RecordingSocialTransport(ChannelKind.Telegram)
    { FailFirst = new HttpRequestException("Failed https://api.telegram.org/bot" + secret + "/sendMessage") };
    var record = CampaignFor(ChannelKind.Telegram); Exception? observed = null;
    try
    {
        await new SocialCampaignRunner(transport, ChannelKind.Telegram).RunAsync(record,
            [ContactFor(ChannelKind.Telegram)], false, 0, _ => { }, default);
    }
    catch (Exception ex) { observed = ex; }
    Require(!JsonSerializer.Serialize(record).Contains(secret, StringComparison.Ordinal) &&
        !(observed?.ToString().Contains(secret, StringComparison.Ordinal) ?? false),
        "An unexpected HTTP failure exposed a bot token in persisted or surfaced error details");
    Require(record.State is "failed" or "completed", "Unexpected transport error left the campaign running");
    Require(observed is not null || record.Results.Single().Status is "failed" or "unknown", "Unexpected error was silently accepted");
}

static async Task TypedUncertainFailure()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    foreach (var errorKind in new[] { ChannelErrorKind.Network, ChannelErrorKind.InvalidResponse })
    {
        const string safeMessage = "لم يتم تأكيد قبول الرسالة لدى المزود.";
        var transport = new RecordingSocialTransport(kind)
        { FailFirst = new ChannelTransportException(errorKind, safeMessage) };
        var record = CampaignFor(kind); var callbacks = 0;
        await new SocialCampaignRunner(transport, kind).RunAsync(record, [ContactFor(kind)], false, 0,
            _ => callbacks++, default);
        var result = record.Results.Single();
        Require(result.Status == "unknown" && result.ProviderMessageId == "" && transport.Attempts == 1 &&
            transport.Sent.Count == 0 && callbacks == 1 && record.State == "completed",
            $"{kind} {errorKind} was incorrectly reported as a confirmed failure or acceptance");
        Require(!result.Detail.Contains("http", StringComparison.OrdinalIgnoreCase) &&
            !result.Detail.Contains("Bearer", StringComparison.OrdinalIgnoreCase),
            "A typed uncertain result exposed a request URI or authorization header");
    }
}

static async Task SmtpInFlightCancellation()
{
    using var cancellation = new CancellationTokenSource();
    var transport = new CancellingEmailTransport(cancellation); var record = new CampaignRecord(); var callbacks = 0;
    await new CampaignRunner(transport).RunAsync(record,
        [new() { Email = "one@example.com" }, new() { Email = "two@example.com" }], false, 0,
        _ => callbacks++, cancellation.Token);
    var result = record.Results.Single();
    Require(transport.Attempts == 1 && record.State == "cancelled" && callbacks == 1 &&
        result.Recipient == "one@example.com" && result.Status == "unknown" && result.ProviderMessageId == "",
        "An in-flight SMTP cancellation lost its unconfirmed attempt or sent a later recipient");
    Require(!JsonSerializer.Serialize(record).Contains(CancellingEmailTransport.SensitiveMarker, StringComparison.Ordinal),
        "SMTP cancellation persisted sensitive exception details");
}

static async Task SocialInFlightCancellation()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        using var cancellation = new CancellationTokenSource();
        var transport = new CancellingSocialTransport(cancellation); var record = CampaignFor(kind); var callbacks = 0;
        var contact = ContactFor(kind);
        await new SocialCampaignRunner(transport, kind).RunAsync(record,
            [contact, ContactFor(kind, 1)], false, 0, _ => callbacks++, cancellation.Token);
        var result = record.Results.Single();
        Require(transport.Attempts == 1 && record.State == "cancelled" && callbacks == 1 &&
            result.Recipient == SocialCampaignRunner.RecipientFor(contact, kind) &&
            result.Status == "unknown" && result.ProviderMessageId == "",
            $"An in-flight {kind} cancellation lost its unconfirmed attempt or sent a later recipient");
        Require(!JsonSerializer.Serialize(record).Contains(CancellingSocialTransport.SensitiveMarker, StringComparison.Ordinal),
            $"{kind} cancellation persisted a credential from the cancelled request URI");
    }
}

static async Task GlobalProviderFailuresPause()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    foreach (var errorKind in new[] { ChannelErrorKind.Authentication, ChannelErrorKind.RateLimited })
    {
        var transport = new RecordingSocialTransport(kind)
        {
            FailFirst = new ChannelTransportException(errorKind, "Fixture account-level failure",
                errorKind == ChannelErrorKind.RateLimited ? HttpStatusCode.TooManyRequests : HttpStatusCode.Unauthorized,
                retryAfterSeconds: errorKind == ChannelErrorKind.RateLimited ? 17 : null)
        };
        var record = CampaignFor(kind); var callbacks = 0; var runner = new SocialCampaignRunner(transport, kind);
        await runner.RunAsync(record, [ContactFor(kind), ContactFor(kind, 1)], false, 0, _ => callbacks++, default);
        Require(record.State == "paused" && transport.Attempts == 1 && transport.Sent.Count == 0 &&
            record.Results.Count == 1 && record.Results.Single().Status == "failed" && callbacks == 1,
            $"{kind} {errorKind} did not save the failed result and pause remaining recipients");
        if (errorKind == ChannelErrorKind.RateLimited)
            Require(record.Results.Single().Detail.Contains("17", StringComparison.Ordinal), "Rate limit report omitted retry-after seconds");
        var rejected = false;
        try { await runner.RunAsync(record, [], false, 0, _ => { }, default); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && transport.Attempts == 1, "A paused campaign automatically retried a request");
    }
}

static async Task SocialCancellation()
{
    foreach (var kind in Enum.GetValues<ChannelKind>())
    {
        using var cancellation = new CancellationTokenSource(); var transport = new RecordingSocialTransport(kind);
        var record = CampaignFor(kind); var runner = new SocialCampaignRunner(transport, kind);
        await runner.RunAsync(record, [ContactFor(kind), ContactFor(kind, 1)], false, 0, _ => cancellation.Cancel(), cancellation.Token);
        Require(record.State == "cancelled" && transport.Sent.Count == 1 && record.Results.Count == 1,
            $"{kind} cancellation sent a pending recipient");
        var rejected = false;
        try { await runner.RunAsync(record, [], false, 0, _ => { }, default); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected && transport.Sent.Count == 1, $"{kind} replay was permitted");
    }
}

static async Task SmtpCancellation()
{
    using var cancellation = new CancellationTokenSource(); var transport = new RecordingEmailTransport();
    var record = new CampaignRecord(); var runner = new CampaignRunner(transport);
    await runner.RunAsync(record, [new() { Email = "one@example.com" }, new() { Email = "two@example.com" }], false, 0,
        _ => cancellation.Cancel(), cancellation.Token);
    Require(record.State == "cancelled" && transport.Sent.Count == 1, "SMTP cancellation sent a pending recipient");
    var rejected = false;
    try { await runner.RunAsync(record, [], false, 0, _ => { }, default); }
    catch (InvalidOperationException) { rejected = true; }
    Require(rejected && transport.Sent.Count == 1, "SMTP replay was permitted");
}

static CampaignRecord CampaignFor(ChannelKind kind) => new()
{
    Name = "Fixture", Channel = kind == ChannelKind.Telegram ? "telegram" : "whatsapp", Body = "Hello {name}",
    WhatsAppTemplate = kind == ChannelKind.WhatsApp ? new() { Name = "welcome", LanguageCode = "ar", BodyParameters = ["{name}"] } : null
};

static Contact ContactFor(ChannelKind kind, int index = 0) => new()
{
    Name = index == 0 ? "زياد" : "أحمد", Email = $"person{index}@example.com", Phone = "+20100123456" + index,
    TelegramChatId = (123456789 + index).ToString(), Group = index == 0 ? "العملاء" : "الأصدقاء"
};

sealed class RecordingEmailTransport : IEmailTransport
{
    public List<(string Recipient, string Subject, string Body)> Sent { get; } = [];
    public Action? OnSend { get; init; }
    public Task SendAsync(string recipient, string subject, string body, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); OnSend?.Invoke(); Sent.Add((recipient, subject, body)); return Task.CompletedTask;
    }
}

sealed class CancellingEmailTransport(CancellationTokenSource cancellation) : IEmailTransport
{
    public const string SensitiveMarker = "fixture-sensitive-smtp-cancellation";
    public int Attempts { get; private set; }
    public Task SendAsync(string recipient, string subject, string body, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Attempts++; cancellation.Cancel();
        throw new OperationCanceledException("Fixture cancelled request with " + SensitiveMarker, token);
    }
}

sealed class CancellingSocialTransport(CancellationTokenSource cancellation) : ISocialTransport
{
    public const string SensitiveMarker = "fixture-sensitive-bot-cancellation";
    public int Attempts { get; private set; }
    public Task<ChannelReceipt> SendAsync(ChannelMessage message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Attempts++; cancellation.Cancel();
        throw new OperationCanceledException("https://api.telegram.org/bot" + SensitiveMarker + "/sendMessage", token);
    }
    public Task<string> CheckConnectionAsync(CancellationToken token) => throw new InvalidOperationException("No connection check expected");
}

sealed class RecordingSocialTransport(ChannelKind kind) : ISocialTransport
{
    public static readonly DateTimeOffset AcceptedAt = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    public List<ChannelMessage> Sent { get; } = [];
    public Action<ChannelMessage>? OnSend { get; init; }
    public Exception? FailFirst { get; init; }
    public int ConnectionChecks { get; private set; }
    private int attempts;
    public int Attempts => attempts;
    public Task<ChannelReceipt> SendAsync(ChannelMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); OnSend?.Invoke(message); attempts++;
        if (attempts == 1 && FailFirst is not null) throw FailFirst;
        Sent.Add(new ChannelMessage
        {
            Recipient = message.Recipient, Body = message.Body, WhatsAppMode = message.WhatsAppMode,
            WhatsAppTemplate = message.WhatsAppTemplate is null ? null : new()
            {
                Name = message.WhatsAppTemplate.Name, LanguageCode = message.WhatsAppTemplate.LanguageCode,
                BodyParameters = message.WhatsAppTemplate.BodyParameters.ToList()
            }
        });
        return Task.FromResult(new ChannelReceipt(kind, "fixture-" + attempts, AcceptedAt));
    }
    public Task<string> CheckConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); ConnectionChecks++; return Task.FromResult("Fixture account");
    }
}
