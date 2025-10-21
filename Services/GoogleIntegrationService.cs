using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using TaskTracker.Models;
using System.Text.RegularExpressions;
using System.IO;
using System;

namespace TaskTracker.Services;

public interface IGoogleIntegrationService
{
    Task<bool> ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task<bool> TestConnectionAsync(CancellationToken ct = default);
    Task<int> ScanCalendarAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    bool IsConnected { get; }
    string? LastError { get; }
}

public class GoogleIntegrationService : IGoogleIntegrationService
{
    private const string AppName = "TaskTracker";
    private static readonly string[] Scopes = { CalendarService.Scope.CalendarReadonly };

    private readonly IConfigurationService _config;
    private readonly ITaskManagementService _tasks;
    private readonly IJiraApiService _jiraApi;

    public string? LastError { get; private set; }

    public GoogleIntegrationService(IConfigurationService config, ITaskManagementService tasks, IJiraApiService jiraApi)
    {
        _config = config;
        _tasks = tasks;
        _jiraApi = jiraApi;
    }

    public bool IsConnected => !string.IsNullOrWhiteSpace(_config.AppSettings.Google.RefreshToken);

    private async Task<CalendarService?> CreateCalendarServiceAsync(CancellationToken ct)
    {
        LastError = null;
        var gs = _config.AppSettings.Google;

        if (string.IsNullOrWhiteSpace(gs.ClientId) || string.IsNullOrWhiteSpace(gs.ClientSecret))
            throw new InvalidOperationException("Google Client ID/Secret are required");

        var dataStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTracker.Google");
        var dataStore = new FileDataStore(dataStorePath, true);

        GoogleAuthorizationCodeFlow? flow = null;
        try
        {
            flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = gs.ClientId,
                    ClientSecret = gs.ClientSecret
                },
                Scopes = Scopes,
                DataStore = dataStore
            });
        }
        catch (Exception ex)
        {
            LastError = "Failed to initialize Google auth flow: " + ex.Message;
            LogHelper.Error("[GoogleIntegration] Flow init error: " + ex.Message, nameof(GoogleIntegrationService), ex.StackTrace);
            return null;
        }

        var codeReceiver = new LocalServerCodeReceiver();
    var app = new AuthorizationCodeInstalledApp(flow, codeReceiver);
        UserCredential? credential = null;
        try
        {
            credential = await app.AuthorizeAsync("user", ct);
        }
        catch (TaskCanceledException)
        {
            LastError = "Authorization cancelled";
            return null;
        }
        catch (TokenResponseException tex)
        {
            var err = tex.Error != null ? ($"{tex.Error.Error}: {tex.Error.ErrorDescription}") : tex.Message;
            LastError = "Token response error: " + err;
            LogHelper.Error("[GoogleIntegration] Token error: " + tex.Message, nameof(GoogleIntegrationService), tex.StackTrace);
            return null;
        }
        catch (Exception ex)
        {
            LastError = "Authorization failed: " + ex.Message;
            LogHelper.Error("[GoogleIntegration] Auth error: " + ex.Message, nameof(GoogleIntegrationService), ex.StackTrace);
            return null;
        }

        // Persist refresh token back to settings (if available)
        if (!string.IsNullOrWhiteSpace(credential?.Token?.RefreshToken))
        {
            if (!string.Equals(gs.RefreshToken, credential.Token.RefreshToken, StringComparison.Ordinal))
            {
                gs.RefreshToken = credential.Token.RefreshToken;
                await _config.SaveSettingsAsync();
            }
        }
        else if (string.IsNullOrWhiteSpace(gs.RefreshToken))
        {
            // No refresh token returned & none stored – likely due to using wrong OAuth client type (should be "Desktop app")
            LastError = "No refresh token returned. Ensure the OAuth client type is 'Desktop app' and try again.";
            LogHelper.Warn("[GoogleIntegration] Missing refresh token after auth.", nameof(GoogleIntegrationService));
        }

        var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = AppName
        });

        return service;
    }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        try
        {
            var service = await CreateCalendarServiceAsync(ct);
            var ok = service != null && string.IsNullOrWhiteSpace(LastError);
            if (!ok && string.IsNullOrWhiteSpace(LastError))
            {
                LastError = "Unknown error during connection";
            }
            return ok;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            LogHelper.Error("[GoogleIntegration] ConnectAsync exception: " + ex.Message, nameof(GoogleIntegrationService), ex.StackTrace);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            var gs = _config.AppSettings.Google;
            gs.RefreshToken = string.Empty;
            await _config.SaveSettingsAsync();

            var dataStorePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskTracker.Google");
            if (Directory.Exists(dataStorePath))
            {
                Directory.Delete(dataStorePath, true);
            }
        }
        catch
        {
            // ignore
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var service = await CreateCalendarServiceAsync(ct);
            if (service == null) return false;

            var listRequest = service.CalendarList.List();
            listRequest.MaxResults = 1;
            var result = await listRequest.ExecuteAsync(ct);
            return result?.Items != null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            LogHelper.Error("[GoogleIntegration] TestConnectionAsync exception: " + ex.Message, nameof(GoogleIntegrationService), ex.StackTrace);
            return false;
        }
    }

    public async Task<int> ScanCalendarAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // Parse bracketed JIRA keys like [ABC-123]
        var keyRegex = new Regex("\\[(?<key>[A-Z][A-Z0-9]+-\\d+)\\]", RegexOptions.Compiled);
        int added = 0;

        try
        {
            var service = await CreateCalendarServiceAsync(ct);
            if (service == null) return 0;

            var timeMin = new DateTimeOffset(new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Local)).ToUniversalTime();
            var timeMax = new DateTimeOffset(new DateTime(to.Year, to.Month, to.Day, 23, 59, 59, DateTimeKind.Local)).ToUniversalTime();

            var eventsReq = service.Events.List("primary");
            eventsReq.TimeMinDateTimeOffset = timeMin;
            eventsReq.TimeMaxDateTimeOffset = timeMax;
            eventsReq.SingleEvents = true;
            eventsReq.ShowDeleted = false;

            Events events = await eventsReq.ExecuteAsync(ct);
            if (events?.Items == null) return 0;

            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ev in events.Items)
            {
                var text = ev.Summary ?? string.Empty;
                foreach (Match m in keyRegex.Matches(text))
                {
                    var key = m.Groups["key"].Value.ToUpperInvariant();
                    if (!seenKeys.Add(key)) continue;

                    // Validate in JIRA
                    var jiraTask = await _jiraApi.GetTaskByKeyAsync(key);
                    if (jiraTask == null) continue;

                    // Ensure task exists locally
                    var addedTask = await _tasks.AddTaskByNumberAsync(key);
                    if (addedTask != null) added++;
                }
            }

            return added;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            LogHelper.Error("[GoogleIntegration] ScanCalendarAsync exception: " + ex.Message, nameof(GoogleIntegrationService), ex.StackTrace);
            return added;
        }
    }
}
