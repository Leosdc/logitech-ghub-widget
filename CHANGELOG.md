# 📋 Registro de Alterações (Changelog)

Todas as alterações relevantes neste projeto serão documentadas neste arquivo.

O formato segue o padrão [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e adota o [Versionamento Semântico](https://semver.org/lang/pt-BR/).

---

## [1.3.3] - 2026-11-04

### 🎮 Ocultamento Automático em Jogos e Aplicativos em Tela Cheia (Fullscreen)
- **Detecção Precisa de Tela Cheia:**
  - Implementada detecção inteligente via Win32 (`GetForegroundWindow`, `WindowFromPoint` e validação geométrica de tela): o widget identifica instantaneamente quando qualquer jogo ou aplicativo entra em modo tela cheia (exclusivo, sem bordas/borderless ou vídeos F11).
  - O widget oculta sua faixa da barra de tarefas de forma limpa (`Visibility = Hidden`), sem sobrepor HUDs, miras ou elementos visuais do jogo.
- **Suporte Multi-Monitor e Segundo Plano:**
  - Valida o monitor ativo do jogo e do widget. Se o usuário estiver interagindo com um monitor secundário enquanto o jogo ocupa o monitor principal, o widget permanece corretamente oculto sob o jogo.
- **Restauração Imediata ao Sair do Jogo:**
  - Ao alternar janelas (Alt+Tab), pressionar a tecla Windows ou fechar o jogo, o widget detecta a desocupação da tela e reancora suavemente na barra de tarefas em 0ms.
- **Filtro Estrito de Classes do Shell:**
  - Janelas nativas do Windows como Área de Trabalho (`Progman`, `WorkerW`), Menu Iniciar (`CoreWindow`), Barra de Tarefas (`Shell_TrayWnd`) e Central de Notificações são categorizadas de forma imune, garantindo que o widget jamais pisque ou suma em uso normal da área de trabalho.

---

## [1.3.2] - 2026-11-04

### ⚡ Estabilidade da Barra de Tarefas & Z-Order Permanente
- **Persistência Total sobre a Barra de Tarefas (Zero Disappear on Click):**
  - Implementado gancho nativo Win32 (`SetWinEventHook` com `EVENT_SYSTEM_FOREGROUND`) e listener para o evento `Deactivated`: o widget detecta em 0 milissegundos cliques na área livre da barra de tarefas (`Shell_TrayWnd`) ou trocas de janela, re-ancorando sua camada Z-order (`HWND_TOPMOST` com `SWP_NOACTIVATE`) instantaneamente sem roubar foco.
  - Eliminação completa de falsos-positivos de tela cheia que causavam ocultamento indevido ao focar na Área de Trabalho (classe `WorkerW`) ou em navegadores/editores de código.
  - Atualização in-place (`_pillMap`): variações percentuais de bateria atualizam propriedades diretamente nos elementos WPF existentes, prevenindo cintilações (flicker) e redesenhos forçados de superfície DWM.

### 🎯 Interação Fluida por Clique
- **1 Clique Simples (Botão Esquerdo):** Alterna o pop-up Fluent completo (Flyout) diretamente acima do ícone na barra de tarefas, replicando o comportamento dos controles nativos de som, rede e calendário do Windows 11.
- **Duplo Clique:** Abre o painel principal de configurações.
- **Botão Direito:** Menu de contexto com atalhos para abrir o Logitech G HUB oficial, forçar atualização imediata e acessar configurações.

### 🔔 Bandeja do Sistema (System Tray) Limpa
- **Ícone Principal Preservado:** O ícone unificado do Logitech G HUB Widget permanece sempre acessível na bandeja (`^`), permitindo atalhos rápidos e resumo consolidado via tooltip.
- **Prevenção de Duplicidades:** Ícones numéricos individuais na bandeja ficam desativados por padrão no modo barra de tarefas, mantendo o menu suspenso limpo e minimalista.

### 🖥️ Múltiplos Monitores
- Abertura de janelas de configuração posicionadas de forma inteligente na tela ativa do ponteiro do mouse, evitando que o painel surja fora do campo de visão do usuário.

---

## [1.3.1] - 2026-11-03

### ⚡ Redesenho Completo dos Ícones na Barra de Tarefas (System Tray)
- **Escala e Peso Visual Nativos do Windows 11:**
  - Ícones agora ocupam 100% da área útil (15-16px na grade nativa de 16x16 / 20x20 / 32x32 com `SM_CXSMICON`), correspondendo com exatidão à escala e visibilidade dos ícones nativos do sistema (como Rede Ethernet e Alto-Falante).
  - Eliminação de qualquer downsampling bilinear do Windows que causava embaçamento em ícones pequenos.
- **Exibição dos 3 Dispositivos Individuais:**
  - Todos os periféricos Logitech (Mouse, Teclado e Headset) possuem seus próprios ícones dedicados e independentes na barra de tarefas.
  - Dispositivos em modo de repouso ou desligados permanecem visíveis com glifo atenuado e rótulo `"OFF"`, mantendo a consistência dos 3 periféricos.
- **Tipografia e Indicadores de Alto Contraste:**
  - Dígitos de bateria ampliados e em negrito nítido (1-bit pixel fit), com cores vibrantes adaptativas (verde, amarelo, vermelho e ciano para carregamento com `⚡`).
  - Suporte completo a 100% com alinhamento perfeito de 3 dígitos (`100`).
- **Pop-up Fluent 2.0 Completo ao Passar o Mouse:**
  - Passar o mouse sobre qualquer um dos ícones abre instantaneamente o pop-up com dados aprofundados de todos os periféricos: porcentagem, status de recarga, consumo e autonomia.

---

## [1.3.0] - 2026-11-03

### ✨ Adicionado
- **Faixa de Periféricos Direto na Barra de Tarefas (Taskbar Strip - Estilo Ubuntu / GNOME Top Bar):**
  - **Exibição Direta na Barra de Tarefas:** Widget horizontal nativo acoplado diretamente na superfície da barra de tarefas (à esquerda da área de notificação/seta `^`), **sem que o usuário precise desocultar ícones do menu overflow**.
  - **Pílulas Elegantes no Estilo Fluent 2.0:** Cada periférico ativo ganha uma pílula horizontal (`[ 🖱️ 61% ]  [ ⌨️ 99% ]`) com glifo vetorial nítido, tipografia cristalina em tamanho legível (Segoe UI Variable 12px), indicador de carregamento `⚡` e cores adaptativas por faixa de bateria (verde >50%, amarelo 20-50%, vermelho <=20%, ciano carregando).
  - **Rastreamento Automático da Barra:** Posicionamento dinâmico que acompanha redimensionamentos, movimentações da bandeja de notificações e auto-ocultação inteligente ao jogar em tela cheia (fullscreen) ou reproduzir vídeos.
  - **Pop-up / Flyout Moderno ao Passar o Mouse (Hover):**
    - Ao passar o cursor sobre as pílulas na barra, surge suavemente o pop-up completo no padrão Windows 11 Fluent 2.0 (WinUI 3).
    - Exibe todos os dados: porcentagem exata, status de carregamento, consumo em tempo real em miliwatts (`mW`), estimativa de autonomia em horas (`~Xh restantes`) e barra de progresso individual colorida.
    - **Botão Fixar (Pin 📌):** Permite manter o pop-up aberto para consultas prolongadas.
    - **Ações Rápidas integradas:** Atalhos para abrir o Logitech G HUB oficial, forçar atualização imediata, alternar modos de exibição e abrir Configurações.
    - **Debounce inteligente:** Tolerância de 360ms na saída do ponteiro para movimentação fluida do mouse entre a barra e o pop-up, além de fechamento automático com `Esc` ou clique fora.
  - **Seletor de 3 Modos de Exibição nas Preferências:**
    - **🚀 Faixa na Barra de Tarefas (Padrão - Direto na Barra):** Acesso imediato no estilo Ubuntu.
    - **🔔 Ícones na Área de Notificação (Bandeja / Tray):** Ícones individuais na bandeja do Windows ao lado do relógio.
    - **🖥️ Janela Flutuante na Área de Trabalho:** Telinha clássica com visual Fluent 2.0 na área de trabalho.

### ⚡ Aprimorado
- **Melhoria Drástica na Legibilidade dos Ícones da Bandeja (System Tray):**
  - Aumentada substancialmente a proporção do número da bateria nos ícones da bandeja, com fonte em negrito e contorno de alto contraste (shadow/halo) de 1px.
  - Elimina o problema de dígitos minúsculos ou ilegíveis em barras de tarefas escuras, claras ou translúcidas.
- **Detecção Resiliente de Periféricos sem Fio:**
  - Consulta do estado de bateria estendida para todos os periféricos enumerados na inicialização do G HUB, garantindo leitura instantânea mesmo em dispositivos sem fio que inicializam em estado de repouso (Lightspeed sleep mode).
- **Gerenciamento de Ciclo de Vida em Segundo Plano:**
  - `ShutdownMode="OnExplicitShutdown"` no WPF com controle robusto de alocação de HICONs nativos via `DestroyIcon`, prevenindo memory leaks e fechamentos inadvertidos.

---

## [1.2.0] - 2026-10-28

### ✨ Adicionado
- **Extensão para GNOME Shell 46 / Ubuntu 24.04 (`gnome-extension/`)** — desenvolvida e contribuída por **[@vinipy12](https://github.com/vinipy12)** (*Vinicius Cavalcante*, [PR #2](https://github.com/Leosdc/logitech-ghub-widget/pull/2)): interface nativa para Linux, onde o agente do G HUB não existe.
  - **Fonte de dados UPower via D-Bus:** lê os periféricos Logitech expostos pelo kernel (`hid-logitech-hidpp`, receptor Lightspeed) e pelo BlueZ (Bluetooth), sem G HUB, sem processo auxiliar e sem polling (GJS puro, ESM).
  - **Um item por periférico na barra superior**, ordenados por tipo (mouse, teclado, headset, controle). Porcentagem exata quando o UPower confia no valor; nível (`Cheia`, `Alta`, `Normal`, `Baixa`, `Crítica`) para dispositivos que só informam faixas (ex.: G305 pelo receptor). Prefixo `⚡` enquanto carrega.
  - **Cores de alerta:** âmbar em 50% ou menos, vermelho em 20% ou menos. Dispositivos dormindo/desconectados saem da barra e ficam em cinza no menu como *Inativo*.
  - **Notificação de bateria fraca:** uma por dispositivo a cada vez que cruza 20% ou menos, rearmada ao recuperar; o estado sobrevive a bloqueio/desbloqueio de tela.
  - **Preferências** (`notify-low`, `show-inactive`) em janela Adwaita.
  - **Cliente D-Bus robusto:** assina os sinais antes de enumerar e usa contador de geração + tokens por caminho, de modo que uma resposta atrasada não ressuscita um dispositivo removido; snapshots autoritativos para sobreviver a reinícios do UPower. Coberto por testes puros (`make test`), teste de corrida determinístico e teste contra o UPower real (`make test-live`).
  - **Empacotamento e release:** `make pack` gera `ghub-battery@vinipy12.github.io.shell-extension.zip`; novo job `pack-extension` no GitHub Actions roda os testes, gera o zip e o inclui no `SHA256SUMS.txt` e nos assets da release.
  - **Documentação:** `gnome-extension/README.md` (instalação, preferências, limitações, solução de problemas), seção "🐧 Ubuntu / GNOME" no README raiz e seção 6 em `SECURITY.md`.

---

## [1.1.2] - 2026-10-27

### ✨ Adicionado & Aprimorado
- **Troca Dinâmica de Tema do Windows em Tempo Real:**
  - Implementado hook no `WndProc` para a mensagem nativa `WM_SETTINGCHANGE` (0x001A) e listener para `SystemEvents.UserPreferenceChanged`.
  - Ao alternar entre modo Claro e Escuro nas Configurações do Windows 11, o widget se adapta instantaneamente em tempo real no modo "Sistema", sem necessidade de reiniciar o aplicativo.
  - A detecção de tema do sistema (`IsSystemDarkTheme()`) agora avalia conjuntamente `SystemUsesLightTheme` (modo do Windows/barra de tarefas) e `AppsUseLightTheme` (modo dos aplicativos), garantindo compatibilidade total com o modo Personalizado do Windows 11.
- **Melhorias de Contraste e Acessibilidade no Modo Claro:**
  - Pílulas selecionadas e switches de alternância com cores de alto contraste dinâmico para garantir perfeita legibilidade sobre fundos claros.
  - Ícone de status e badges ajustados com paleta Fluent 2.0 para temas claros.

### 🐛 Corrigido
- **Corte de Textos na Interface:**
  - Largura da janela ajustada de 460px para 490px.
  - Reestruturação em grid responsivo de 2 linhas nos cards "Tema de Aparência" e "Intervalo de Atualização", impedindo que os botões espremessem os títulos.
  - Habilitada quebra automática de linha (`TextWrapping="Wrap"`) em todos os textos explicativos e subtítulos, eliminando truncamentos em telas de qualquer resolução ou escala de DPI.

---

## [1.1.1] - 2026-10-27

### 🐛 Corrigido
- **Instalador no Windows PowerShell 5.1:** corrigida falha em `Instalar-Widget.ps1` e `Instalar-GUI.ps1` onde o cmdlet `Get-ChildItem -Exclude "*.pdb"` retornava 0 itens no PowerShell padrão do Windows, impedindo a cópia dos binários para `%LOCALAPPDATA%\Programs\LogitechGHubWidget` e gerando erro `Start-Process: O sistema não pode encontrar o arquivo especificado` (Issue #1).
- **Detecção Resiliente dos Binários:** busca em múltiplos diretórios candidatos (`build/widget/`, raiz, diretório do script) para localizar `LogitechGHubWidget.exe`, tornando o instalador compatível tanto com o repositório clonado quanto com o pacote ZIP descompactado (com subpastas ou extração na raiz).
- **Validação de Integridade na Instalação:** asserções explícitas antes de criar atalhos e antes de iniciar o aplicativo, emitindo mensagens de erro claras caso arquivos estejam ausentes antes de registrar no sistema.
- **Empacotamento ZIP (`Criar-Pacote-Zip.bat`):** empacotamento com pasta de staging preservando a estrutura de pastas (`build/widget/`, `installer/`) e mantendo scripts executáveis e binários na raiz, garantindo funcionamento imediato pós-download em qualquer formato de extração.
- **Desinstalador Portável:** substituído caminho absoluto por `%LOCALAPPDATA%\Programs\LogitechGHubWidget` dinâmico em `Desinstalar-Widget.bat`.

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
