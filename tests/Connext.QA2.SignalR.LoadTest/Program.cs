using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

Console.WriteLine("=== CONNEXT QA2 - SIGNALR LOAD TEST ===");

// 1. Doc tham so kiem thu
if (args.Length != 3 ||
    !int.TryParse(args[0], out int clientCount) ||
    !int.TryParse(args[1], out int messageRate) ||
    !int.TryParse(args[2], out int duration) ||
    clientCount < 2 || clientCount > 100 ||
    messageRate < 1 || messageRate > 20 ||
    duration < 1 || duration > 60)
{
    Console.WriteLine("Tham so: SoKetNoi TinNhanMoiGiay SoGiay");
    Environment.ExitCode = 1;
    return;
}

string? tokenA =
    Environment.GetEnvironmentVariable("QA2_TOKEN");

string? tokenB2 =
    Environment.GetEnvironmentVariable("QA2_TOKEN_B2");

if (string.IsNullOrWhiteSpace(tokenA) ||
    string.IsNullOrWhiteSpace(tokenB2) ||
    tokenA == tokenB2 ||
    !Guid.TryParse(
        Environment.GetEnvironmentVariable("QA2_CONVERSATION_ID"),
        out Guid groupId) ||
    groupId == Guid.Empty)
{
    Console.WriteLine("Thieu JWT hoac Conversation ID.");
    Environment.ExitCode = 1;
    return;
}

int plannedMessages = messageRate * duration;
int expectedDeliveries = plannedMessages * (clientCount - 1);

Console.WriteLine($"So ket noi: {clientCount}");
Console.WriteLine($"Tin nhan/giay muc tieu: {messageRate}");
Console.WriteLine($"Thoi gian: {duration} giay");

var connections = new List<HubConnection>();

var sentTimes =
    new ConcurrentDictionary<string, long>();

var receivedTimes =
    new ConcurrentDictionary<(int, string), double>();

string runId = Guid.NewGuid().ToString();

int attempted = 0;
int sentSuccess = 0;

