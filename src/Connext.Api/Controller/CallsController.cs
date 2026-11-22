
using Connext.Shared.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/calls")]
public class CallsController(IConfiguration cfg, TimeProvider clock) : ControllerBase
{
    // Client gọi trước khi tạo RTCPeerConnection: new RTCPeerConnection({ iceServers })
    [HttpGet("ice-servers")]
    public IActionResult GetIceServers()
    {
        var servers = new List<object>();

        var stun = cfg.GetSection("Ice:StunUrls").Get<string[]>() ?? [];
        if (stun.Length > 0) servers.Add(new { urls = stun });

        var turn = cfg.GetSection("Ice:TurnUrls").Get<string[]>() ?? [];
        if (turn.Length > 0)
        {
            string? username = cfg["Ice:TurnUsername"], credential = cfg["Ice:TurnCredential"];

            // Cách an toàn hơn: coturn "use-auth-secret", cấp tài khoản tạm theo thời gian
            var secret = cfg["Ice:TurnSecret"];
            if (!string.IsNullOrEmpty(secret))
            {
                var ttl = int.TryParse(cfg["Ice:TurnTtlMinutes"], out var m) ? m : 60;
                var expiry = clock.GetUtcNow().AddMinutes(ttl).ToUnixTimeSeconds();
                var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anon";
                username = $"{expiry}:{userId}";
                using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(secret));
                credential = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(username)));
            }

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(credential))
                servers.Add(new { urls = turn, username, credential });
        }

        return Ok(ApiResponse<object>.Ok(new { iceServers = servers }));
    }
}