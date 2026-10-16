using System.Runtime.InteropServices;
using CubeBridge;
using CubeBridge.Providers;

// --console : run in the terminal without a tray icon
// --dry-run : fetch usage and log the payload, do not contact the cube (implies --console)
var dryRun = args.Contains("--dry-run");
var console = dryRun || args.Contains("--console");
if (console) AttachConsole(-1); // WinExe has no console of its own

var builder = Host.CreateApplicationBuilder(args.Where(a => a is not ("--dry-run" or "--console")).ToArray());
builder.Services.Configure<CubeOptions>(builder.Configuration.GetSection("Cube"));
if (dryRun) builder.Services.PostConfigure<CubeOptions>(o => o.DryRun = true);

builder.Services.AddSingleton<RawDump>();
builder.Services.AddSingleton<BridgeStatus>();
builder.Services.AddHttpClient<ClaudeUsageProvider>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient<CodexUsageProvider>(c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddSingleton<IUsageProvider>(sp => sp.GetRequiredService<ClaudeUsageProvider>());
builder.Services.AddSingleton<IUsageProvider>(sp => sp.GetRequiredService<CodexUsageProvider>());
builder.Services.AddHttpClient<CubeClient>(c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

if (console)
{
    host.Run();
    return;
}

// Tray mode: host runs in the background, WinForms message loop owns the main thread.
await host.StartAsync();
ApplicationConfiguration.Initialize();
using var tray = new NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "CubeBridge" };
tray.ContextMenuStrip = new ContextMenuStrip();
tray.ContextMenuStrip.Items.Add("Open debug folder", null, (_, _) =>
    System.Diagnostics.Process.Start("explorer.exe", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CubeBridge")));
tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Application.Exit());

var status = host.Services.GetRequiredService<BridgeStatus>();
var ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
status.Changed += s => ui.Post(_ => tray.Text = s.Length > 127 ? s[..127] : s, null);

Application.Run();
tray.Visible = false;
await host.StopAsync();

[DllImport("kernel32.dll")]
static extern bool AttachConsole(int processId);
