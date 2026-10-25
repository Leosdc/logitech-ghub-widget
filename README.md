# ⚡ Logitech G HUB Battery Widget (Windows 11)

[![Windows 11](https://img.shields.io/badge/Windows-11%20Fluent-0078D4?style=flat&logo=windows11&logoColor=white)](https://github.com/Leosdc/logitech-ghub-widget)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Non-Destructive](https://img.shields.io/badge/Logitech%20G%20HUB-100%25%20Untouched-10B981?style=flat)](https://github.com/Leosdc/logitech-ghub-widget)

Um widget moderno com interface nativa **Windows 11 Fluent 2.0 (WinUI 3)** para monitorar em tempo real o nível de bateria, status de carregamento, autonomia estimada e consumo de energia dos seus periféricos Logitech G (Mouse, Teclado, Headset, etc.).

---

## 🔒 Segurança e Integridade (100% Não-Destrutivo)

- **A pasta oficial do Logitech G HUB (`C:\Program Files\LGHUB`) NÃO é modificada.**
- Nenhum arquivo executável, biblioteca `.dll`, driver ou recurso oficial é alterado ou substituído.
- O widget atua exclusivamente como um cliente local seguro conectado à porta WebSocket oficial do agente do G HUB (`ws://127.0.0.1:9010`).
- Atualizações oficiais do Logitech G HUB continuam funcionando normalmente.
- O widget é instalado em `%LOCALAPPDATA%\Programs\LogitechGHubWidget`, sem requerer privilégios administrativos e sem deixar resíduos no sistema.

---

## ✨ Recursos

- 🎧 **Detecção Automática:**
  - Identifica e categoriza automaticamente seus periféricos:
    - **Mouses** (ex: G305 X SUPERLIGHT, G Pro X, etc.)
    - **Teclados** (ex: G515 LIGHTSPEED TKL, G915, etc.)
    - **Headsets / Fones** (ex: G325, G733, G PRO X, etc.)
- 🔋 **Monitoramento Preciso:**
  - Porcentagem exata em tempo real (0% a 100%).
  - Horas restantes estimadas de bateria (ex: `~130h de duração estimada`).
  - Monitoramento de consumo de energia em miliwatts (`mW`).
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

## 🚀 Instalação Rápida (1 Clique)

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

---

## 🔍 Verificação de Integridade (SHA-256)

Para garantir que os binários que você baixou correspondem exatamente aos compilados a partir do código aberto deste repositório (sem alterações ou adulterações), você pode verificar os hashes oficiais:

```powershell
Get-FileHash Logitech-GHub-Widget-Setup.zip -Algorithm SHA256
```

| Arquivo | Algoritmo | Hash SHA-256 |
|---|---|---|
| `LogitechGHubWidget.exe` | SHA-256 | `B834F76FA07683C282D8566E5F9F2304A46A4B3537DB8327C4F8801F735DBABB` |
| `LogitechGHubWidget.dll` | SHA-256 | `C41EFF2D6D4C10B7C10D5A8252B20826D1A2A0FC63511B0B7195D2191E0EF9D5` |

*Os pacotes `.zip` de release e os arquivos `SHA256SUMS.txt` são construídos de forma reproduzível e transparente via **GitHub Actions** em cada release.*

---

## 🛠️ Compilação a partir do Código-Fonte

Caso queira compilar o projeto você mesmo:

### Pré-requisitos:
- Windows 10/11 (x64)
- [.NET SDK 10.0](https://dotnet.microsoft.com/) ou compatível

### Comandos:
```bash
# Restaurar dependências e compilar
dotnet build src/GHubWidget.csproj -c Release

# Publicar binário otimizado
dotnet publish src/GHubWidget.csproj -c Release -o build/widget
```

---

## 📄 Licença

Distribuído sob a licença **MIT**. Consulte o arquivo [LICENSE](LICENSE) para obter mais informações.
