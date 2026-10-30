using System.ClientModel;
using Microsoft.AspNetCore.Mvc;

namespace HealthSys_20260428_v2.csproj.Controllers;

// Customer service never interprets or executes commands.
public sealed class AgentController : Controller
{
    public const string UnavailableMessage = "請聯絡系統負責人 目前不能提供服務";

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Chat() => Json(new { reply = UnavailableMessage });

    // Older clients also receive only the unavailable message.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Plan() => Json(new { action = "clarify", message = UnavailableMessage });
}
