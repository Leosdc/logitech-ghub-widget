#!/usr/bin/env python3
"""
⚡ Logitech G HUB CLI Monitor (Universal Python Version)
Monitor de bateria e status em tempo real para periféricos Logitech G.
Compatível com Linux, macOS e Windows.
Zero dependências externas (utiliza apenas a biblioteca padrão do Python).
"""

import sys
import os
import socket
import base64
import json
import time
import argparse
import select
import re
import unicodedata
from datetime import datetime

# Ensure UTF-8 output on Windows terminals
if sys.platform == "win32":
    if hasattr(sys.stdout, "reconfigure"):
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
            sys.stderr.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass

VERSION = "1.1.0-cli"

# ANSI Colors
ANSI_RESET = "\033[0m"
ANSI_BOLD = "\033[1m"
ANSI_GREEN = "\033[38;2;108;203;95m"
ANSI_YELLOW = "\033[38;2;252;225;0m"
ANSI_RED = "\033[38;2;255;100;100m"
ANSI_CYAN = "\033[38;2;96;205;255m"
ANSI_WHITE = "\033[38;2;240;240;240m"
ANSI_GRAY = "\033[38;2;160;160;160m"
ANSI_DARK_GRAY = "\033[38;2;110;110;110m"

class DeviceInfo:
    def __init__(self, dev_id, name, dev_type="UNKNOWN", conn_type="WIRELESS", active=True):
        self.id = dev_id
        self.name = name or "Dispositivo Logitech"
        self.device_type = dev_type or "UNKNOWN"
        self.connection_type = conn_type or "WIRELESS"
        self.battery_percentage = 0
        self.is_charging = False
        self.mileage = 0.0
        self.max_lifespan = 0.0
        self.consumption_mw = 0.0
        self.is_active = active
        self.last_updated = datetime.now()

    @property
    def category_label(self):
        upper_type = self.device_type.upper()
        upper_name = self.name.upper()
        if "MOUSE" in upper_type or "MOUSE" in upper_name or "SUPERLIGHT" in upper_name or "G305" in upper_name or "G502" in upper_name:
            return "Mouse"
        if "KEYBOARD" in upper_type or "KEYBOARD" in upper_name or "TECLADO" in upper_name or "G515" in upper_name or "G915" in upper_name:
            return "Teclado"
        if "HEADSET" in upper_type or "HEADSET" in upper_name or "FONE" in upper_name or "G325" in upper_name or "G733" in upper_name:
            return "Headset"
        return "Periférico"

    @property
    def category_icon(self):
        cat = self.category_label
        # Emoji widths vary between terminals (1 or 2 cells), so the TUI never
        # relies on them: name and right border use absolute cursor columns.
        if cat == "Mouse": return "\U0001F5B1"    # 🖱
        if cat == "Teclado": return "\u2328"      # ⌨
        if cat == "Headset": return "\U0001F3A7"  # 🎧
        return "\U0001F3AE"                       # 🎮

    @property
    def status_text(self):
        if not self.is_active: return "Inativo"
        if self.is_charging: return "Carregando"
        return "Ativo"

    @property
    def formatted_percentage(self):
        if not self.is_active and self.battery_percentage == 0:
            return "--%"
        return f"{self.battery_percentage}%"

    @property
    def formatted_mileage(self):
        if not self.is_active: return "Desconectado"
        if self.is_charging: return "Carregando..."
        if self.mileage > 0:
            hours = int(round(self.mileage))
            return f"~{hours}h restantes" if hours > 0 else "< 1h restante"
        return "Calculando..."

    @property
    def formatted_consumption(self):
        if not self.is_active: return "--"
        return f"{self.consumption_mw:.1f} mW" if self.consumption_mw > 0 else "Normal"


