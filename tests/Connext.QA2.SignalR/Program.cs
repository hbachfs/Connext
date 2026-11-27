
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;

Console.WriteLine("=== CONNEXT QA2 - SIGNALR TESTING ===");
Console.WriteLine("TC_SIGNALR_05 - Ket noi WebSocket");

// Lay JWT tu bien moi truong
string? token = Environment.GetEnvironmentVariable("QA2_TOKEN");

if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("Chua cung cap JWT.");
    return;
}

// Khoi tao ket noi SignalR
await using var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5232/hubs/chat", options =>
    {
        options.AccessTokenProvider =
            () => Task.FromResult<string?>(token);

        options.Transports = HttpTransportType.WebSockets;
    })
    .Build();

try
{
    using var timeout = new CancellationTokenSource(
        TimeSpan.FromSeconds(15));

    // Ket noi den ChatHub
    await connection.StartAsync(timeout.Token);

    if (connection.State == HubConnectionState.Connected)
    {
        Console.WriteLine("[PASS] TC_SIGNALR_05");
        Console.WriteLine("Ket noi WebSocket thanh cong.");
        Console.WriteLine($"State: {connection.State}");
        Console.WriteLine($"Connection ID: {connection.ConnectionId}");
    }
    else
    {
        Console.WriteLine("[FAIL] TC_SIGNALR_05");
        Environment.ExitCode = 1;
    }

    // TC_SIGNALR_06 - Thanh vien tham gia hoi thoai
    Console.WriteLine();
    Console.WriteLine("TC_SIGNALR_06 - JoinConversation");

    string? conversationId =
        Environment.GetEnvironmentVariable("QA2_CONVERSATION_ID");

    if (Guid.TryParse(conversationId, out Guid groupId))
    {
        try
        {
            await connection.InvokeAsync("JoinConversation", groupId);

            Console.WriteLine("[PASS] TC_SIGNALR_06");
            Console.WriteLine("Thanh vien tham gia hoi thoai thanh cong.");
            Console.WriteLine($"Conversation ID: {groupId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[FAIL] TC_SIGNALR_06");
            Console.WriteLine(ex.Message);
            Environment.ExitCode = 1;
        }
    }
    else
    {
        Console.WriteLine("[FAIL] Conversation ID khong hop le.");
        Environment.ExitCode = 1;
    }


// TC_SIGNALR_07 - Nguoi ngoai tham gia hoi thoai
Console.WriteLine();
Console.WriteLine("TC_SIGNALR_07 - Unauthorized JoinConversation");

string? tokenD =
    Environment.GetEnvironmentVariable("QA2_TOKEN_D");

if (string.IsNullOrWhiteSpace(tokenD) ||
    tokenD == token ||
    groupId == Guid.Empty)
{
    Console.WriteLine("[FAIL] Du lieu kiem thu khong hop le.");
    Environment.ExitCode = 1;
}
else
{
    await using var connectionD = new HubConnectionBuilder()
        .WithUrl("http://localhost:5232/hubs/chat", options =>
        {
            options.AccessTokenProvider =
                () => Task.FromResult<string?>(tokenD);

            options.Transports = HttpTransportType.WebSockets;
        })
        .Build();

    try
    {
        await connectionD.StartAsync();

        Console.WriteLine("Tai khoan D da ket noi WebSocket.");

        try
        {
            await connectionD.InvokeAsync(
                "JoinConversation", groupId);

            Console.WriteLine("[FAIL] TC_SIGNALR_07");
            Console.WriteLine("Nguoi ngoai tham gia duoc hoi thoai.");
            Environment.ExitCode = 1;
        }
        catch (Microsoft.AspNetCore.SignalR.HubException ex)
            when (ex.Message.Contains(
                "Bạn không thuộc cuộc hội thoại này."))
        {
            Console.WriteLine("[PASS] TC_SIGNALR_07");
            Console.WriteLine("Backend tu choi nguoi ngoai tham gia.");
            Console.WriteLine(ex.Message);
        }

        await connectionD.StopAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine("[FAIL] TC_SIGNALR_07");
        Console.WriteLine(ex.GetType().Name);
        Environment.ExitCode = 1;
    }
}


// TC_SIGNALR_08 - Gui va nhan tin nhan Realtime
Console.WriteLine();
Console.WriteLine("TC_SIGNALR_08 - SendMessage and ReceiveMessage");

string? tokenB2 =
    Environment.GetEnvironmentVariable("QA2_TOKEN_B2");

try
{
    if (string.IsNullOrWhiteSpace(tokenB2) ||
        tokenB2 == token ||
        groupId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "Du lieu kiem thu khong hop le.");
    }

    // Tao ket noi cho tai khoan B2
    await using var connectionB2 = new HubConnectionBuilder()
        .WithUrl("http://localhost:5232/hubs/chat", options =>
        {
            options.AccessTokenProvider =
                () => Task.FromResult<string?>(tokenB2);

            options.Transports = HttpTransportType.WebSockets;
        })
        .Build();

    // Tao noi dung rieng cho moi lan kiem thu
    string testMessage = "QA2 SignalR Test " + Guid.NewGuid();

    var receivedMessage =
        new TaskCompletionSource<System.Text.Json.JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

    // B2 lang nghe tin nhan tu Server
    using var subscription =
        connectionB2.On<System.Text.Json.JsonElement>(
            "ReceiveMessage", message =>
            {
                if (message.TryGetProperty("content", out var content)
                    && content.GetString() == testMessage)
                {
                    receivedMessage.TrySetResult(message);
                }
            });

    // Thoi gian cho cua TC_SIGNALR_08
    using var timeout08 =
        new CancellationTokenSource(TimeSpan.FromSeconds(30));

    // B2 ket noi va tham gia hoi thoai
    await connectionB2.StartAsync(timeout08.Token);

    await connectionB2.InvokeAsync(
        "JoinConversation", groupId, timeout08.Token);

    Console.WriteLine("Tai khoan B2 da tham gia hoi thoai.");

    // Tai khoan A gui tin nhan
    await connection.InvokeAsync("SendMessage", new
    {
        conversationId = groupId,
        content = testMessage
    }, timeout08.Token);

    Console.WriteLine($"A gui: {testMessage}");

    // Cho B2 nhan tin nhan trong 10 giay
    var message = await receivedMessage.Task.WaitAsync(
        TimeSpan.FromSeconds(10));

    Guid messageId = message.GetProperty("id").GetGuid();

    if (messageId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "Message ID khong hop le.");
    }

    Console.WriteLine($"B2 nhan: {message.GetProperty("content").GetString()}");
    Console.WriteLine($"Message ID: {messageId}");

    await connectionB2.StopAsync();

    Console.WriteLine("[PASS] TC_SIGNALR_08");
    Console.WriteLine("Gui va nhan tin nhan Realtime thanh cong.");
}
catch (Exception ex)
{
    Console.WriteLine("[FAIL] TC_SIGNALR_08");
    Console.WriteLine(ex.GetType().Name);
    Environment.ExitCode = 1;
}


