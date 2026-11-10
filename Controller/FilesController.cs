using Connext.Api.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    // Danh sách đuôi file được phép. Cố ý KHÔNG có .html/.htm/.svg/.js... vì file được phục vụ tĩnh cùng origin với API (nguy cơ XSS).
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private static readonly HashSet<string> FileExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".zip", ".rar", ".7z" };

    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public FilesController(IWebHostEnvironment env, IConfiguration config)
    {
        _env = env;
        _config = config;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn file."));

        var maxMb = _config.GetValue<int>("Upload:MaxFileSizeMB", 5);
        if (file.Length > maxMb * 1024L * 1024L)
            return BadRequest(ApiResponse<object>.Fail($"Kích thước file vượt quá {maxMb}MB."));

        // Phân loại theo ĐUÔI FILE (content-type do client tự khai nên không đáng tin)
        var extension = Path.GetExtension(file.FileName);
        string type;
        if (ImageExtensions.Contains(extension)) type = "image";
        else if (FileExtensions.Contains(extension)) type = "file";
        else return BadRequest(ApiResponse<object>.Fail("Định dạng file không được hỗ trợ."));

        var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        // Tên file ngẫu nhiên, đuôi chữ thường lấy từ danh sách cho phép
        var fileName = $"{Guid.NewGuid()}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Trả về đường dẫn tương đối để frontend hiển thị / gửi qua ChatHub.SendMessage
        var fileUrl = $"/uploads/{fileName}";

        return Ok(ApiResponse<object>.Ok(new
        {
            fileUrl,
            fileName = Path.GetFileName(file.FileName), // tên gốc để hiển thị (bỏ phần đường dẫn nếu có)
            fileSize = file.Length,
            type
        }));
    }
}