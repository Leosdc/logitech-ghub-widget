# 📋 Registro de Alterações (Changelog)

Todas as alterações relevantes neste projeto serão documentadas neste arquivo.

O formato segue o padrão [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e adota o [Versionamento Semântico](https://semver.org/lang/pt-BR/).

---

## [Unreleased]

### ✨ Adicionado
- Extensão para GNOME Shell 46 (`gnome-extension/`): bateria dos periféricos na barra superior via UPower, cores âmbar/vermelha, notificação 20% ou menos, preferências.

---

## [1.1.0] - 2026-10-26

### ✨ Adicionado
- **Logitech G HUB CLI Monitor (Multiplataforma):**
  - **Script Universal em Python (`ghub-cli.py`):** zero dependências externas, protocolo RFC 6455 puro via biblioteca padrão `socket`, compatível com Python 3.7+ em Windows, Linux e macOS.
  - **Executáveis Nativos Independentes (.NET 8.0 Single-File):** binários autossuficientes sem necessidade de instalar runtime em `build/cli/` para:
    - Windows x64 (`ghub-cli.exe`)
    - Linux x64 (`ghub-cli`)
    - macOS Apple Silicon M1–M4 (`osx-arm64`)
    - macOS Intel (`osx-x64`)
  - **Telinha de Prompt Interativa (TUI):**
    - Monitoramento em tempo real com taxa de atualização contínua e sem cintilação (flicker-free).
    - Barras gráficas de nível de bateria coloridas por faixa de carga (verde >50%, amarelo 20-50%, vermelho <=20%, ciano carregando).
    - Estimativa de autonomia restante em horas (`~124h restantes`) e consumo elétrico em tempo real (`3.2 mW`).
  - **Modos de Execução:**
    - Flag `--once`: consulta única imediata, imprime a tabela e finaliza.
    - Flag `--json`: saída estruturada em JSON para integração com barras de status (**Waybar**, **Sketchybar**, scripts e automações).
    - Flag `--compact`: visualização enxuta com 1 linha por periférico.
    - Flags `--host` e `-p, --port`: conexão com instâncias do G HUB em outros computadores na rede local (via túnel SSH).
    - Flag `--no-color` e variável de ambiente `NO_COLOR`: suporte a consoles sem cores ANSI.
  - **Atalhos no Modo Interativo:**
    - `Q` / `Esc` / `Ctrl+C`: sair do monitor.
    - `R`: forçar atualização imediata.
    - `C`: alternar entre modo detalhado e compacto.
    - `H`: alternar exibição da barra de ajuda.
- **Melhorias no Widget (Windows 11):**
  - Assinatura em tempo real aos eventos `/devices/state/changed` e `/battery/state/changed` do agente do G HUB.
  - Detecção imediata de sono e desconexão de dispositivos sem fio: periféricos desligados passam a exibir o badge `[Inativo]` em vez de congelar valores antigos.
  - Rodapé dinâmico com contagem de dispositivos ativos vs inativos (ex: `2 de 3 dispositivo(s) ativo(s)`).
  - Tooltip da bandeja do sistema (System Tray) atualizado dinamicamente com o status de cada dispositivo ativo e aviso de inatividade.
  - Monitoramento do consumo em miliwatts (`mW`).
- **Segurança e Governança:**
  - Criado [`SECURITY.md`](SECURITY.md) detalhando a arquitetura 100% não-destrutiva, política de zero telemetria, permissões restritas e diretrizes para reporte responsável de vulnerabilidades.
  - Atualizado o fluxo de CI/CD em [`.github/workflows/build-and-release.yml`](.github/workflows/build-and-release.yml) para compilar, empacotar e calcular hashes SHA-256 tanto do Widget quanto dos binários do CLI para todos os sistemas operacionais.

### 🐛 Corrigido
- **Alinhamento do Quadro TUI no Terminal:**
  - Substituído o cálculo empírico de largura de caracteres por posicionamento absoluto de colunas via sequências ANSI (`\033[<n>G` / `CHA`).
  - Resolvida a divergência de largura do emoji de mouse (`🖱`, U+1F5B1) entre diferentes emuladores de terminal (Windows Terminal, ConPTY, xterm.js no VS Code).
  - Eliminado o acúmulo de caracteres residuais de frames anteriores (limpeza explícita com `\033[2K` e espaçamento estático garantido entre ícone e texto).

---

## [1.0.0] - 2026-10-25

### ✨ Lançamento Inicial
- Interface nativa para Windows 11 com design Fluent 2.0 (WinUI 3).
- Comunicação local e segura com o agente oficial do Logitech G HUB (`ws://127.0.0.1:9010`).
- Suporte a temas Claro, Escuro e Sincronização automática com o Windows.
- Modos de fixação: Sempre no Topo (`🔝`), Na Área de Trabalho (`📌`) e Minimizado na Bandeja do Sistema (Tray).
- Notificações nativas do Windows para alerta de bateria crítica (< 20%).
- Controle de opacidade e taxa de atualização configuráveis.
- Scripts de instalação (`Instalar-Widget.bat`, `Instalar-Com-Interface.bat`) e desinstalação limpa (`Desinstalar-Widget.bat`).
- Verificação de integridade via hashes SHA-256.