// TC_SIGNALR_09 - Nguoi ngoai gui tin nhan
Console.WriteLine();
Console.WriteLine("TC_SIGNALR_09 - Unauthorized SendMessage");

try
{
    if (string.IsNullOrWhiteSpace(tokenD) ||
        tokenD == token ||
        groupId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "Du lieu kiem thu khong hop le.");
    }

    // Tao ket noi cho tai khoan D
    await using var connectionD09 = new HubConnectionBuilder()
        .WithUrl("http://localhost:5232/hubs/chat", options =>
        {
            options.AccessTokenProvider =
                () => Task.FromResult<string?>(tokenD);

            options.Transports = HttpTransportType.WebSockets;
        })
        .Build();

    using var timeout09 =
        new CancellationTokenSource(TimeSpan.FromSeconds(30));

    string unauthorizedMessage =
        "QA2 Unauthorized Message " + Guid.NewGuid();

    var received09 = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    // Tai khoan A lang nghe tin nhan
    using var subscription09 =
        connection.On<System.Text.Json.JsonElement>(
            "ReceiveMessage", message =>
            {
                if (message.TryGetProperty("content", out var content)
                    && content.GetString() == unauthorizedMessage)
                {
                    received09.TrySetResult(true);
                }
            });

    await connection.InvokeAsync(
        "JoinConversation", groupId, timeout09.Token);

    // D ket noi WebSocket
    await connectionD09.StartAsync(timeout09.Token);

    Console.WriteLine("Tai khoan D da ket noi WebSocket.");

    bool rejected09 = false;

    try
    {
        // D gui tin nhan vao nhom khong thuoc ve minh
        await connectionD09.InvokeAsync("SendMessage", new
        {
            conversationId = groupId,
            content = unauthorizedMessage
        }, timeout09.Token);
    }
    catch (Microsoft.AspNetCore.SignalR.HubException ex)
        when (ex.Message.Contains(
            "Bạn không thuộc cuộc hội thoại này."))
    {
        rejected09 = true;
        Console.WriteLine("Backend tu choi D gui tin nhan.");
        Console.WriteLine(ex.Message);
    }

    // Kiem tra A co nhan tin nhan trai phep khong
    await Task.WhenAny(
        received09.Task, Task.Delay(3000));

    if (rejected09 &&
        !received09.Task.IsCompleted &&
        connection.State == HubConnectionState.Connected)
    {
        Console.WriteLine("A khong nhan tin nhan trai phep.");
        Console.WriteLine("[PASS] TC_SIGNALR_09");
    }
    else
    {
        Console.WriteLine("[FAIL] TC_SIGNALR_09");
        Environment.ExitCode = 1;
    }

    await connectionD09.StopAsync();
}
catch (Exception ex)
{
    Console.WriteLine("[FAIL] TC_SIGNALR_09");
    Console.WriteLine(ex.GetType().Name);
    Environment.ExitCode = 1;
}


