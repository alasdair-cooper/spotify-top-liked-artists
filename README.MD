# Spotify Top 10 Liked Artists

This is a small CLI app for displaying your top 10 most liked artists, and the number of liked songs by each artist.

This uses the new [dotnet run app.cs]() .NET feature. 

## Usage

You will need a client ID for a Spotify app with access to the Web API, a redirect URI of `http://127.0.0.1:8080/callback` and the `user-library-read` scope.

The following command will query for the client ID interactively:

```bash
dotnet run app.cs
```

Or you can pass it in as a program argument:

```bash
dotnet run app.cs -- client_id
```