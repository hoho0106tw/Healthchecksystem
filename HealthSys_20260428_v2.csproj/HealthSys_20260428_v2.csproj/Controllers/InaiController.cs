using System.ClientModel;
using HealthSys_20260428_v2.csproj.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthSys_20260428_v2.csproj.Controllers;

public sealed class InaiController(InaiChatService chat) : Controller
{
    // 智能查詢已移除；保留原 API 程式供查閱，不再對外提供 /Inai/Chat。
    // 智能客服使用 AgentController.Plan，路由為 POST /Agent/Plan。
    [NonAction]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(131072)]
    public async Task<IActionResult> Chat([FromBody] InaiRequest? request, CancellationToken cancellationToken)
    {
        var messages = request?.Messages;
        if (messages is null || messages.Count is < 1 or > 21
            || messages.Any(m => m is null || m.Role is not ("user" or "assistant") || string.IsNullOrWhiteSpace(m.Content) || m.Content.Length > 12000)
            || messages[^1].Role != "user" || messages[^1].Content.Length > 4000
            || messages.Sum(m => m.Content.Length) > 60000)
            return BadRequest(new { error = "查詢內容不符合格式或長度限制，請清除對話後重新輸入。" });
        try
        {
            return Json(new { reply = await chat.ReplyAsync(messages, cancellationToken) });
        }
        catch (ClientResultException ex)
        {
            return StatusCode(502, new { error = $"AI 服務暫時無法回應（HTTP {ex.Status}），請稍後重試。" });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new { error = "查詢逾時，請稍後重試。" });
        }
        catch (Exception)
        {
            return StatusCode(503, new { error = "智能查詢暫時無法使用，請聯絡系統管理員。" });
        }
    }
}
