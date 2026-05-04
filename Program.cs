using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static readonly ConcurrentDictionary<string, WebSocket> Clients = new();

    static async Task Main()
    {
        string port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://+:{port}/");
        listener.Start();
        Console.WriteLine($"Сервер запущено на порту {port}");

        while (true)
        {
            var context = await listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
                _ = Task.Run(() => HandleClient(context));
            else { context.Response.StatusCode = 400; context.Response.Close(); }
        }
    }

    static async Task HandleClient(HttpListenerContext context)
    {
        var ws = (await context.AcceptWebSocketAsync(null)).WebSocket;
        string id = Guid.NewGuid().ToString("N")[..8];
        Clients[id] = ws;
        Console.WriteLine($"[+] {id} підключився. Усього: {Clients.Count}");

        await Send(ws, JsonSerializer.Serialize(new { type = "connected", clientId = id }));
        await Broadcast(JsonSerializer.Serialize(new { type = "system", text = $"Клієнт {id} приєднався до чату" }));

        var buf = new byte[4096];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var r = await ws.ReceiveAsync(buf, CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close) break;

                var doc = JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, r.Count)).RootElement;
                if (doc.TryGetProperty("text", out var t))
                {
                    Console.WriteLine($"[{id}]: {t}");
                    await Broadcast(JsonSerializer.Serialize(new { type = "message", from = id, text = t.GetString() }));
                }
            }
        }
        finally
        {
            Clients.TryRemove(id, out _);
            Console.WriteLine($"[-] {id} відключився. Усього: {Clients.Count}");
            await Broadcast(JsonSerializer.Serialize(new { type = "system", text = $"Клієнт {id} покинув чат" }));
            if (ws.State != WebSocketState.Closed)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
    }

    static async Task Broadcast(string json)
    {
        var seg = new ArraySegment<byte>(Encoding.UTF8.GetBytes(json));
        foreach (var (_, ws) in Clients)
            if (ws.State == WebSocketState.Open)
                try { await ws.SendAsync(seg, WebSocketMessageType.Text, true, CancellationToken.None); } catch { }
    }

    static Task Send(WebSocket ws, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}

class ChatMessage {
    public string From { get; set; } = "";
    public string Text { get; set; } = ""; 
    public bool IsSystem { get; set; } 
}