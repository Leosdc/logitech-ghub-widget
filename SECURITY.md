# 🔒 Política de Segurança (Security Policy)

Este documento descreve o modelo de segurança, arquitetura de integridade e diretrizes para reporte de vulnerabilidades do **Logitech G HUB Battery Widget**, do **Logitech G HUB CLI Monitor** e da **extensão Logitech G Battery para GNOME Shell**.

---

## 🛡️ Versões Suportadas

Recebem atualizações de segurança e correções:

| Versão | Suporte | Notas |
|:---|:---:|:---|
| **v1.1.x** | ✅ Suportado | Versão atual (Widget Fluent WinUI 3 + CLI multiplataforma + extensão GNOME Shell 46). |
| **v1.0.x** | ⚠️ Parcial | Apenas patches críticos de segurança. Recomenda-se migrar para v1.1.x. |
| < v1.0.0 | ❌ Descontinuado | Versões de desenvolvimento pré-lançamento. |

---

## 🏛️ Modelo e Arquitetura de Segurança

O projeto foi projetado desde a sua concepção com o princípio de **mínimo privilégio** e **integridade 100% não-destrutiva**:

### 1. Integridade Absoluta do Logitech G HUB Oficial
- **A pasta oficial do Logitech G HUB (`C:\Program Files\LGHUB`) NUNCA é modificada.**
- Nenhum arquivo executável oficial (`lghub.exe`, `lghub_agent.exe`, etc.), biblioteca (`.dll`), driver de dispositivo ou recurso nativo é alterado, substituído ou injetado.
- Não há injeção de DLL (DLL injection), hooking de APIs do sistema operacional ou leitura direta de memória de processos de terceiros.
- Atualizações oficiais da Logitech continuam funcionando sem conflito.

### 2. Comunicação Segura e Estritamente Local
- O aplicativo conecta-se à porta WebSocket local aberta pelo próprio serviço oficial do G HUB:
  ```text
  ws://127.0.0.1:9010
  ```
- **Apenas operações de leitura (Read-Only):**
  - O widget envia comandos `GET` e `SUBSCRIBE` exclusivamente para as rotas `/devices/list`, `/battery/{id}/state`, `/devices/state/changed` e `/battery/state/changed`.
  - Nenhuma alteração de DPI, macros, iluminação RGB, perfil de jogos ou gravação de memória flash do periférico é realizada.
- **Zero telemetria:**
  - O código não contém rastreadores, métricas, telemetria, Google Analytics, endpoints de log externos ou envio de relatórios de erro.
  - Todas as conexões de rede externas são inexistentes. O aplicativo opera 100% offline.

### 3. Permissões de Usuário e Isolamento (Sem Privilégios de Administrador)
- Widget, CLI e extensão GNOME executam no contexto do usuário padrão (`Medium Mandatory Level`), sem solicitar elevação administrativa do UAC (`Run as Administrator`).
- A instalação do widget reside inteiramente em:
  ```text
  %LOCALAPPDATA%\Programs\LogitechGHubWidget
  ```
- A inicialização automática opcional é configurada no escopo do usuário atual (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`), sem tocar em chaves globais da máquina (`HKLM`).
- A desinstalação é completa e remove 100% dos arquivos e chaves criados, sem resíduos.

### 4. Política de Scripts de Instalação (`-ExecutionPolicy Bypass`)
- Os scripts `.bat` executam o PowerShell com a flag `-ExecutionPolicy Bypass`.
- Essa flag atua **exclusivamente no processo temporário** de instalação em andamento (`Process scope`), permitindo que scripts locais não assinados sejam executados sem alterar a política de segurança global da máquina do usuário.

### 5. Boas Práticas para o CLI em Rede Local
- O WebSocket nativo do Logitech G HUB (`ws://127.0.0.1:9010`) não possui autenticação nem criptografia TLS nativas.
- **Nunca exponha a porta 9010 diretamente para a internet.**
- Caso queira monitorar o G HUB a partir de outra máquina (ex: Linux para Windows), utilize um túnel SSH seguro com redirecionamento de porta local:
  ```bash
  ssh -N -L 9010:127.0.0.1:9010 usuario@seu-pc-com-ghub
  ```

### 6. Extensão GNOME Shell (Ubuntu): somente leitura do UPower
- A extensão não usa o G HUB. Ela lê as propriedades dos dispositivos publicadas pelo **UPower** no D-Bus do sistema (`org.freedesktop.UPower`, `org.freedesktop.UPower.Device`) por meio de `EnumerateDevices`, `GetAll` e dos sinais `DeviceAdded`, `DeviceRemoved` e `PropertiesChanged`.
- **Nenhum comando é enviado ao periférico** e nenhuma propriedade é escrita. Não há acesso a `/dev/hidraw*`, nem a dispositivos USB/Bluetooth diretamente.
- Sem rede, sem processos filhos, sem polling: código GJS carregado dentro do próprio `gnome-shell`, reagindo a sinais D-Bus.
- Instalação no escopo do usuário (`~/.local/share/gnome-shell/extensions/ghub-battery@vinipy12.github.io`), sem `sudo`. Os únicos dados gravados são duas preferências booleanas no dconf (`/org/gnome/shell/extensions/ghub-battery/`).
- As notificações usam a API nativa do GNOME (`MessageTray`) e ficam na máquina.
- O pacote `.shell-extension.zip` é gerado pelo GitHub Actions e listado no `SHA256SUMS.txt` da release, como os demais artefatos. Os fixtures de teste no repositório foram capturados de hardware real com endereços MAC e números de série substituídos por valores sintéticos.

---

## 🔍 Verificação de Integridade e Hashes SHA-256

Para auditar e certificar que seus arquivos baixados são legítimos e idênticos aos gerados pelo pipeline de integração contínua:

1. Acesse a release oficial no repositório.
2. Baixe o arquivo `SHA256SUMS.txt` gerado de forma transparente pelo GitHub Actions.
3. Compare o hash SHA-256 calculado localmente:
   ```powershell
   Get-FileHash Logitech-GHub-Widget-Setup.zip -Algorithm SHA256
   ```
   ```bash
   shasum -a 256 -c SHA256SUMS.txt --ignore-missing
   ```

---

## 🚨 Como Reportar uma Vulnerabilidade

Levamos a segurança da comunidade muito a sério. Se você descobrir uma vulnerabilidade de segurança:

1. **NÃO crie uma issue pública** no GitHub para relatar problemas de segurança com potencial de exploração.
2. Utilize o recurso oficial do GitHub: **[Security Advisories → Report a vulnerability](https://github.com/Leosdc/logitech-ghub-widget/security/advisories/new)**.
3. Alternativamente, entre em contato diretamente com os mantenedores por meio das informações de contato listadas no perfil oficial do GitHub.

### O que incluir no seu relatório:
- Descrição detalhada do vetor de ataque ou vulnerabilidade.
- Versão do widget, CLI ou extensão GNOME afetada.
- Sistema operacional e versão do Logitech G HUB instalada (ou, para a extensão, versão do GNOME Shell e do UPower: `gnome-shell --version`, `upower --version`).
- Passos reproduzíveis (PoC - Proof of Concept) ou script simplificado.

### Prazo de resposta:
- **Confirmação inicial:** Responderemos em até **48 horas**.
- **Avaliação e correção:** Manteremos você informado sobre a análise, desenvolvimento da correção e agendamento da release com os devidos créditos de divulgação responsável.