try
{
    // 2. Tao cac ket noi WebSocket
    for (int i = 0; i < clientCount; i++)
    {
        int clientIndex = i;
        string clientToken = i == 0 ? tokenA : tokenB2;

        var client = new HubConnectionBuilder()
            .WithUrl("http://localhost:5232/hubs/chat", options =>
            {
                options.AccessTokenProvider =
                    () => Task.FromResult<string?>(clientToken);

                options.Transports =
                    HttpTransportType.WebSockets;
            })
            .Build();

        // Cac client B2 lang nghe tin nhan
        if (i > 0)
        {
            client.On<JsonElement>("ReceiveMessage", message =>
            {
                if (!message.TryGetProperty("content", out var value))
                    return;

                string? content = value.GetString();

                if (content != null &&
                    sentTimes.TryGetValue(content, out long start))
                {
                    double latency =
                        Stopwatch.GetElapsedTime(start)
                            .TotalMilliseconds;

                    receivedTimes.TryAdd(
                        (clientIndex, content), latency);
                }
            });
        }

        connections.Add(client);
    }

    // 3. Ket noi dong thoi va tham gia hoi thoai
    Console.WriteLine("\nDang khoi tao cac ket noi...");

    var connectWatch = Stopwatch.StartNew();

    var results = await Task.WhenAll(
        connections.Select(async (client, index) =>
        {
            try
            {
                using var setupTimeout =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(30));

                await client.StartAsync(setupTimeout.Token);

                await client.InvokeAsync(
                    "JoinConversation",
                    groupId,
                    setupTimeout.Token);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Client {index + 1} loi: {ex.GetType().Name}");

                return false;
            }
        }));

    connectWatch.Stop();

    int connectedCount = results.Count(x => x);

    Console.WriteLine(
        $"Ket noi va Join thanh cong: {connectedCount}/{clientCount}");

    Console.WriteLine(
        $"Ket noi that bai: {clientCount - connectedCount}");

    Console.WriteLine(
        $"Thoi gian khoi tao: {connectWatch.ElapsedMilliseconds} ms");

    if (connectedCount != clientCount)
    {
        Console.WriteLine("[FAIL] Khong du ket noi.");
        Environment.ExitCode = 1;
        return;
    }

    // 4. Gui tin nhan theo muc tai
    Console.WriteLine("\nBat dau kiem thu tai...");

    var loadWatch = Stopwatch.StartNew();

    double intervalMs = 1000.0 / messageRate;
    double nextSendMs = 0;

    for (int i = 0; i < plannedMessages; i++)
    {
        double waitMs =
            nextSendMs - loadWatch.Elapsed.TotalMilliseconds;

        if (waitMs > 0)
            await Task.Delay(TimeSpan.FromMilliseconds(waitMs));

        if (loadWatch.Elapsed.TotalSeconds >= duration)
            break;

        nextSendMs += intervalMs;

        string content = $"QA2 LOAD {runId} - {i + 1}";

        sentTimes[content] = Stopwatch.GetTimestamp();

        attempted++;

        try
        {
            using var sendTimeout =
                new CancellationTokenSource(
                    TimeSpan.FromSeconds(10));

            await connections[0].InvokeAsync("SendMessage", new
            {
                conversationId = groupId,
                content = content
            }, sendTimeout.Token);

            sentSuccess++;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Loi gui tin {i + 1}: {ex.GetType().Name}");
        }
    }

    // Duy tri cua so kiem thu theo thoi gian da chon
    double remaining =
        duration - loadWatch.Elapsed.TotalSeconds;

    if (remaining > 0)
        await Task.Delay(TimeSpan.FromSeconds(remaining));

    loadWatch.Stop();

    // 5. Cho cac tin nhan con lai duoc nhan
    var drainWatch = Stopwatch.StartNew();

    while (receivedTimes.Count <
           attempted * (clientCount - 1) &&
           drainWatch.Elapsed.TotalSeconds < 10)
    {
        await Task.Delay(100);
    }

    // 6. Tong hop ket qua
    double[] latencies =
        receivedTimes.Values.OrderBy(x => x).ToArray();

    int received = latencies.Length;

    int activeConnections = connections.Count(
        x => x.State == HubConnectionState.Connected);

    double actualRate =
        sentSuccess / loadWatch.Elapsed.TotalSeconds;

    double errorRate =
        (plannedMessages - sentSuccess)
        * 100.0 / plannedMessages;

    double deliveryRate =
        received * 100.0 / expectedDeliveries;

    Console.WriteLine("\n========== KET QUA LOAD TEST ==========");

    Console.WriteLine($"Ket noi yeu cau: {clientCount}");
    Console.WriteLine($"Ket noi thanh cong: {connectedCount}");
    Console.WriteLine($"Ket noi cuoi bai: {activeConnections}");

    Console.WriteLine($"Tin nhan du kien: {plannedMessages}");
    Console.WriteLine($"Tin nhan da thu gui: {attempted}");
    Console.WriteLine($"Gui thanh cong: {sentSuccess}");
    Console.WriteLine($"Gui that bai: {attempted - sentSuccess}");
    Console.WriteLine($"Chua gui: {plannedMessages - attempted}");

    Console.WriteLine($"Luot nhan du kien: {expectedDeliveries}");
    Console.WriteLine($"Luot nhan thuc te: {received}");

    Console.WriteLine(
        $"Luot nhan con thieu: {expectedDeliveries - received}");

    Console.WriteLine($"Ty le nhan: {deliveryRate:F2}%");
    Console.WriteLine($"Ty le loi gui/khong dat: {errorRate:F2}%");

    Console.WriteLine(
        $"Thoi gian tao tai: {loadWatch.Elapsed.TotalSeconds:F2} s");

    Console.WriteLine($"Throughput: {actualRate:F2} msg/s");

    if (latencies.Length > 0)
    {
        int p95Index =
            (int)Math.Ceiling(latencies.Length * 0.95) - 1;

        Console.WriteLine($"Latency Min: {latencies.Min():F2} ms");
        Console.WriteLine($"Latency Avg: {latencies.Average():F2} ms");
        Console.WriteLine($"Latency Max: {latencies.Max():F2} ms");
        Console.WriteLine($"Latency P95: {latencies[p95Index]:F2} ms");
    }

    Console.WriteLine("=======================================");

    if (sentSuccess == plannedMessages &&
        received == expectedDeliveries &&
        activeConnections == clientCount)
    {
        Console.WriteLine("[PASS] LOAD TEST");
    }
    else
    {
        Console.WriteLine("[FAIL] LOAD TEST");
        Environment.ExitCode = 1;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[FAIL] {ex.GetType().Name}");
    Environment.ExitCode = 1;
}
finally
{
    // 7. Giai phong tat ca ket noi
    foreach (var client in connections)
    {
        try
        {
            await client.DisposeAsync();
        }
        catch
        {
            Console.WriteLine("Loi khi dong ket noi.");
            Environment.ExitCode = 1;
        }
    }

    Console.WriteLine("Da ket thuc kiem thu.");
}
