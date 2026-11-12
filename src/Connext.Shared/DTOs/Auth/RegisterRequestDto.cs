using System.ComponentModel.DataAnnotations;

namespace Connext.Shared.DTOs.Auth;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên hiển thị là bắt buộc.")]
    [MaxLength(100, ErrorMessage = "Tên hiển thị tối đa 100 ký tự.")]
    public string DisplayName { get; set; } = string.Empty;
}

