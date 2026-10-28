# ⚡ Logitech G HUB CLI Monitor (Windows, macOS, Linux)

[![Multiplataforma](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D4?style=flat)](https://github.com/Leosdc/logitech-ghub-widget)
[![.NET 8.0+](https://img.shields.io/badge/.NET-8.0%2B-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Python 3.7+](https://img.shields.io/badge/Python-3.7%2B-3776AB?style=flat&logo=python&logoColor=white)](https://python.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Versão de linha de comando (terminal / prompt) do **Logitech G HUB Widget**. Mostra em tempo real o status, nível de bateria, autonomia e consumo dos seus periféricos Logitech G diretamente no console.

---

## 🖥️ Prévia

```text
┌──────────────────────────────────────────────────────────────────────┐
│ ⚡  LOGITECH G HUB MONITOR                                v1.1.0-cli │
│ Status: ● Conectado (ws://127.0.0.1:9010)       Atualizado: 13:06:02 │
├──────────────────────────────────────────────────────────────────────┤
│ 🎧  G325 Gaming Headset  [○ INATIVO]                                 │
│     Bateria: [░░░░░░░░░░░░░░░░]  --%                                 │
│     Dispositivo inativo ou desligado  |  USB                         │
│                                                                      │
│ 🖱  G305 X SUPERLIGHT Wireless Mouse  [● ATIVO]                      │
│     Bateria: [██████████████░░]  85%                                 │
│     Autonomia: ~124h restantes  |  Consumo: 3.2 mW  |  WIRELESS      │
│                                                                      │
│ ⌨  G515 LIGHTSPEED TKL Gaming Keyboard  [● ATIVO]                   │
│     Bateria: [████████░░░░░░░░]  50%                                 │
│     Autonomia: ~22h restantes  |  Consumo: 33.1 mW  |  WIRELESS      │
├──────────────────────────────────────────────────────────────────────┤
│ [Ctrl+C] Sair   Modo: Atualização em Tempo Real                      │
└──────────────────────────────────────────────────────────────────────┘
```

> **ℹ️ Requisito:** o CLI lê os dados do agente local do **Logitech G HUB**, que existe apenas para **Windows e macOS**. No Linux, conecte-se a um G HUB em outra máquina (veja [Usando a partir de outra máquina](#-usando-a-partir-de-outra-máquina-linux)). Se você usa **Ubuntu com GNOME**, a [extensão para a barra superior](../gnome-extension/README.md) mostra a bateria direto do UPower, sem precisar de G HUB.

---

## 🚀 Como Executar

Há duas opções equivalentes: o **executável nativo** (não precisa instalar runtime) ou o **script Python** (não precisa de nenhuma biblioteca externa).

### Opção 1: Executável nativo (.NET single-file)

Baixe o arquivo da sua plataforma em **[Releases](https://github.com/Leosdc/logitech-ghub-widget/releases)**:

| Plataforma | Arquivo |
|---|---|
| Windows x64 | `ghub-cli-win-x64.zip` |
| macOS Apple Silicon (M1–M4) | `ghub-cli-osx-arm64.tar.gz` |
| macOS Intel | `ghub-cli-osx-x64.tar.gz` |
| Linux x64 | `ghub-cli-linux-x64.tar.gz` |

* **Windows:**
  ```powershell
  .\ghub-cli.exe
  ```
* **macOS / Linux:**
  ```bash
  tar -xzf ghub-cli-<plataforma>.tar.gz
  chmod +x ghub-cli
  ./ghub-cli
  ```

Se você compilou a partir do código-fonte, os binários ficam em `build/cli/<plataforma>/`.

### Opção 2: Script Python (zero dependências)

Funciona com Python 3.7+ usando apenas a biblioteca padrão:

```bash
# A partir da raiz do repositório
python3 ghub-cli.py

# No Windows, também pode ser
python ghub-cli.py
```

---

## 🎛️ Parâmetros

| Flag | Descrição |
|---|---|
| *(padrão)* | Abre o painel interativo atualizado em tempo real. |
| `--once` | Consulta uma única vez, imprime o resultado e encerra (útil em scripts e Tmux). |
| `--json` | Imprime a lista de periféricos em JSON e encerra. |
| `--compact` | Modo compacto, com uma linha resumida por periférico. |
| `--host <ip>` | Endereço do agente do G HUB (padrão: `127.0.0.1`). |
| `-p, --port <porta>` | Porta WebSocket do agente do G HUB (padrão: `9010`). |
| `-i, --interval <seg>` | Intervalo de atualização do painel, em segundos (padrão: `2`). |
| `--no-color` | Desativa as cores ANSI (ou defina a variável de ambiente `NO_COLOR=1`). |
| `-h, --help` | Mostra a ajuda com todas as opções. |

### Formato do `--json`

```json
[
  {
    "id": "dev00000001",
    "name": "G305 X SUPERLIGHT Wireless Mouse",
    "category": "Mouse",
    "type": "MOUSE",
    "connection": "WIRELESS",
    "active": true,
    "battery_percentage": 85,
    "is_charging": false,
    "mileage_hours": 124.0,
    "consumption_mw": 3.2,
    "status": "Ativo",
    "last_updated": "2026-10-26T13:06:02"
  }
]
```

---

## ⌨️ Atalhos no modo interativo

| Tecla | Script Python | Executável nativo |
|---|---|---|
| `Ctrl+C` | Sair | Sair |
| `Q` / `Esc` | — | Sair |
| `R` | — | Forçar atualização imediata |
| `C` | — | Alternar modo detalhado / compacto |
| `H` | — | Mostrar/ocultar barra de ajuda |

---

## 💡 Integrações com barras de status

Os exemplos abaixo usam o [`jq`](https://jqlang.github.io/jq/) para extrair dados do `--json`.

### Waybar (Linux)

O Waybar espera um objeto JSON com o campo `text`:

```json
"custom/logitech_mouse": {
    "exec": "ghub-cli --json | jq -c '{text: ((.[] | select(.category == \"Mouse\") | .battery_percentage | tostring) + \"%\")}'",
    "return-type": "json",
    "interval": 60
}
```

### Sketchybar (macOS)

```bash
sketchybar --set mouse label="$(ghub-cli --json | jq -r '.[] | select(.category == "Mouse") | "\(.battery_percentage)%"')"
```

---

## 🌐 Usando a partir de outra máquina (Linux)

O agente do G HUB escuta apenas em `127.0.0.1` e a conexão WebSocket **não é criptografada**. **Não exponha a porta 9010 na rede.** Para ler os dados de outra máquina, use um túnel SSH até o computador onde o G HUB está rodando:

```bash
# No Linux: encaminha a porta 9010 local para o 127.0.0.1:9010 da máquina com o G HUB
ssh -N -L 9010:127.0.0.1:9010 usuario@pc-com-ghub

# Em outro terminal
./ghub-cli
```

O computador com o G HUB precisa ter um servidor SSH ativo (no Windows: *Recurso opcional → Servidor OpenSSH*).

---

## 🩺 Solução de problemas

| Sintoma | Causa provável / solução |
|---|---|
| `Falha ao conectar` | O G HUB não está aberto, ou o agente (`lghub_agent`) não está rodando. Abra o G HUB e tente novamente. |
| Todos os dispositivos aparecem como `INATIVO` | Os periféricos sem fio estão desligados ou em repouso. Mexa o mouse ou aperte uma tecla para acordá-los. |
| macOS: "não é possível abrir porque o desenvolvedor não pode ser verificado" ou o processo é encerrado com `killed` | O binário não é assinado. Rode `xattr -d com.apple.quarantine ghub-cli` e `codesign --sign - --force ghub-cli`. |
| Windows: aviso do SmartScreen | O executável não tem assinatura digital. Confira o hash em `SHA256SUMS.txt` e clique em "Mais informações → Executar assim mesmo". |
| Caracteres estranhos ou sem cores | Use um terminal com suporte a UTF-8 e ANSI (Windows Terminal, terminal do VS Code, iTerm2, GNOME Terminal) ou rode com `--no-color`. |

---

## 🛠️ Como recompilar os binários

* **Windows:** execute `cli/build-cross-platform.bat` ou
  ```powershell
  pwsh ./cli/build-cross-platform.ps1
  ```
* **Qualquer SO com o .NET SDK 8.0+:**
  ```bash
  dotnet publish cli/GHubCli.csproj -c Release -r <RID> --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
  ```
  *(`<RID>` pode ser `win-x64`, `linux-x64`, `osx-x64` ou `osx-arm64`)*
