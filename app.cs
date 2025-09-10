#:sdk Microsoft.NET.Sdk.Web

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

#region Settings

var clientId = args.FirstOrDefault() ?? Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID");

while (clientId is null)
{
    Console.Write("Enter your Spotify Client ID: ");
    clientId = Console.ReadLine();
}

var baseUrl = "http://127.0.0.1:8080";
var callbackPath = "/callback";

#endregion

#region Auth 

var codeVerifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
var codeChallenge = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

var builder = WebApplication.CreateSlimBuilder(args);

builder.Logging.ClearProviders();

var app = builder.Build();

var redirectUri = baseUrl + callbackPath;

var authUrl = $"https://accounts.spotify.com/authorize?" +
    $"client_id={clientId}&" +
    $"response_type=code&" +
    $"redirect_uri={redirectUri}&" +
    $"code_challenge_method=S256&" +
    $"code_challenge={codeChallenge}&" +
    $"scope=user-library-read";

var tcs = new TaskCompletionSource<string>();

app.MapGet(callbackPath, (string code) =>
{
    tcs.SetResult(code);
    return "Authorization successful! You can close this window.";
});

var serverTask = app.RunAsync(baseUrl);

Process.Start(new ProcessStartInfo
{
    FileName = authUrl,
    UseShellExecute = true
});

var authCode = await tcs.Task;

await app.StopAsync();

var tokenClient = new HttpClient();
var tokenRequest = new FormUrlEncodedContent(
[
    new KeyValuePair<string, string>("client_id", clientId),
    new KeyValuePair<string, string>("grant_type", "authorization_code"),
    new KeyValuePair<string, string>("code", authCode),
    new KeyValuePair<string, string>("redirect_uri", redirectUri),
    new KeyValuePair<string, string>("code_verifier", codeVerifier)
]);

var tokenResponse = await tokenClient.PostAsync("https://accounts.spotify.com/api/token", tokenRequest);
var tokenData = await tokenResponse.Content.ReadFromJsonAsync<SpotifyTokenResponse>();
var token = tokenData?.AccessToken ?? throw new InvalidOperationException("Failed to get access token");

#endregion

#region Fetch Tracks

var apiClient = new HttpClient()
{
    BaseAddress = new Uri("https://api.spotify.com/v1/"),
    DefaultRequestHeaders = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
};

var tracks = new List<SpotifyTrack>();

var count = 0;

SpotifyGetUsersSavedTracksResponse? response = null;

do
{
    response = await apiClient.GetFromJsonAsync<SpotifyGetUsersSavedTracksResponse>(
        $"me/tracks?limit=50&offset={count}",
        JsonSerializerOptions.Web) ?? throw new UnreachableException();

    tracks.AddRange(response.Items.Select(i => i.Track));

    count += response.Items.Length;
}
while (response.Next is not null);

#endregion

var topArtists = tracks
    .SelectMany(t => t.Artists)
    .GroupBy(a => a.Id)
    .Select(g => new { Artist = g.First(), Count = g.Count() })
    .OrderByDescending(ac => ac.Count)
    .Take(10);

#region Output

Console.WriteLine("Your Top 10 Liked Artists:");
Console.WriteLine();
Console.WriteLine("┌─────┬──────────────────────────────────────────┬───────────────┐");
Console.WriteLine("│ Rank│ Artist Name                              │ Liked Tracks  │");
Console.WriteLine("├─────┼──────────────────────────────────────────┼───────────────┤");

var rank = 1;
foreach (var artist in topArtists)
{
    Console.WriteLine($"│ {rank,3} │ {artist.Artist.Name,-40} │ {artist.Count,13} │");
    rank++;
}

Console.WriteLine("└─────┴──────────────────────────────────────────┴───────────────┘");

#endregion

record SpotifyTokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

record SpotifyGetUsersSavedTracksResponse(string Next, SpotifyUserSavedTrack[] Items);

record SpotifyUserSavedTrack(SpotifyTrack Track);

record SpotifyTrack(string Id, string Name, SpotifyArtist[] Artists);

record SpotifyArtist(string Id, string Name);