// TC_SIGNALR_10 - Kiem tra trang thai dang nhap
Console.WriteLine();
Console.WriteLine("TC_SIGNALR_10 - Typing");

try
{
    if (string.IsNullOrWhiteSpace(tokenB2) ||
        tokenB2 == token ||
        groupId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "Du lieu kiem thu khong hop le.");
    }

    // Tao ket noi cho tai khoan B2
    await using var connectionB210 = new HubConnectionBuilder()
        .WithUrl("http://localhost:5232/hubs/chat", options =>
        {
            options.AccessTokenProvider =
                () => Task.FromResult<string?>(tokenB2);

            options.Transports = HttpTransportType.WebSockets;
        })
        .Build();

    using var timeout10 =
        new CancellationTokenSource(TimeSpan.FromSeconds(40));

    var typingStarted10 = new TaskCompletionSource<Guid>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    var typingStopped10 = new TaskCompletionSource<Guid>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    // B2 lang nghe su kien Typing
    using var subscription10 =
        connectionB210.On<Guid, Guid, bool>(
            "Typing", (senderId10, conversationId10, isTyping10) =>
            {
                if (conversationId10 == groupId &&
                    senderId10 != Guid.Empty)
                {
                    if (isTyping10)
                    {
                        typingStarted10.TrySetResult(senderId10);
                    }
                    else
                    {
                        typingStopped10.TrySetResult(senderId10);
                    }
                }
            });

    // B2 ket noi va tham gia hoi thoai
    await connectionB210.StartAsync(timeout10.Token);

    await connectionB210.InvokeAsync(
        "JoinConversation", groupId, timeout10.Token);

    Console.WriteLine("Tai khoan B2 da tham gia hoi thoai.");

    // A bat dau nhap tin nhan
    await connection.InvokeAsync(
        "Typing", groupId, true, timeout10.Token);

    Console.WriteLine("A gui Typing = True");

    Guid senderStarted10 =
        await typingStarted10.Task.WaitAsync(
            TimeSpan.FromSeconds(10));

    Console.WriteLine("B2 nhan Typing = True");

    // A ngung nhap tin nhan
    await connection.InvokeAsync(
        "Typing", groupId, false, timeout10.Token);

    Console.WriteLine("A gui Typing = False");

    Guid senderStopped10 =
        await typingStopped10.Task.WaitAsync(
            TimeSpan.FromSeconds(10));

    Console.WriteLine("B2 nhan Typing = False");

    // Kiem tra hai su kien cung mot nguoi gui
    if (senderStarted10 != senderStopped10)
    {
        throw new InvalidOperationException(
            "Sender ID khong trung khop.");
    }

    Console.WriteLine($"Sender ID: {senderStarted10}");

    await connectionB210.StopAsync();

    Console.WriteLine("[PASS] TC_SIGNALR_10");
    Console.WriteLine("Trang thai Typing hoat dong dung.");
}
catch (Exception ex)
{
    Console.WriteLine("[FAIL] TC_SIGNALR_10");
    Console.WriteLine(ex.GetType().Name);
    Environment.ExitCode = 1;
}


// TC_SIGNALR_11 - Nguoi ngoai gui trang thai dang nhap
Console.WriteLine();
Console.WriteLine("TC_SIGNALR_11 - Unauthorized Typing");