class StdWebSocketClient:
    """Zero-dependency RFC 6455 WebSocket client using pure Python standard library socket."""
    def __init__(self, host="127.0.0.1", port=9010):
        self.host = host
        self.port = port
        self.sock = None

    def connect(self, timeout=3.5):
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.settimeout(timeout)
        self.sock.connect((self.host, self.port))

        key = base64.b64encode(os.urandom(16)).decode('ascii')
        handshake = (
            f"GET / HTTP/1.1\r\n"
            f"Host: {self.host}:{self.port}\r\n"
            f"Upgrade: websocket\r\n"
            f"Connection: Upgrade\r\n"
            f"Sec-WebSocket-Key: {key}\r\n"
            f"Sec-WebSocket-Version: 13\r\n"
            f"Sec-WebSocket-Protocol: json\r\n"
            f"Origin: file://\r\n\r\n"
        )
        self.sock.sendall(handshake.encode('utf-8'))
        response = self.sock.recv(4096).decode('utf-8', errors='ignore')
        if "101 Switching Protocols" not in response:
            raise ConnectionError("Falha no handshake WebSocket com o Logitech G HUB.")

    def send_json(self, payload_str):
        if not self.sock: return
        data = payload_str.encode('utf-8')
        length = len(data)
        mask = os.urandom(4)
        header = bytearray([0x81])
        if length < 126:
            header.append(0x80 | length)
        elif length < 65536:
            header.append(0x80 | 126)
            header.extend(length.to_bytes(2, 'big'))
        else:
            header.append(0x80 | 127)
            header.extend(length.to_bytes(8, 'big'))
        header.extend(mask)
        masked_data = bytearray(b ^ mask[i % 4] for i, b in enumerate(data))
        self.sock.sendall(header + masked_data)

    def read_message(self, timeout=None):
        if not self.sock: return None
        if timeout is not None:
            self.sock.settimeout(timeout)
        try:
            head = self.sock.recv(2)
            if not head or len(head) < 2:
                return None
            b1, b2 = head[0], head[1]
            opcode = b1 & 0x0F
            masked = (b2 & 0x80) != 0
            payload_len = b2 & 0x7F

            if payload_len == 126:
                ext = self.sock.recv(2)
                payload_len = int.from_bytes(ext, 'big')
            elif payload_len == 127:
                ext = self.sock.recv(8)
                payload_len = int.from_bytes(ext, 'big')

            mask = self.sock.recv(4) if masked else None

            data = bytearray()
            while len(data) < payload_len:
                chunk = self.sock.recv(min(payload_len - len(data), 65536))
                if not chunk: break
                data.extend(chunk)

            if masked and mask:
                data = bytearray(b ^ mask[i % 4] for i, b in enumerate(data))

            if opcode == 1: # Text
                return data.decode('utf-8', errors='ignore')
            elif opcode == 8: # Close
                return None
            elif opcode == 9: # Ping -> Pong
                pong = bytearray([0x8A, 0x80])
                pong.extend(os.urandom(4))
                self.sock.sendall(pong)
                return ""
            return ""
        except socket.timeout:
            return None
        except Exception:
            return None

    def close(self):
        if self.sock:
            try:
                self.sock.close()
            except Exception:
                pass
            self.sock = None


def colorize(text, ansi_code, disable_color=False):
    if disable_color: return text
    return f"{ansi_code}{text}{ANSI_RESET}"


ANSI_REGEX = re.compile(r'\x1b\[[0-9;?]*[a-zA-Z]')

def visible_width(text):
    clean = ANSI_REGEX.sub('', text)
    width = 0
    for char in clean:
        if char in ('\ufe0f', '\ufe0e'):
            continue
        ea = unicodedata.east_asian_width(char)
        if ea in ('W', 'F'):
            width += 2
        else:
            width += 1
    return width


def format_box_line(content, width=72, border_color=ANSI_CYAN, no_color=False):
    left = colorize("│", border_color, no_color)
    right = colorize("│", border_color, no_color)
    # \033[2K clears the whole line first, so cells skipped by absolute jumps
    # (\033[nG) never keep text from a previous frame. \033[{width}G puts the
    # right border at a fixed column, no matter how the terminal measures emoji.
    return f"\033[2K{left} {content}\033[{width}G{right}\n"


