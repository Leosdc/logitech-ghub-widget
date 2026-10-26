# ⚡ Logitech G HUB Battery Widget (Windows 11 + CLI multiplataforma)

[![Windows 11](https://img.shields.io/badge/Windows-11%20Fluent-0078D4?style=flat&logo=windows11&logoColor=white)](https://github.com/Leosdc/logitech-ghub-widget)
[![CLI](https://img.shields.io/badge/CLI-Windows%20%7C%20macOS%20%7C%20Linux-333333?style=flat&logo=gnubash&logoColor=white)](cli/README.md)
[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%208.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Python 3.7+](https://img.shields.io/badge/Python-3.7%2B-3776AB?style=flat&logo=python&logoColor=white)](https://python.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Non-Destructive](https://img.shields.io/badge/Logitech%20G%20HUB-100%25%20Untouched-10B981?style=flat)](https://github.com/Leosdc/logitech-ghub-widget)

Um widget moderno com interface nativa **Windows 11 Fluent 2.0 (WinUI 3)** para monitorar em tempo real o nível de bateria, status de carregamento, autonomia estimada e consumo de energia dos seus periféricos Logitech G (Mouse, Teclado, Headset, etc.).

A partir da **v1.1.0**, também há uma **versão de terminal (CLI)** que mostra os mesmos dados em uma tela de prompt atualizada em tempo real. Ela funciona no Windows, macOS e Linux.

📋 [Changelog](CHANGELOG.md) · 🔒 [Política de Segurança](SECURITY.md) · 💻 [Documentação do CLI](cli/README.md)

---

## 🔒 Segurança e Integridade (100% Não-Destrutivo)

- **A pasta oficial do Logitech G HUB (`C:\Program Files\LGHUB`) NÃO é modificada.**
- Nenhum arquivo executável, biblioteca `.dll`, driver ou recurso oficial é alterado ou substituído.
- O widget e o CLI atuam exclusivamente como clientes locais da porta WebSocket oficial do agente do G HUB (`ws://127.0.0.1:9010`), enviando **apenas consultas de leitura** (lista de dispositivos e estado da bateria).
- **Zero telemetria:** nenhum dado sai do seu computador.
- Atualizações oficiais do Logitech G HUB continuam funcionando normalmente.
- O widget é instalado em `%LOCALAPPDATA%\Programs\LogitechGHubWidget`, sem requerer privilégios administrativos e sem deixar resíduos no sistema.

Detalhes completos do modelo de segurança e como reportar vulnerabilidades: **[SECURITY.md](SECURITY.md)**.

---

## ✨ Recursos

- 🎧 **Detecção Automática:**
  - Identifica e categoriza automaticamente seus periféricos:
    - **Mouses** (ex: G305 X SUPERLIGHT, G Pro X, etc.)
    - **Teclados** (ex: G515 LIGHTSPEED TKL, G915, etc.)
    - **Headsets / Fones** (ex: G325, G733, G PRO X, etc.)
  - Distingue dispositivos **ativos** de **desligados/desconectados** (ex: mouse sem fio desligado aparece como "Inativo").
- 🔋 **Monitoramento Preciso:**
  - Porcentagem exata em tempo real (0% a 100%).
  - Horas restantes estimadas de bateria (ex: `~130h de duração estimada`).
  - Monitoramento de consumo de energia em miliwatts (`mW`).
  - Atualização por eventos do G HUB (`/devices/state/changed` e `/battery/state/changed`), sem esperar o próximo ciclo.
  - Cores dinâmicas de alto contraste:
    - 🟢 **Verde** (> 50%)
    - 🟡 **Âmbar** (20% a 50%)
    - 🔴 **Vermelho** (<= 20% - Nível crítico)
    - 🔵 **Ciano / Azul** (Carregando ⚡)
- 🌓 **Suporte a Temas (Claro e Escuro):**
  - **🌗 Seguir o Windows (Padrão):** Detecta e sincroniza automaticamente com o modo Claro ou Escuro do Windows.
  - **☀️ Modo Claro:** Fundo claro com contraste máximo e tipografia nítida.
  - **🌙 Modo Escuro:** Estilo Fluent Dark autêntico com superfícies WinUI 3.
- 📌 **Modos de Exibição:**
  - **Sempre no Topo (`🔝`):** O widget flutua sobre jogos e outras janelas.
  - **Fixado na Área de Trabalho (`📌`):** Comporta-se como um widget no papel de parede, atrás de navegadores e janelas.
  - **Bandeja do Sistema (System Tray):** Minimiza discretamente ao lado do relógio do Windows com tooltip de status.
- ⚡ **Alertas de Bateria Fraca:**
  - Notificação na Central de Notificações do Windows quando a bateria cair abaixo de 20%.
- 🎛️ **Configurações Rápidas:**
  - Ajuste de opacidade da janela (40% a 100%).
  - Inicialização automática com o Windows.
  - Frequência de atualização configurável (2s, 5s, 15s ou 30s).
  - Atalho rápido para abrir o Logitech G HUB oficial.

---

## 🚀 Instalação Rápida do Widget (1 Clique)

1. Baixe o pacote em **[Releases](https://github.com/Leosdc/logitech-ghub-widget/releases)** ou baixe o arquivo **[Logitech-GHub-Widget-Setup.zip](https://github.com/Leosdc/logitech-ghub-widget/raw/main/Logitech-GHub-Widget-Setup.zip)** ou clone este repositório:
   ```bash
   git clone https://github.com/Leosdc/logitech-ghub-widget.git
   ```
2. Abra a pasta e execute:
   - **`Instalar-Com-Interface.bat`** (Assistente gráfico com escolha de pasta e opções)
   - ou **`Instalar-Widget.bat`** (Instalação rápida direta via terminal)
3. Pronto! O atalho será criado na sua Área de Trabalho e no Menu Iniciar.

> **💡 Nota sobre os inicializadores `.bat` (`-ExecutionPolicy Bypass`):**
> O parâmetro `-ExecutionPolicy Bypass` presente nos arquivos `.bat` é utilizado de forma estritamente temporária para aquela execução específica, permitindo que o PowerShell execute o script de instalação mesmo em máquinas onde a política padrão do Windows bloqueie scripts `.ps1` locais não assinados. Ele **não** altera a política global do seu computador e **não** solicita privilégios de Administrador.

---

## 💻 Versão de Terminal / CLI (Windows, macOS, Linux)

O **Logitech G HUB CLI Monitor** mostra os seus periféricos numa tela de terminal atualizada em tempo real, com barras de bateria, autonomia e consumo:

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

> **ℹ️ Onde o G HUB roda:** o Logitech G HUB existe para **Windows e macOS**. No Linux não há G HUB, então o CLI precisa se conectar a um G HUB rodando em outra máquina. A forma segura de fazer isso é um túnel SSH (veja [cli/README.md](cli/README.md#-usando-a-partir-de-outra-máquina-linux)).

### Como executar

1. **Via Python (universal, sem dependências — apenas biblioteca padrão):**
   ```bash
   python3 ghub-cli.py
   ```
2. **Via executável nativo (.NET single-file, não precisa instalar runtime):**
   baixe o arquivo da sua plataforma em **[Releases](https://github.com/Leosdc/logitech-ghub-widget/releases)**:

   | Plataforma | Arquivo da release |
   |---|---|
   | Windows x64 | `ghub-cli-win-x64.zip` |
   | macOS Apple Silicon (M1–M4) | `ghub-cli-osx-arm64.tar.gz` |
   | macOS Intel | `ghub-cli-osx-x64.tar.gz` |
   | Linux x64 | `ghub-cli-linux-x64.tar.gz` |

   ```bash
   # macOS / Linux
   tar -xzf ghub-cli-<plataforma>.tar.gz
   chmod +x ghub-cli
   ./ghub-cli
   ```
   ```powershell
   # Windows
   .\ghub-cli.exe
   ```
3. **Modos especiais:**
   * `--once`: consulta única, imprime o resultado e encerra.
   * `--json`: exporta JSON para scripts e barras de status (**Waybar**, **Sketchybar**).
   * `--compact`: uma linha resumida por periférico.
   * `--host` / `--port`: endereço do agente do G HUB (padrão `127.0.0.1:9010`).

👉 Documentação completa (flags, atalhos, integrações, solução de problemas): **[cli/README.md](cli/README.md)**.

---

## 🗑️ Como Desinstalar

Para remover o aplicativo de forma 100% limpa:
- Execute **`Desinstalar-Widget.bat`** (ou o `Desinstalar.bat` criado na pasta escolhida de instalação).
- O desinstalador:
  - Encerra o processo do widget se estiver ativo.
  - Remove os atalhos da Área de Trabalho e do Menu Iniciar.
  - Remove a entrada de autostart em `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
  - Remove todos os arquivos do programa em `%LOCALAPPDATA%\Programs\LogitechGHubWidget` (ou na pasta personalizada selecionada).
  - Remove as configurações salvas em `%LOCALAPPDATA%\LogitechGHubWidget`.
- **Zero resíduos no sistema.**

O CLI não instala nada nem grava arquivos: basta apagar o executável ou o script.

---

## 🔍 Verificação de Integridade (SHA-256)

Cada release publica um arquivo **`SHA256SUMS.txt`** com os hashes de todos os arquivos (pacote do widget, binários do CLI e `ghub-cli.py`), gerado automaticamente pelo **GitHub Actions** a partir deste código-fonte.

```powershell
# Windows (PowerShell)
Get-FileHash Logitech-GHub-Widget-Setup.zip -Algorithm SHA256
```
```bash
# macOS / Linux (na pasta com os arquivos baixados)
shasum -a 256 -c SHA256SUMS.txt --ignore-missing
```

Compare o resultado com a linha correspondente em `SHA256SUMS.txt`. Os hashes mudam a cada versão, então use sempre o arquivo da **mesma release** que você baixou.

---

## 🛠️ Compilação a partir do Código-Fonte

### Pré-requisitos
- Widget: Windows 10/11 (x64) e [.NET SDK 10.0](https://dotnet.microsoft.com/)
- CLI: .NET SDK 8.0 ou superior (qualquer SO) — ou apenas Python 3.7+ para o script

### Comandos
```bash
# Widget (Windows)
dotnet build src/GHubWidget.csproj -c Release
dotnet publish src/GHubWidget.csproj -c Release -o build/widget

# CLI para todas as plataformas (gera build/cli/<plataforma>/)
pwsh ./cli/build-cross-platform.ps1
```

---

## 📄 Licença

Distribuído sob a licença **MIT**. Consulte o arquivo [LICENSE](LICENSE) para obter mais informações.
