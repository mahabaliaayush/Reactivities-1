using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using API.DTOs;
using Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using static API.DTOs.GitHUbInfo;


namespace API.Controllers;

public class AccountController(SignInManager<User> signInManager, IEmailSender<User> emailSender, IConfiguration config) : BaseApiController
{

    [AllowAnonymous]
    [HttpPost("github-login")]
    public async Task<ActionResult> LoginWithGitHub(string code)
    {
        if (string.IsNullOrEmpty(code))
            return BadRequest("Missing authorization code");

        using var httpclient = new HttpClient();
        httpclient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // setp 1 - Exchange code for access token

        var tokenResponse = await httpclient.PostAsJsonAsync(
            "https://github.com/login/oauth/access_token",
            new GitHubAuthRequest
            {
                Code = code,
                ClientId = config["Authentication:GitHub:ClientID"]!,
                ClientSecret = config["Authentication:GitHub:ClientSecret"]!,
                RedirectUri = $"{config["ClinetAppUrl"]}/auth-callback"
            }
        );

        if (!tokenResponse.IsSuccessStatusCode)
            return BadRequest("Failed to Get The Access Token");

        var tokenContent = await tokenResponse.Content.ReadFromJsonAsync<GitHunTokenResponse>();

        if (string.IsNullOrEmpty(tokenContent?.AccessToken))
            return BadRequest("Failed to retrieve access Token");

        // step 2 -  Fetch user-info from Github

        httpclient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenContent.AccessToken);
        httpclient.DefaultRequestHeaders.UserAgent.ParseAdd("FriendsGrid");

        var userresponce = await httpclient.GetAsync("https://api.github.com/user");

        if (!userresponce.IsSuccessStatusCode)
            return BadRequest("Failed to fethc user from GitHub");

        var user = await userresponce.Content.ReadFromJsonAsync<GithubUser>();
        if (user == null) return BadRequest("Failed to read user from GitHub");

        // step 3 - getting the Emial if needed 

        if (string.IsNullOrEmpty(user?.Email))
        {
            var emailResponce = await httpclient.GetAsync("https://api.github.com/user/emails");
            if (emailResponce.IsSuccessStatusCode)
            {
                var emails = await emailResponce.Content.ReadFromJsonAsync<List<GithubEmail>>();

                var primary = emails?.FirstOrDefault(e => e is { Primary: true, Varified: true })?.Email;

                if (string.IsNullOrEmpty(primary))
                    return BadRequest("Failed to get email From GitHub");

                user!.Email = primary;
            }
        }

        // step 4 - Find or creat user and sign in 
        var existingUser = await signInManager.UserManager.FindByEmailAsync(user!.Email);

        if (existingUser == null)
        {
            existingUser = new User
            {
                Email = user.Email,
                UserName = user.Email,
                DisplayName = user.Name,
                ImageUrl = user.ImageUrl
            };

            var createResult = await signInManager.UserManager.CreateAsync(existingUser);

            if (!createResult.Succeeded)
                return BadRequest("Failed to create user");
            
        }

        await signInManager.SignInAsync(existingUser, false);

        return Ok();

    }


    [AllowAnonymous]
    [HttpPost("register")]

    public async Task<ActionResult> RegisterUser(RegisterDTO registerDTO)
    {
        var user = new User
        {
            UserName = registerDTO.Email,
            Email = registerDTO.Email,
            DisplayName = registerDTO.DisplayName
        };

        var result = await signInManager.UserManager.CreateAsync(user, registerDTO.Password);
        if (result.Succeeded)
        {
            await SendConfirmationEmailAsync(user, registerDTO.Email);

            return Ok();
        }

        foreach (var error in result.Errors)

        {
            ModelState.AddModelError(error.Code, error.Description);

        }
        return ValidationProblem();

    }
    [AllowAnonymous]
    [HttpGet("resendConfirmEmail")]

    public async Task<ActionResult> ResendConfirmEmail(string? email, string? userId)
    {
        if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(userId))
        {
            return BadRequest("Email or UserId must be provided");
        }

        var user = await signInManager.UserManager.Users.FirstOrDefaultAsync(x => x.Email == email || x.Id == userId);

        if (user == null || string.IsNullOrEmpty(user.Email)) return BadRequest("User Not Found");

        await SendConfirmationEmailAsync(user, user.Email);

        return Ok();
    }

    private async Task SendConfirmationEmailAsync(User user, string email)
    {
        var code = await signInManager.UserManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var confirmEmailUrl = $"{config["ClinetAppUrl"]}/confirm-email?userId={user.Id}&code={code}";

        await emailSender.SendConfirmationLinkAsync(user, email, confirmEmailUrl);
    }

    [AllowAnonymous]
    [HttpGet("user-info")]

    public async Task<ActionResult> GetUserInfo()

    {
        if (User.Identity?.IsAuthenticated == false) return NoContent();
        var user = await signInManager.UserManager.GetUserAsync(User);

        if (user == null) return Unauthorized();

        return Ok(new
        {
            user.DisplayName,
            user.Email,
            user.Id,
            user.ImageUrl
        });
    }


    [HttpPost("logout")]

    public async Task<ActionResult> Logout()
    {
        await signInManager.SignOutAsync();

        return NoContent();

    }

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword(ChangePassDto passDto)
    {
        var user = await signInManager.UserManager.GetUserAsync(User);

        if (user == null) return Unauthorized();

        var result = await signInManager.UserManager.ChangePasswordAsync(user, passDto.CurrentPassword, passDto.NewPassword);

        if (result.Succeeded) return Ok();

        return BadRequest(result.Errors.First().Description);
    }
    

}