try
{
    if (string.IsNullOrWhiteSpace(tokenD) ||
        tokenD == token ||
        groupId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "Du lieu kiem thu khong hop le.");
    }

    // Tao ket noi cho tai khoan D
    await using var connectionD11 = new HubConnectionBuilder()
        .WithUrl("http://localhost:5232/hubs/chat", options =>
        {
            options.AccessTokenProvider =
                () => Task.FromResult<string?>(tokenD);

            options.Transports = HttpTransportType.WebSockets;
        })
        .Build();

    using var timeout11 =
        new CancellationTokenSource(TimeSpan.FromSeconds(30));

    var received11 = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    // Tai khoan A tham gia hoi thoai
    await connection.InvokeAsync(
        "JoinConversation", groupId, timeout11.Token);

    // D ket noi WebSocket
    await connectionD11.StartAsync(timeout11.Token);

    Console.WriteLine("Tai khoan D da ket noi WebSocket.");

    // A lang nghe su kien Typing
    using var subscription11 =
        connection.On<Guid, Guid, bool>(
            "Typing", (senderId11, conversationId11, isTyping11) =>
            {
                if (conversationId11 == groupId && isTyping11)
                {
                    received11.TrySetResult(true);
                }
            });

    bool rejected11 = false;

    try
    {
        // D gui trang thai dang nhap trai phep
        await connectionD11.InvokeAsync(
            "Typing", groupId, true, timeout11.Token);
    }
    catch (Microsoft.AspNetCore.SignalR.HubException ex)
        when (ex.Message.Contains(
            "Bạn không thuộc cuộc hội thoại này."))
    {
        rejected11 = true;

        Console.WriteLine("Backend tu choi Typing cua D.");
        Console.WriteLine(ex.Message);
    }

    // Quan sat A trong 3 giay
    await Task.WhenAny(
        received11.Task, Task.Delay(3000));

    if (rejected11 &&
        !received11.Task.IsCompleted &&
        connection.State == HubConnectionState.Connected)
    {
        Console.WriteLine("A khong nhan Typing trai phep.");
        Console.WriteLine("[PASS] TC_SIGNALR_11");
    }
    else
    {
        Console.WriteLine("[FAIL] TC_SIGNALR_11");
        Environment.ExitCode = 1;
    }

    await connectionD11.StopAsync();
}
catch (Exception ex)
{
    Console.WriteLine("[FAIL] TC_SIGNALR_11");
    Console.WriteLine(ex.GetType().Name);
    Environment.ExitCode = 1;
}


// TC_SIGNALR_12 - Xac nhan da nhan tin nhan
Console.WriteLine();
Console.WriteLine("TC_SIGNALR_12 - AcknowledgeDelivered");

try
{
    if (string.IsNullOrWhiteSpace(tokenB2) ||
        tokenB2 == token ||
        groupId == Guid.Empty ||
        !Guid.TryParse(
            Environment.GetEnvironmentVariable("QA2_B2_USER_ID"),
            out Guid expectedB2Id12))
    {
        throw new InvalidOperationException(
            "Du lieu kiem thu khong hop le.");
    }

    // Tao ket noi cho tai khoan B2
    await using var connectionB212 = new HubConnectionBuilder()
        .WithUrl("http://localhost:5232/hubs/chat", options =>
        {
            options.AccessTokenProvider =
                () => Task.FromResult<string?>(tokenB2);

            options.Transports = HttpTransportType.WebSockets;
        })
        .Build();

    using var timeout12 =
        new CancellationTokenSource(TimeSpan.FromSeconds(40));

    string testMessage12 =
        "QA2 Delivered Test " + Guid.NewGuid();

    var received12 = new TaskCompletionSource<Guid>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    // B2 lang nghe tin nhan tu A
    using var subscriptionReceive12 =
        connectionB212.On<System.Text.Json.JsonElement>(
            "ReceiveMessage", message =>
            {
                if (message.TryGetProperty("content", out var content)
                    && content.GetString() == testMessage12)
                {
                    received12.TrySetResult(
                        message.GetProperty("id").GetGuid());
                }
            });

    await connectionB212.StartAsync(timeout12.Token);

    await connectionB212.InvokeAsync(
        "JoinConversation", groupId, timeout12.Token);

    // A gui tin nhan moi
    await connection.InvokeAsync("SendMessage", new
    {
        conversationId = groupId,
        content = testMessage12
    }, timeout12.Token);

    Console.WriteLine("A da gui tin nhan.");

    // B2 nhan tin nhan va lay Message ID
    Guid messageId12 = await received12.Task.WaitAsync(
        TimeSpan.FromSeconds(10));

    if (messageId12 == Guid.Empty)
    {
        throw new InvalidOperationException(
            "Message ID khong hop le.");
    }

    Console.WriteLine("B2 da nhan tin nhan.");
    Console.WriteLine($"Message ID: {messageId12}");

    var delivered12 = new TaskCompletionSource<Guid>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    // A lang nghe su kien MessageDelivered
    using var subscriptionDelivered12 =
        connection.On<Guid, Guid>(
            "MessageDelivered", (deliveredMessageId12, userId12) =>
            {
                if (deliveredMessageId12 == messageId12)
                {
                    delivered12.TrySetResult(userId12);
                }
            });

    // B2 xac nhan da nhan tin nhan
    await connectionB212.InvokeAsync(
        "AcknowledgeDelivered",
        messageId12,
        timeout12.Token);

    Console.WriteLine("B2 da gui AcknowledgeDelivered.");

    // A cho su kien MessageDelivered
    Guid deliveredUserId12 =
        await delivered12.Task.WaitAsync(
            TimeSpan.FromSeconds(10));

    // Kiem tra dung tai khoan B2
    if (deliveredUserId12 != expectedB2Id12)
    {
        throw new InvalidOperationException(
            "User ID xac nhan khong trung khop.");
    }

    Console.WriteLine("A da nhan su kien MessageDelivered.");
    Console.WriteLine($"Delivered User ID: {deliveredUserId12}");

    await connectionB212.StopAsync();

    Console.WriteLine("[PASS] TC_SIGNALR_12");
    Console.WriteLine("Xac nhan da nhan tin nhan thanh cong.");
}
catch (Exception ex)
{
    Console.WriteLine("[FAIL] TC_SIGNALR_12");
    Console.WriteLine(ex.GetType().Name);
    Console.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}


