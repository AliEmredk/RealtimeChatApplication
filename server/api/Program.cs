using api.Services;
using dataaccess;
using Microsoft.EntityFrameworkCore;
using StateleSSE.AspNetCore;
using StateleSSE.AspNetCore.Extensions;
using dataaccess.Entities;
using Npgsql;
using Npgsql.NameTranslation;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<IRoomChatService, RoomChatService>();


var redisConn =
    Environment.GetEnvironmentVariable("REDIS_CONNECTION")
    ?? "127.0.0.1:6379,abortConnect=false"; // fallback to local docker redis

builder.Services.AddRedisSseBackplane(conf =>
{
    conf.RedisConnectionString = redisConn;
});

builder.Services.AddEfRealtime();

var cs = builder.Configuration.GetConnectionString("Db")!;

builder.Services.AddDbContext<MyDbContext>((sp, opt) =>
{
    opt.UseNpgsql(cs);
    opt.UseSnakeCaseNamingConvention();

    if (!builder.Environment.IsEnvironment("Migration"))
        opt.AddEfRealtimeInterceptor(sp);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// disconnect hook (same as you already have)
var backplane = app.Services.GetRequiredService<ISseBackplane>();
backplane.OnClientDisconnected += async (_, e) =>
{
    foreach (var g in e.Groups)
    {
        if (!g.StartsWith("room:")) continue;

        var members = await backplane.Groups.GetMembersAsync(g);

        var evt = new
        {
            type = "system",
            message = $"{e.ConnectionId} disconnected",
            room = g["room:".Length..],
            at = DateTimeOffset.UtcNow
        };

        foreach (var memberId in members)
        {
            if (memberId == e.ConnectionId) continue;
            await backplane.Clients.SendToGroupAsync($"client:{memberId}", evt);
        }
    }
};

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.Run();