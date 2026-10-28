# 🔋 Logitech G Battery — extensão para GNOME Shell (Ubuntu 24.04)

Mostra a bateria dos seus periféricos Logitech G na barra superior do GNOME,
um item por dispositivo, com cores âmbar (≤ 50%) e vermelha (≤ 20%) e uma
notificação quando um dispositivo cai para 20% ou menos.

Não precisa do Logitech G HUB: os dados vêm do **UPower**, que o Ubuntu já
usa para a bateria do notebook. Dispositivos ligados por receptor Lightspeed
aparecem pelo driver `hid-logitech-hidpp` do kernel; dispositivos Bluetooth
aparecem pelo BlueZ.

## Requisitos

- Ubuntu 24.04 (GNOME Shell 46). Outras versões do Shell não são suportadas.
- UPower (já instalado no Ubuntu).

## Instalação

```bash
cd gnome-extension
make install
```

Em sessão X11: `Alt+F2`, digite `r`, Enter (reinicia o Shell). Em Wayland:
encerre a sessão e entre de novo. Depois:

```bash
gnome-extensions enable ghub-battery@vinipy12.github.io
```

Ou baixe o `.shell-extension.zip` em Releases e rode
`gnome-extensions install -f <arquivo>.zip`.

## O que aparece

| Situação | Barra superior |
|---|---|
| Dispositivo com porcentagem exata (ex.: G515 por Bluetooth) | `36%` |
| Dispositivo que só informa nível (ex.: G305 pelo receptor) | `Cheia`, `Alta`, `Normal`, `Baixa`, `Crítica` |
| Carregando | `⚡ 40%` |
| Dormindo / desconectado | sai da barra; fica em cinza no menu como *Inativo* |

## Limitações

- Mouses mais antigos pelo receptor (como o G305) só informam nível, não
  porcentagem exata. Leitura direta via HID++ fica para um projeto futuro.
- Sem consumo em mW nem horas restantes (o UPower não expõe isso).
- A notificação de bateria fraca some ao bloquear a tela (o GNOME desativa extensões na tela de bloqueio); o item na barra continua vermelho.

## Desenvolvimento

```bash
make test        # testes puros (gjs -m), sem Shell
make test-live   # lê o UPower real desta máquina
make test-race   # teste determinístico de corrida do cliente D-Bus
make pack        # gera o zip e confere o conteúdo
make nested      # GNOME Shell aninhado para testar sem reiniciar a sessão
```
