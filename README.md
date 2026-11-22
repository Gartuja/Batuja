# 🖱️ Batuja - Monitor de Dispositivos sem Fio

Aplicativo projetado para monitorar a porcentagem de bateria dos seus dispositivos sem fio.

---

## Principais Funcionalidades

- **Sobreposição na Tela (HUD Overlay para Jogos e Vídeos em Tela Cheia) 🎮:**
  - Inspirado na sobreposição de desempenho do Windows (Windows + G / Xbox Game Bar).
  - Janela flutuante não-intrusiva (`WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`) que fica sempre no topo de **jogos em tela cheia (borderless/fullscreen)** e reprodutores de mídia sem roubar foco nem minimizar a aplicação.
  - **Bateria em Segmentos**: Design de bateria com contorno arredondado, polo positivo e barras verticais internas proporcionais à carga, acompanhado do ícone do dispositivo, porcentagem em tempo real e indicador de carregamento (⚡).
  - **Customizável**: Arraste para qualquer canto da tela (posição salva automaticamente), alterne entre orientação Horizontal e Vertical, ajuste opacidade (100% a 45%) ou bloqueie a posição.

- **Ícones Individuais na Barra de Tarefas / Bandeja:**
  - Cada dispositivo conectado ganha seu próprio ícone na bandeja do sistema exibindo a porcentagem exata da bateria (ex: `90`, `85`, `70`).
  - O aplicativo também fica presente na **Barra de Tarefas principal** como um aplicativo ativo padrão do Windows.
  - Dispositivos desligados saem automaticamente da bandeja e da tela principal para não poluir sua visualização.
  - Opção de ligar/desligar individualmente o ícone de qualquer periférico na bandeja via botão **`● Ícone Ligado` / `○ Ícone Desligado`**.
  - 
- **Histórico de Dispositivos Sem Fio 📋:**
  - Painel com registro persistente de todos os dispositivos sem fio que já foram conectados durante o uso do Batuja.
  - Mostra status em tempo real (**● Conectado** ou **○ Desligado**), protocolo, data/hora da última conexão e última porcentagem de carga registrada.

- **Personalização de Cores e Ícones:**
  - Escolha qualquer cor para cada periférico (ex: Verde Neon, Roxo, Ciano, Amarelo).
  - Escolha ou altere a categoria do ícone (Mouse, Teclado, Fone, Gamepad, Notebook, Bateria) ou selecione uma imagem PNG/ICO personalizada.
  - Suporte a plano de fundo personalizado com ajuste de opacidade e suporte a GIFs animados.
  - 
- **Leve e Otimizado:**
  - Executável único (`Batuja.exe`) de apenas **~166 KB**.
  - Desenvolvido em **C# nativo com Win32 APIs** (`SetupAPI`, `hid.dll`, `XInput`, `kernel32`).
  - Zero dependências pesadas (sem Electron, sem Python). Consome apenas **~15 MB de RAM** e **0% de CPU**.

---

## Como Usar

### Ativar Sobreposição (HUD Overlay):
- Clique no botão **`Sobrepor`** no cabeçalho da janela principal (ou clique com o botão direito em qualquer ícone da bandeja e selecione *Sobreposição na Tela (HUD)*).
- O HUD aparecerá no topo da tela. Arraste-o para onde preferir.
- Clique com o botão direito no HUD para configurar opacidade, alternar formato (horizontal/vertical) ou travar a posição.

### Personalizar a Cor e Ícone de uma Bateria:
1. No painel, clique no botão **"Cor"** do cartão do dispositivo desejado (ou clique com o botão direito no ícone da bandeja).
2. Escolha qualquer cor na paleta e clique em OK. A barra do cartão, o HUD e o número na barra de tarefas mudarão imediatamente.

### Modo Administrador (Opcional):
- Se o Windows restringir a leitura em tempo real de portas HID de baixo nível de algum dongle USB, clique no botão **"Admin"** no cabeçalho do aplicativo para reiniciar com privilégios elevados.

### Modo Diagnóstico no Terminal (CLI):
Para conferir a varredura no prompt/PowerShell sem abrir a interface gráfica:
```powershell
.\Batuja.exe --scan

