
using System;
using System.Text.Json.Serialization;

namespace API.DTOs;

public class GitHUbInfo
{
    public class GitHubAuthRequest
    {
        public required string Code { get; set; }

        [JsonPropertyName("client_id")]
        public required string ClientId { get; set; }

        [JsonPropertyName("client_secret")]
        public required string ClientSecret { get; set; }

        [JsonPropertyName("redirect_uri")]
        public required string RedirectUri { get; set; }
    }


    public class GitHunTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
    }

    public class GithubUser
    {
        public string Email { get; set; } = "";

        public string Name { get; set; } = "";

        [JsonPropertyName("avatar_url")]
        public string? ImageUrl { get; set; }

    }

    public class GithubEmail
    {
        public string Email { get; set; } = "";

        public Boolean Primary { get; set; }

        public Boolean Varified { get; set; }
    }
}
