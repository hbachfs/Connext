using Connext.Api.Model;
using Connext.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Connext.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
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
        if (file.Length > maxMb * 1024 * 1024)
            return BadRequest(ApiResponse<object>.Fail($"Kích thước file vượt quá {maxMb}MB."));

        // Phân loại image hay file dựa trên content-type
        var type = file.ContentType.StartsWith("image/") ? "image" : "file";

        var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

        // Tạo tên file duy nhất tránh trùng lặp
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Trả về đường dẫn tương đối để frontend hiển thị
        var fileUrl = $"/uploads/{fileName}";

        return Ok(ApiResponse<object>.Ok(new
        {
            fileUrl = fileUrl,
            fileName = file.FileName, // Trả về tên gốc để hiển thị
            fileSize = file.Length,
            type = type
        }));
    }
}
