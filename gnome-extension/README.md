# 🔋 Logitech G Battery — extensão para GNOME Shell (Ubuntu 24.04)

> **Autor da versão Ubuntu/GNOME:** **[@vinipy12](https://github.com/vinipy12)** (*Vinicius Cavalcante*) — [PR #2](https://github.com/Leosdc/logitech-ghub-widget/pull/2)

Mostra a bateria dos seus periféricos Logitech G na barra superior do GNOME,
um item por dispositivo, com cores âmbar (≤ 50%) e vermelha (≤ 20%) e uma
notificação quando um dispositivo cai para 20% ou menos.

Não precisa do Logitech G HUB: os dados vêm do **UPower**, que o Ubuntu já
usa para a bateria do notebook. Dispositivos ligados por receptor Lightspeed
aparecem pelo driver `hid-logitech-hidpp` do kernel; dispositivos Bluetooth
aparecem pelo BlueZ. A extensão é GJS puro (ESM), fala D-Bus diretamente com
o UPower e não roda nenhum processo auxiliar nem faz polling.

## Requisitos

- Ubuntu 24.04 (GNOME Shell 46). Outras versões do Shell não são suportadas
  (o `metadata.json` declara apenas `"46"`; o GNOME recusa carregar em outras).
- UPower (já instalado no Ubuntu).
- Mouse/teclado por receptor Lightspeed: kernel com `hid-logitech-hidpp`
  (padrão no Ubuntu). Headset/teclado por Bluetooth: BlueZ (padrão).

Para conferir se o UPower já vê o dispositivo antes de instalar:

```bash
upower -e            # lista os caminhos; procure mouse_*, keyboard_*, headset_*
upower -i <caminho>  # mostra model, percentage, battery-level e state
```

## Instalação

A partir do pacote da release (`ghub-battery@vinipy12.github.io.shell-extension.zip`,
listado no `SHA256SUMS.txt`):

```bash
gnome-extensions install -f ghub-battery@vinipy12.github.io.shell-extension.zip
```

Ou a partir do código-fonte:

```bash
cd gnome-extension
make install
```

Na **primeira instalação** o Shell precisa ser reiniciado para enxergar a
extensão. Em sessão X11: `Alt+F2`, digite `r`, Enter. Em Wayland: encerre a
sessão e entre de novo. Depois:

```bash
gnome-extensions enable ghub-battery@vinipy12.github.io
gnome-extensions info ghub-battery@vinipy12.github.io   # deve mostrar State: ACTIVE
```

### Atualização

Repita a instalação (`make install` ou `gnome-extensions install -f`) e
reinicie o Shell do mesmo jeito. As preferências são mantidas.

### Desinstalação

```bash
gnome-extensions uninstall ghub-battery@vinipy12.github.io
```

Isso apaga `~/.local/share/gnome-shell/extensions/ghub-battery@vinipy12.github.io`.
As duas preferências ficam no dconf em `/org/gnome/shell/extensions/ghub-battery/`;
para zerá-las: `dconf reset -f /org/gnome/shell/extensions/ghub-battery/`.

## O que aparece

| Situação | Barra superior |
|---|---|
| Dispositivo com porcentagem exata (ex.: G515 por Bluetooth) | `36%` |
| Dispositivo que só informa nível (ex.: G305 pelo receptor) | `Cheia`, `Alta`, `Normal`, `Baixa`, `Crítica` |
| Carregando | `⚡ 40%` |
| Dormindo / desconectado | sai da barra; fica em cinza no menu como *Inativo* |
| UPower parado | um único item `—` com a mensagem *UPower indisponível* no menu |

- Os itens ficam à direita, ao lado dos indicadores do sistema, ordenados por
  tipo: mouse, teclado, headset, controle, outros.
- Cores: neutra acima de 50%, **âmbar** em 50% ou menos, **vermelha** em 20% ou
  menos. Para dispositivos que só informam nível: *Baixa* é âmbar; *Crítica* é
  vermelha. Carregando nunca fica colorido.
- Clicar em qualquer item abre o menu com **todos** os dispositivos (ativos e
  inativos) e um atalho para **Preferências**.
- A notificação de bateria fraca dispara **uma vez por dispositivo** quando ele
  cruza 20% ou menos e só volta a disparar depois que ele recupera (carregar ou
  subir acima de 20%). Bloquear e desbloquear a tela não repete o aviso.

## Preferências

Abra pelo menu da extensão ou pelo app **Extensões** (ou
`gnome-extensions prefs ghub-battery@vinipy12.github.io`).

| Opção | Chave (`org.gnome.shell.extensions.ghub-battery`) | Padrão |
|---|---|---|
| Notificar bateria fraca | `notify-low` | ligado |
| Mostrar dispositivos inativos no menu | `show-inactive` | ligado |

## Limitações

- Mouses mais antigos pelo receptor (como o G305) só informam nível, não
  porcentagem exata. Leitura direta via HID++ fica para um projeto futuro.
- Sem consumo em mW nem horas restantes (o UPower não expõe isso).
- A notificação de bateria fraca some ao bloquear a tela (o GNOME desativa
  extensões na tela de bloqueio); o item na barra continua vermelho e o aviso
  não é repetido ao desbloquear.
- Dispositivos que o UPower não enxerga (sem driver `hidpp`, ou headsets por
  dongle USB próprio) não aparecem. Confira com `upower -e`.

## Solução de problemas

| Sintoma | O que fazer |
|---|---|
| `gnome-extensions info` diz que a extensão "doesn't exist" | O Shell ainda não foi reiniciado desde a instalação. X11: `Alt+F2`, `r`, Enter. Wayland: encerre e reabra a sessão. |
| `State: ERROR` ou nada aparece na barra | Veja o log: `journalctl --user -b -o cat _COMM=gnome-shell \| grep -iE "ghub-battery\|JS ERROR"` e abra uma issue com a saída. |
| Item `—` com *UPower indisponível* | `systemctl status upower`; a extensão volta sozinha quando o serviço sobe. |
| Dispositivo ligado mas listado como *Inativo* | Pelo receptor, o UPower só atualiza quando o dispositivo acorda: mexa o mouse ou aperte uma tecla. Por Bluetooth, confira a conexão nas Configurações. |
| Porcentagem "travada" em 50% para um dispositivo pelo receptor | É o valor-padrão que o UPower publica enquanto o dispositivo dorme; a extensão ignora e mostra *Inativo*. |

## Segurança e privacidade

- Só lê propriedades do UPower pelo D-Bus do sistema (`org.freedesktop.UPower`
  e `org.freedesktop.UPower.Device`): nenhum comando é enviado ao dispositivo.
- Nenhuma conexão de rede, nenhum processo filho, nenhum arquivo gravado além
  das duas preferências no dconf.
- Instalada no escopo do usuário, sem `sudo`.

## Desenvolvimento

Dependências (Ubuntu): `gjs`, `libglib2.0-bin`, `gir1.2-upowerglib-1.0`; o
comando `gnome-extensions` vem com o `gnome-shell`.

```bash
make test        # testes puros (gjs -m), sem Shell: modelo, menu e corrida do cliente D-Bus
make test-live   # lê o UPower real desta máquina
make test-race   # só o teste determinístico de corrida do cliente D-Bus
make pack        # gera o zip e confere o conteúdo
make install     # instala em ~/.local/share/gnome-shell/extensions
make nested      # GNOME Shell aninhado para testar sem reiniciar a sessão
```

Estrutura:

| Arquivo | Responsabilidade |
|---|---|
| `extension.js` | Ciclo de vida (`enable`/`disable`), estado por dispositivo, alertas, montagem dos itens na barra |
| `upowerSource.js` | Cliente D-Bus do UPower: enumeração, sinais, snapshots consistentes |
| `model.js` | Funções puras: filtro de periféricos, normalização, rótulo, cor, ordenação, máquina de estado do alerta |
| `panelItem.js` | Um item da barra (`PanelMenu.Button`) e seu menu |
| `menu.js` | Linhas do menu a partir dos registros (puro) |
| `notifier.js` | Fonte de notificações do GNOME, criada sob demanda |
| `prefs.js` | Janela de preferências (Adwaita) |
| `schemas/` | GSettings schema |
| `tests/` | Runner, fixtures capturados de hardware real (identificadores anonimizados) e testes |

Em um Shell real, o estado interno pode ser inspecionado pelo Looking Glass
(`Alt+F2`, `lg`):

```js
imports.ui.main.extensionManager.lookup('ghub-battery@vinipy12.github.io').stateObj
```