def render_battery_bar(percentage, is_active, is_charging, total_blocks=16, no_color=False):
    if not is_active and percentage == 0:
        return colorize("[" + "░" * total_blocks + "]", ANSI_DARK_GRAY, no_color)

    filled = int(round((percentage / 100.0) * total_blocks))
    filled = max(0, min(total_blocks, filled))
    fill_str = "█" * filled
    empty_str = "░" * (total_blocks - filled)

    if not is_active:
        color = ANSI_DARK_GRAY
    elif is_charging:
        color = ANSI_CYAN
    elif percentage > 50:
        color = ANSI_GREEN
    elif percentage > 20:
        color = ANSI_YELLOW
    else:
        color = ANSI_RED

    return "[" + colorize(fill_str, color, no_color) + colorize(empty_str, ANSI_DARK_GRAY, no_color) + "]"


def fetch_devices_snapshot(host="127.0.0.1", port=9010, timeout=3.5):
    ws = StdWebSocketClient(host, port)
    ws.connect(timeout)

    devices = {}
    ws.send_json('{"path":"/devices/list","verb":"GET"}')

    pending_ids = set()
    deadline = time.time() + timeout
    devices_list_received = False

    while time.time() < deadline:
        msg = ws.read_message(timeout=max(0.1, deadline - time.time()))
        if not msg:
            if devices_list_received and len(pending_ids) == 0:
                break
            continue

        try:
            doc = json.loads(msg)
            path = doc.get("path", "")

            if path == "/devices/list":
                infos = doc.get("payload", {}).get("deviceInfos", [])
                for item in infos:
                    dev_id = item.get("id")
                    if not dev_id: continue
                    name = item.get("extendedDisplayName") or item.get("displayName") or dev_id
                    state = item.get("state", "UNKNOWN")
                    conn_type = item.get("connectionType", "WIRELESS")
                    dev_type = "UNKNOWN"

                    interfaces = item.get("activeInterfaces", [])
                    active_interface_count = len(interfaces) if isinstance(interfaces, list) else 0
                    for iface in interfaces:
                        if "deviceType" in iface:
                            dev_type = iface["deviceType"]
                            break

                    is_active = (state.upper() == "ACTIVE")
                    if active_interface_count == 0 and conn_type.upper() == "WIRELESS":
                        is_active = False

                    dev = DeviceInfo(dev_id, name, dev_type, conn_type, is_active)
                    devices[dev_id] = dev
                    pending_ids.add(dev_id)

                    ws.send_json(f'{{"path":"/battery/{dev_id}/state","verb":"GET"}}')

                devices_list_received = True
                if len(pending_ids) == 0:
                    break

            elif path.startswith("/battery/"):
                parts = path.split('/')
                dev_id = parts[2] if len(parts) > 2 else None
                payload = doc.get("payload")
                result = doc.get("result", {})
                code = result.get("code", "")

                has_error = (code and code.upper() != "SUCCESS") or not payload
                if dev_id and dev_id in devices:
                    dev = devices[dev_id]
                    if has_error:
                        dev.is_active = False
                    else:
                        dev.battery_percentage = payload.get("percentage", 0)
                        dev.is_charging = payload.get("charging", False)
                        dev.mileage = float(payload.get("mileage", 0.0))
                        dev.max_lifespan = float(payload.get("maxLifeSpan", 0.0))
                        cons = payload.get("consumption", {})
                        dev.consumption_mw = float(cons.get("value", 0.0)) if isinstance(cons, dict) else 0.0
                        dev.is_active = True
                        dev.last_updated = datetime.now()

                    pending_ids.discard(dev_id)

                if devices_list_received and len(pending_ids) == 0:
                    break
        except Exception:
            pass

    ws.close()
    return sorted(devices.values(), key=lambda d: (d.category_label, d.name))


