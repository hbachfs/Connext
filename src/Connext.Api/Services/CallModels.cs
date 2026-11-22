namespace Connext.Api.Services;

public class CallOptions
{
    public int RingSeconds { get; set; } = 30;
    public int MaxSignalLength { get; set; } = 20000;   // giới hạn kích thước SDP/ICE
    public TimeSpan RingTimeout => TimeSpan.FromSeconds(RingSeconds);
}

public enum CallStatus { Ringing, Active }

public enum CallEndReason { Ended, Cancelled, Rejected, Missed, ConnectionLost }

public enum CallError
{
    None, InvalidTarget, UserNotFound, NotFriends, Blocked,
    Busy, Offline, NotFound, NotAllowed, InvalidState, TooLarge
}

public record CallResult(CallError Error, Guid CallId = default)
{
    public bool Ok => Error == CallError.None;
}

public static class CallMessages
{
    public static string For(CallError e) => e switch
    {
        CallError.UserNotFound => "Người dùng không tồn tại.",
        CallError.NotFriends => "Chỉ có thể gọi cho bạn bè.",
        CallError.Blocked => "Không thể gọi cho người dùng này.",
        CallError.Busy => "Máy bận.",
        CallError.Offline => "Người nhận không trực tuyến.",
        CallError.NotFound => "Cuộc gọi không tồn tại hoặc đã kết thúc.",
        CallError.NotAllowed => "Bạn không có quyền thực hiện thao tác này.",
        CallError.InvalidState => "Trạng thái cuộc gọi không hợp lệ.",
        CallError.TooLarge => "Dữ liệu tín hiệu quá lớn.",
        _ => "Yêu cầu không hợp lệ."
    };
}