// TC_SIGNALR_13 - Ngat ket noi WebSocket
Console.WriteLine();
Console.WriteLine("TC_SIGNALR_13 - Disconnect and Offline");

try
{
    if (string.IsNullOrWhiteSpace(tokenB2) ||
        tokenB2 == token ||
        !Guid.TryParse(
            Environment.GetEnvironmentVariable("QA2_A_USER_ID"),
            out Guid expectedAId13))
    {
        throw new InvalidOperationException(
            "Du lieu kiem thu khong hop le.");
    }

    // Tao ket noi cho tai khoan B2
    await using var connectionB213 = new HubConnectionBuilder()
        .WithUrl("http://localhost:5232/hubs/chat", options =>
        {
            options.AccessTokenProvider =
                () => Task.FromResult<string?>(tokenB2);

            options.Transports = HttpTransportType.WebSockets;
        })
        .Build();

    using var timeout13 =
        new CancellationTokenSource(TimeSpan.FromSeconds(40));

    var online13 = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    var offline13 = new TaskCompletionSource<bool>(
        TaskCreationOptions.RunContinuationsAsynchronously);

    // B2 lang nghe trang thai cua tai khoan A
    using var subscription13 =
        connectionB213.On<Guid, bool>(
            "OnlineChanged", (userId13, isOnline13) =>
            {
                if (userId13 == expectedAId13)
                {
                    if (isOnline13)
                    {
                        online13.TrySetResult(true);
                    }
                    else
                    {
                        offline13.TrySetResult(true);
                    }
                }
            });

    // B2 ket noi WebSocket
    await connectionB213.StartAsync(timeout13.Token);

    Console.WriteLine("Tai khoan B2 da ket noi.");

    // Xac nhan A dang Online
    await online13.Task.WaitAsync(
        TimeSpan.FromSeconds(10));

    Console.WriteLine("B2 nhan trang thai A: Online");

    if (offline13.Task.IsCompleted)
    {
        throw new InvalidOperationException(
            "A da Offline truoc khi kiem thu.");
    }

    // A ngat ket noi WebSocket
    await connection.StopAsync(timeout13.Token);

    Console.WriteLine("A da ngat ket noi.");
    Console.WriteLine($"State: {connection.State}");

    if (connection.State != HubConnectionState.Disconnected)
    {
        throw new InvalidOperationException(
            "A chua ngat ket noi thanh cong.");
    }

    // B2 cho thong bao A Offline
    await offline13.Task.WaitAsync(
        TimeSpan.FromSeconds(10));

    Console.WriteLine("B2 nhan trang thai A: Offline");

    if (connectionB213.State != HubConnectionState.Connected)
    {
        throw new InvalidOperationException(
            "Ket noi B2 khong con hoat dong.");
    }

    await connectionB213.StopAsync();

    Console.WriteLine("[PASS] TC_SIGNALR_13");
    Console.WriteLine("Ngat ket noi va cap nhat Offline thanh cong.");
}
catch (Exception ex)
{
    Console.WriteLine("[FAIL] TC_SIGNALR_13");
    Console.WriteLine(ex.GetType().Name);
    Console.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}


    await connection.StopAsync();
}
catch (Exception ex)
{
    Console.WriteLine("[FAIL] TC_SIGNALR_05");
    Console.WriteLine(ex.GetType().Name);
    Environment.ExitCode = 1;
}