def run_snapshot(host, port, json_mode, no_color):
    try:
        devices = fetch_devices_snapshot(host, port)
        if json_mode:
            out = []
            for d in devices:
                out.append({
                    "id": d.id,
                    "name": d.name,
                    "category": d.category_label,
                    "type": d.device_type,
                    "connection": d.connection_type,
                    "active": d.is_active,
                    "battery_percentage": d.battery_percentage,
                    "is_charging": d.is_charging,
                    "mileage_hours": round(d.mileage, 1),
                    "consumption_mw": round(d.consumption_mw, 2),
                    "status": d.status_text,
                    "last_updated": d.last_updated.isoformat()
                })
            print(json.dumps(out, indent=2))
            return 0

        print()
        print(colorize(f"  ⚡ LOGITECH G HUB STATUS SNAPSHOT  (v{VERSION})", ANSI_BOLD + ANSI_CYAN, no_color))
        print(colorize("  " + "─" * 68, ANSI_GRAY, no_color))

        if not devices:
            print(colorize("  Nenhum periférico detectado ou G HUB não está em execução.", ANSI_YELLOW, no_color))
            print()
            return 1

        for d in devices:
            status_badge = colorize("[⚡ CARREGANDO]", ANSI_CYAN, no_color) if d.is_charging else \
                           (colorize("[● ATIVO]", ANSI_GREEN, no_color) if d.is_active else colorize("[○ INATIVO]", ANSI_GRAY, no_color))
            bar = render_battery_bar(d.battery_percentage, d.is_active, d.is_charging, 16, no_color)
            pct = colorize(f"{d.formatted_percentage:>4}", ANSI_BOLD + ANSI_WHITE, no_color)

            print(f"  {d.category_icon}  {colorize(d.name, ANSI_BOLD, no_color)}  {status_badge}")
            print(f"     Bateria: {bar} {pct}  |  Autonomia: {d.formatted_mileage}  |  Consumo: {d.formatted_consumption}")
            print()
        return 0
    except Exception as e:
        if json_mode:
            print(json.dumps({"error": str(e)}))
        else:
            print(colorize(f"Erro ao consultar G HUB: {e}", ANSI_RED, no_color))
        return 1


