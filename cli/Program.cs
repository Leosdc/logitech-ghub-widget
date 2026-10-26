using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GHubCli.Models;
using GHubCli.Services;

namespace GHubCli
{
    internal class Program
    {
        private static bool _noColor = false;
        private static bool _compact = false;
        private static bool _showHelpBanner = false;
        private static readonly object _renderLock = new();
        private static IReadOnlyList<CliDeviceInfo> _lastDevices = Array.Empty<CliDeviceInfo>();
        private static string _connectionStatus = "Iniciando...";
        private static bool _isConnected = false;
        private static string _lastUpdatedStr = "--:--:--";

        private const string Version = "1.1.0-cli";

        static async Task<int> Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string host = "127.0.0.1";
            int port = 9010;
            int interval = 2;
            bool once = false;
            bool jsonOutput = false;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i].ToLowerInvariant();
                if (arg is "-h" or "--help")
                {
                    ShowHelp();
                    return 0;
                }
                if (arg is "-v" or "--version")
                {
                    Console.WriteLine($"Logitech G HUB CLI Monitor v{Version}");
                    return 0;
                }
                if (arg is "--once")
                {
                    once = true;
                }
                else if (arg is "--json")
                {
                    jsonOutput = true;
                    _noColor = true;
                }
                else if (arg is "--no-color")
                {
                    _noColor = true;
                }
                else if (arg is "--compact")
                {
                    _compact = true;
                }
                else if ((arg is "--host" or "-h") && i + 1 < args.Length)
                {
                    host = args[++i];
                }
                else if ((arg is "--port" or "-p") && i + 1 < args.Length && int.TryParse(args[i + 1], out var p))
                {
                    port = p;
                    i++;
                }
                else if ((arg is "--interval" or "-i") && i + 1 < args.Length && int.TryParse(args[i + 1], out var intVal))
                {
                    interval = Math.Max(1, intVal);
                    i++;
                }
            }

            // Check if terminal supports ANSI or if color was disabled via environment (NO_COLOR)
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")))
            {
                _noColor = true;
            }

            var service = new LgHubCliService
            {
                Host = host,
                Port = port,
                RefreshIntervalSeconds = interval
            };

            // Snapshot mode (--once or --json)
            if (once || jsonOutput)
            {
                return await RunSnapshotModeAsync(service, jsonOutput);
            }

            // Interactive TUI mode
            return await RunInteractiveTuiAsync(service);
        }

        private static async Task<int> RunSnapshotModeAsync(LgHubCliService service, bool jsonOutput)
        {
            try
            {
                var devices = await service.FetchSnapshotAsync(TimeSpan.FromSeconds(3.5));

                if (jsonOutput)
                {
                    var exportData = devices.Select(d => new
                    {
                        id = d.Id,
                        name = d.Name,
                        category = d.CategoryLabel,
                        type = d.DeviceType,
                        connection = d.ConnectionType,
                        active = d.IsActive,
                        battery_percentage = d.BatteryPercentage,
                        is_charging = d.IsCharging,
                        mileage_hours = Math.Round(d.Mileage, 1),
                        consumption_mw = Math.Round(d.ConsumptionMw, 2),
                        status = d.StatusText,
                        last_updated = d.LastUpdated.ToString("o")
                    });

                    Console.WriteLine(JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true }));
                    return 0;
                }

                // Table output (--once)
                Console.WriteLine();
                Console.WriteLine(Color($"  ⚡ LOGITECH G HUB STATUS SNAPSHOT  (v{Version})", AnsiBold + AnsiCyan));
                Console.WriteLine(Color("  " + new string('─', 68), AnsiGray));

                if (devices.Count == 0)
                {
                    Console.WriteLine(Color("  Nenhum dispositivo detectado ou G HUB não está em execução.", AnsiYellow));
                    Console.WriteLine();
                    return 1;
                }

                foreach (var d in devices)
                {
                    string statusBadge = d.IsCharging ? Color("[⚡ CARREGANDO]", AnsiCyan)
                        : (d.IsActive ? Color("[● ATIVO]", AnsiGreen) : Color("[○ INATIVO]", AnsiGray));

                    string bar = RenderBatteryBar(d.BatteryPercentage, d.IsActive, d.IsCharging, 16);
                    string pct = Color($"{d.FormattedPercentage,4}", GetBatteryColor(d.BatteryPercentage, d.IsActive, d.IsCharging));

                    Console.WriteLine($"  {d.CategoryIcon}  {Color(d.Name, AnsiBold)}  {statusBadge}");
                    Console.WriteLine($"     Bateria: {bar} {pct}  |  Autonomia: {d.FormattedMileage}  |  Consumo: {d.FormattedConsumption}");
                    Console.WriteLine();
                }

                return 0;
            }
            catch (Exception ex)
            {
                if (jsonOutput)
                {
                    Console.WriteLine($"{{\"error\":\"{ex.Message}\"}}");
                }
                else
                {
                    Console.WriteLine(Color($"Erro ao consultar G HUB: {ex.Message}", AnsiRed));
                }
                return 1;
            }
        }

        private static async Task<int> RunInteractiveTuiAsync(LgHubCliService service)
        {
            var cts = new CancellationTokenSource();

            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            // Hide cursor & clear screen
            Console.Write("\x1b[?25l\x1b[2J");

            service.ConnectionStatusChanged += (conn, msg) =>
            {
                _isConnected = conn;
                _connectionStatus = msg;
                Render();
            };

            service.DevicesUpdated += (devs) =>
            {
                _lastDevices = devs;
                _lastUpdatedStr = DateTime.Now.ToString("HH:mm:ss");
                Render();
            };

            var serviceTask = service.StartAsync(cts.Token);

            // Keyboard listener loop
            var keyTask = Task.Run(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
                        {
                            cts.Cancel();
                            break;
                        }
                        if (key.Key == ConsoleKey.R)
                        {
                            _ = service.RefreshNowAsync();
                        }
                        if (key.Key == ConsoleKey.C)
                        {
                            _compact = !_compact;
                            Render();
                        }
                        if (key.Key == ConsoleKey.H)
                        {
                            _showHelpBanner = !_showHelpBanner;
                            Render();
                        }
                    }
                    Thread.Sleep(50);
                }
            }, cts.Token);

            // Periodic fallback render loop to update clock / status
            var clockTask = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(1000, cts.Token);
                    Render();
                }
            }, cts.Token);

            try
            {
                await Task.WhenAny(serviceTask, keyTask);
            }
            catch (OperationCanceledException) { }

            // Restore terminal
            Console.Write("\x1b[?25h\x1b[0m");
            Console.WriteLine();
            Console.WriteLine(Color("G HUB Monitor encerrado.", AnsiGray));
            return 0;
        }

        private static readonly System.Text.RegularExpressions.Regex AnsiRegex = new(@"\x1b\[[0-9;?]*[a-zA-Z]", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static int VisibleWidth(string text)
        {
            string clean = AnsiRegex.Replace(text, "");
            int width = 0;
            for (int i = 0; i < clean.Length; i++)
            {
                char c = clean[i];
                if (c is '\ufe0f' or '\ufe0e') continue;

                if (char.IsHighSurrogate(c) && i + 1 < clean.Length && char.IsLowSurrogate(clean[i + 1]))
                {
                    int codePoint = char.ConvertToUtf32(c, clean[i + 1]);
                    i++;
                    if (codePoint is >= 0x1F300 and <= 0x1FAFF)
                    {
                        width += 2;
                        continue;
                    }
                }

                if (c is '\u26a1')
                {
                    width += 2;
                }
                else
                {
                    width += 1;
                }
            }
            return width;
        }

        private static string FormatBoxLine(string content, int width = 72, string borderColor = AnsiCyan)
        {
            // \x1b[2K clears the whole line first, so cells skipped by absolute jumps
            // never keep text from a previous frame; \x1b[{width}G fixes the right border column.
            return $"\x1b[2K{Color("│", borderColor)} {content}\x1b[{width}G{Color("│", borderColor)}";
        }

        private static void Render()
        {
            lock (_renderLock)
            {
                var sb = new StringBuilder();

                // Move cursor to top-left (0,0) without clearing screen (flicker-free)
                sb.Append("\x1b[H");

                int width = 72;
                string borderTop = "┌" + new string('─', width - 2) + "┐";
                string borderMid = "├" + new string('─', width - 2) + "┤";
                string borderBot = "└" + new string('─', width - 2) + "┘";

                sb.AppendLine(Color(borderTop, AnsiCyan));

                // Title row
                string titleLeft = Color("⚡  LOGITECH G HUB MONITOR", AnsiBold + AnsiWhite);
                string titleRightText = $"v{Version}";
                int titleCol = width - 1 - titleRightText.Length;
                string titleRight = Color(titleRightText, AnsiDarkGray);
                sb.AppendLine(FormatBoxLine($"{titleLeft}\x1b[{titleCol}G{titleRight}", width, AnsiCyan));

                // Status row
                string statusDot = _isConnected ? Color("●", AnsiGreen) : Color("○", AnsiRed);
                string connText = _isConnected ? $"Conectado ({_connectionStatus})" : _connectionStatus;
                if (connText.Length > 40) connText = connText.Substring(0, 37) + "...";
                string statusLeft = $"Status: {statusDot} {connText}";
                string statusRight = $"Atualizado: {_lastUpdatedStr}";
                int statusCol = width - 1 - statusRight.Length;
                sb.AppendLine(FormatBoxLine($"{statusLeft}\x1b[{statusCol}G{statusRight}", width, AnsiCyan));

                sb.AppendLine(Color(borderMid, AnsiCyan));

                if (_showHelpBanner)
                {
                    sb.AppendLine(FormatBoxLine(Color("[Q/Esc] Sair  |  [R] Atualizar  |  [C] Modo Compacto  |  [H] Fechar Ajuda", AnsiYellow), width, AnsiCyan));
                    sb.AppendLine(Color(borderMid, AnsiCyan));
                }

                // Device rows
                if (_lastDevices.Count == 0)
                {
                    string msg = _isConnected ? "Aguardando detecção de periféricos Logitech..." : "Conectando ao agente do Logitech G HUB (porta 9010)...";
                    sb.AppendLine(FormatBoxLine(Color(msg, AnsiYellow), width, AnsiCyan));
                }
                else
                {
                    for (int i = 0; i < _lastDevices.Count; i++)
                    {
                        var d = _lastDevices[i];
                        string statusBadge = d.IsCharging 
                            ? Color("[⚡ CARREGANDO]", AnsiBold + AnsiCyan)
                            : (d.IsActive ? Color("[● ATIVO]", AnsiBold + AnsiGreen) : Color("[○ INATIVO]", AnsiDarkGray));

                        string bar = RenderBatteryBar(d.BatteryPercentage, d.IsActive, d.IsCharging, 16);
                        string pct = Color($"{d.FormattedPercentage,4}", GetBatteryColor(d.BatteryPercentage, d.IsActive, d.IsCharging));

                        // Line 1: Icon + Name (always at absolute column 7) + Badge.
                        // Cols 5-6 are written as real spaces so no stale text survives.
                        string headerLine = $"{d.CategoryIcon}\x1b[5G  \x1b[7G{Color(d.Name, AnsiBold + AnsiWhite)}  {statusBadge}";
                        sb.AppendLine(FormatBoxLine(headerLine, width, AnsiCyan));

                        // Line 2: Battery gauge
                        string barLine = $"    Bateria: {bar} {pct}";
                        if (_compact)
                        {
                            barLine += $"  {Color($"({d.FormattedMileage})", AnsiDarkGray)}";
                        }
                        sb.AppendLine(FormatBoxLine(barLine, width, AnsiCyan));

                        // Line 3: Details (if not compact)
                        if (!_compact)
                        {
                            string detailsLine = d.IsActive 
                                ? $"    Autonomia: {Color(d.FormattedMileage, AnsiWhite)}  |  Consumo: {Color(d.FormattedConsumption, AnsiWhite)}  |  {Color(d.ConnectionType, AnsiCyan)}"
                                : $"    {Color("Dispositivo inativo ou desligado", AnsiDarkGray)}  |  {Color(d.ConnectionType, AnsiDarkGray)}";
                            sb.AppendLine(FormatBoxLine(detailsLine, width, AnsiCyan));
                        }

                        // Separator between items
                        if (i < _lastDevices.Count - 1)
                        {
                            sb.AppendLine(FormatBoxLine("", width, AnsiCyan));
                        }
                    }
                }

                // Footer row
                sb.AppendLine(Color(borderMid, AnsiCyan));
                string footer = Color("[Q] Sair   [R] Atualizar   [C] Alternar Modo   [H] Ajuda", AnsiDarkGray);
                sb.AppendLine(FormatBoxLine(footer, width, AnsiCyan));
                sb.AppendLine(Color(borderBot, AnsiCyan));

                // Clear any trailing lines from previous larger frames
                sb.Append("\x1b[J");

                Console.Write(sb.ToString());
            }
        }

        private static string RenderBatteryBar(int percentage, bool isActive, bool isCharging, int totalBlocks)
        {
            if (!isActive && percentage == 0)
            {
                return Color("[" + new string('░', totalBlocks) + "]", AnsiDarkGray);
            }

            int filled = (int)Math.Round((percentage / 100.0) * totalBlocks);
            if (filled < 0) filled = 0;
            if (filled > totalBlocks) filled = totalBlocks;

            string fillStr = new('█', filled);
            string emptyStr = new('░', totalBlocks - filled);

            string color = GetBatteryColor(percentage, isActive, isCharging);
            return "[" + Color(fillStr, color) + Color(emptyStr, AnsiDarkGray) + "]";
        }

        private static string GetBatteryColor(int percentage, bool isActive, bool isCharging)
        {
            if (!isActive) return AnsiDarkGray;
            if (isCharging) return AnsiCyan;
            if (percentage > 50) return AnsiGreen;
            if (percentage > 20) return AnsiYellow;
            return AnsiRed;
        }

        private static void ShowHelp()
        {
            Console.WriteLine($@"
⚡ Logitech G HUB CLI Monitor v{Version}
Monitor de bateria e status em tempo real para periféricos Logitech G.
Multiplataforma: Windows, macOS e Linux.

USO:
  ghub-cli [opções]

OPÇÕES:
  -h, --help           Exibe esta tela de ajuda e sai.
  -v, --version        Exibe a versão do programa.
  --once               Consulta uma única vez, exibe a tabela e encerra.
  --json               Exporta dados estruturados em JSON para stdout e encerra.
  --host <ip>          Endereço do host do G HUB (padrão: 127.0.0.1).
  --port <porta>       Porta WebSocket do G HUB (padrão: 9010).
  -i, --interval <sec> Intervalo de atualização em segundos no modo TUI (padrão: 2).
  --compact            Inicia o monitor em modo compacto de uma linha por periférico.
  --no-color           Desativa o uso de cores ANSI no terminal.

CONTROLES NO MODO TUI:
  [Q] ou [Esc]         Sair do aplicativo.
  [R]                  Forçar atualização imediata.
  [C]                  Alternar entre modo detalhado e compacto.
  [H]                  Exibir/ocultar atalhos de ajuda.

EXEMPLOS:
  ghub-cli                     # Inicia painel em tempo real interativo
  ghub-cli --once              # Consulta rápida no terminal
  ghub-cli --json              # Para integração com Waybar, Polybar, Sketchybar
  ghub-cli --host 192.168.1.50 # Monitora G HUB em outra máquina na rede local
");
        }

        #region ANSI Colors
        private const string AnsiReset = "\x1b[0m";
        private const string AnsiBold = "\x1b[1m";
        private const string AnsiGreen = "\x1b[38;2;108;203;95m";
        private const string AnsiYellow = "\x1b[38;2;252;225;0m";
        private const string AnsiRed = "\x1b[38;2;255;100;100m";
        private const string AnsiCyan = "\x1b[38;2;96;205;255m";
        private const string AnsiWhite = "\x1b[38;2;240;240;240m";
        private const string AnsiGray = "\x1b[38;2;160;160;160m";
        private const string AnsiDarkGray = "\x1b[38;2;110;110;110m";

        private static string Color(string text, string ansiCode)
        {
            if (_noColor) return text;
            return $"{ansiCode}{text}{AnsiReset}";
        }
        #endregion
    }
}