def run_interactive(host, port, interval, compact, no_color):
    # Hide cursor and clear
    sys.stdout.write("\033[?25l\033[2J")
    sys.stdout.flush()

    try:
        ws = StdWebSocketClient(host, port)
        try:
            ws.connect(3.5)
            connected = True
            conn_msg = f"Conectado (ws://{host}:{port})"
            ws.send_json('{"path":"/devices/state/changed","verb":"SUBSCRIBE"}')
            ws.send_json('{"path":"/battery/state/changed","verb":"SUBSCRIBE"}')
            ws.send_json('{"path":"/devices/list","verb":"GET"}')
        except Exception as e:
            connected = False
            conn_msg = f"Falha ao conectar: {e}"

        devices = {}
        last_refresh = time.time()

        while True:
            # Poll socket
            if ws.sock:
                ready, _, _ = select.select([ws.sock], [], [], 0.1)
                if ready:
                    msg = ws.read_message(timeout=0.1)
                    if msg:
                        try:
                            doc = json.loads(msg)
                            path = doc.get("path", "")
                            if path == "/devices/list":
                                infos = doc.get("payload", {}).get("deviceInfos", [])
                                for item in infos:
                                    dev_id = item.get("id")
                                    if not dev_id: continue
                                    name = item.get("extendedDisplayName") or item.get("displayName") or dev_id
                                    conn_type = item.get("connectionType", "WIRELESS")
                                    state = item.get("state", "UNKNOWN")
                                    interfaces = item.get("activeInterfaces", [])
                                    is_active = (state.upper() == "ACTIVE")
                                    if len(interfaces) == 0 and conn_type.upper() == "WIRELESS":
                                        is_active = False

                                    dev = devices.get(dev_id) or DeviceInfo(dev_id, name, conn_type=conn_type, active=is_active)
                                    dev.name = name
                                    dev.is_active = is_active
                                    devices[dev_id] = dev
                                    ws.send_json(f'{{"path":"/battery/{dev_id}/state","verb":"GET"}}')
                            elif path.startswith("/battery/"):
                                parts = path.split('/')
                                dev_id = parts[2] if len(parts) > 2 else None
                                payload = doc.get("payload")
                                res = doc.get("result", {})
                                code = res.get("code", "")
                                if dev_id and dev_id in devices:
                                    dev = devices[dev_id]
                                    if (code and code.upper() != "SUCCESS") or not payload:
                                        dev.is_active = False
                                    else:
                                        dev.battery_percentage = payload.get("percentage", 0)
                                        dev.is_charging = payload.get("charging", False)
                                        dev.mileage = float(payload.get("mileage", 0.0))
                                        cons = payload.get("consumption", {})
                                        dev.consumption_mw = float(cons.get("value", 0.0)) if isinstance(cons, dict) else 0.0
                                        dev.is_active = True
                                        dev.last_updated = datetime.now()
                            elif path == "/devices/state/changed":
                                ws.send_json('{"path":"/devices/list","verb":"GET"}')
                        except Exception:
                            pass

            if time.time() - last_refresh >= interval:
                last_refresh = time.time()
                if ws.sock:
                    try:
                        ws.send_json('{"path":"/devices/list","verb":"GET"}')
                        for d_id in devices:
                            ws.send_json(f'{{"path":"/battery/{d_id}/state","verb":"GET"}}')
                    except Exception:
                        pass

            # Render frame
            width = 72
            sb = ["\033[H"]
            top_border = colorize("┌" + "─" * (width - 2) + "┐", ANSI_CYAN, no_color) + "\n"
            mid_border = colorize("├" + "─" * (width - 2) + "┤", ANSI_CYAN, no_color) + "\n"
            bot_border = colorize("└" + "─" * (width - 2) + "┘", ANSI_CYAN, no_color) + "\n"

            sb.append(top_border)

            title_left = colorize("⚡  LOGITECH G HUB MONITOR", ANSI_BOLD + ANSI_WHITE, no_color)
            title_right_text = f"v{VERSION}"
            title_col = width - 1 - len(title_right_text)
            title_right = colorize(title_right_text, ANSI_DARK_GRAY, no_color)
            sb.append(format_box_line(f"{title_left}\033[{title_col}G{title_right}", width, ANSI_CYAN, no_color))

            status_dot = colorize("●", ANSI_GREEN, no_color) if connected else colorize("○", ANSI_RED, no_color)
            now_str = datetime.now().strftime("%H:%M:%S")
            conn_display = conn_msg if len(conn_msg) <= 40 else conn_msg[:37] + "..."
            status_left = f"Status: {status_dot} {conn_display}"
            status_right = f"Atualizado: {now_str}"
            status_col = width - 1 - len(status_right)
            sb.append(format_box_line(f"{status_left}\033[{status_col}G{status_right}", width, ANSI_CYAN, no_color))

            sb.append(mid_border)

            dev_list = sorted(devices.values(), key=lambda d: (d.category_label, d.name))
            if not dev_list:
                msg = colorize("Aguardando detecção de periféricos Logitech...", ANSI_YELLOW, no_color)
                sb.append(format_box_line(msg, width, ANSI_CYAN, no_color))
            else:
                for idx, d in enumerate(dev_list):
                    status_badge = colorize("[⚡ CARREGANDO]", ANSI_BOLD + ANSI_CYAN, no_color) if d.is_charging else \
                                   (colorize("[● ATIVO]", ANSI_BOLD + ANSI_GREEN, no_color) if d.is_active else colorize("[○ INATIVO]", ANSI_DARK_GRAY, no_color))
                    bar = render_battery_bar(d.battery_percentage, d.is_active, d.is_charging, 16, no_color)
                    pct = colorize(f"{d.formatted_percentage:>4}", ANSI_BOLD + ANSI_WHITE, no_color)

                    # Name always starts at absolute column 7, whatever the icon width.
                    # Cols 5-6 are written as real spaces (not skipped) so no stale text survives.
                    hdr = f"{d.category_icon}\033[5G  \033[7G{colorize(d.name, ANSI_BOLD + ANSI_WHITE, no_color)}  {status_badge}"
                    sb.append(format_box_line(hdr, width, ANSI_CYAN, no_color))

                    bar_line = f"    Bateria: {bar} {pct}"
                    if compact:
                        bar_line += f"  {colorize(f'({d.formatted_mileage})', ANSI_DARK_GRAY, no_color)}"
                    sb.append(format_box_line(bar_line, width, ANSI_CYAN, no_color))

                    if not compact:
                        if d.is_active:
                            det = f"    Autonomia: {colorize(d.formatted_mileage, ANSI_WHITE, no_color)}  |  Consumo: {colorize(d.formatted_consumption, ANSI_WHITE, no_color)}  |  {colorize(d.connection_type, ANSI_CYAN, no_color)}"
                        else:
                            det = f"    {colorize('Dispositivo inativo ou desligado', ANSI_DARK_GRAY, no_color)}  |  {colorize(d.connection_type, ANSI_DARK_GRAY, no_color)}"
                        sb.append(format_box_line(det, width, ANSI_CYAN, no_color))

                    if idx < len(dev_list) - 1:
                        sb.append(format_box_line("", width, ANSI_CYAN, no_color))

            sb.append(mid_border)
            footer = colorize("[Ctrl+C] Sair   Modo: Atualização em Tempo Real", ANSI_DARK_GRAY, no_color)
            sb.append(format_box_line(footer, width, ANSI_CYAN, no_color))
            sb.append(bot_border)
            sb.append("\033[J")

            sys.stdout.write("".join(sb))
            sys.stdout.flush()

            time.sleep(0.4)

    except KeyboardInterrupt:
        pass
    finally:
        sys.stdout.write("\033[?25h\033[0m\n")
        sys.stdout.flush()
        print(colorize("G HUB Monitor encerrado.", ANSI_GRAY, no_color))
        if 'ws' in locals():
            ws.close()


def main():
    parser = argparse.ArgumentParser(
        description="⚡ Logitech G HUB CLI Monitor - Monitor de bateria em tempo real para Linux, macOS e Windows.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""Exemplos:
  ghub-cli.py                # Painel interativo em tempo real
  ghub-cli.py --once         # Consulta rápida única no terminal
  ghub-cli.py --json         # Saída estruturada em JSON (Waybar, Sketchybar)
  ghub-cli.py --host 10.0.0.2# Conectar a outra máquina rodando G HUB"""
    )
    parser.add_argument("-v", "--version", action="version", version=f"Logitech G HUB CLI Monitor v{VERSION}")
    parser.add_argument("--once", action="store_true", help="Consulta uma única vez e sai.")
    parser.add_argument("--json", action="store_true", help="Exporta dados em JSON para stdout e sai.")
    parser.add_argument("--compact", action="store_true", help="Exibição compacta sem detalhes extras.")
    parser.add_argument("--no-color", action="store_true", help="Desativa cores ANSI.")
    parser.add_argument("--host", default="127.0.0.1", help="Host do Logitech G HUB (padrão: 127.0.0.1)")
    parser.add_argument("-p", "--port", type=int, default=9010, help="Porta WebSocket do G HUB (padrão: 9010)")
    parser.add_argument("-i", "--interval", type=int, default=2, help="Intervalo de atualização em segundos (padrão: 2)")

    args = parser.parse_args()

    no_color = args.no_color or bool(os.environ.get("NO_COLOR"))

    if args.once or args.json:
        sys.exit(run_snapshot(args.host, args.port, args.json, no_color))
    else:
        run_interactive(args.host, args.port, max(1, args.interval), args.compact, no_color)


if __name__ == '__main__':
    main()
