# 🖱️ Batuja - Monitor de Dispositivos sem Fio

Um aplicativo ultra leve, nativo e otimizado para Windows projetado para monitorar a porcentagem de bateria dos seus dispositivos sem fio (**MCHOSE V9 Turbo**, **Attack Shark RS6 Ultra**, **VK 87 Max V2**, **Logitech**, **Anbernic**, controles e periféricos em geral).

---

## 🚀 Principais Destaques e Funcionalidades

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

- **Estilos de Ícones na Bandeja 🎨:**
  - Suporte a múltiplos temas para os números na bandeja:
    - **Moderno**: Fundo escuro com cantos arredondados e números legíveis.
    - **Aeroglass**: Efeito de vidro translúcido com brilho e reflexo sutil.
    - **Retro**: Visual pixel art 8-bit com borda clássica.
    - **Minimalista**: Apenas o número puro com antialiasing.

- **Histórico de Dispositivos Sem Fio 📋:**
  - Painel com registro persistente de todos os dispositivos sem fio que já foram conectados durante o uso do Batuja.
  - Mostra status em tempo real (**● Conectado** ou **○ Desligado**), protocolo, data/hora da última conexão e última porcentagem de carga registrada.

- **Personalização de Cores e Ícones:**
  - Escolha qualquer cor para cada periférico (ex: Verde Neon, Roxo, Ciano, Amarelo).
  - Escolha ou altere a categoria do ícone (Mouse, Teclado, Fone, Gamepad, Notebook, Bateria) ou selecione uma imagem PNG/ICO personalizada.
  - Suporte a plano de fundo personalizado com ajuste de opacidade e suporte a GIFs animados.

- **Reconhecimento Nativo dos seus Dispositivos:**
  - 🖱️ **Attack Shark RS6 Ultra** (Compx / Dual Magnetic 2.4GHz)
  - 🎧 **MCHOSE V9 Turbo** (Protocolo MCHOSE Wireless 2.4GHz)
  - ⌨️ **VK 87 Max V2** (Valkyrie Wireless HID)
  - 🎮 **Anbernic / Xbox / 8BitDo** (XInput)
  - 🎧 **Fones Bluetooth** (JBL Tune Flex 2, Wave Beam 2, AirPods, etc.)
  - 🖱️ **Logitech Lightspeed e Unifying** (G Pro, MX Master, G502, etc.)

- **Extremamente Leve e Otimizado:**
  - Binário executável único (`Batuja.exe`) de apenas **~166 KB**.
  - Desenvolvido em **C# nativo com Win32 APIs** (`SetupAPI`, `hid.dll`, `XInput`, `kernel32`).
  - Zero dependências pesadas (sem Electron, sem Python). Consome apenas **~15 MB de RAM** e **0% de CPU**.

---

## 🛠️ Como Usar

### 1. Iniciar o Aplicativo:
- Dê dois cliques em **`start.bat`** ou **`Batuja.exe`**.
- A janela principal abrirá listando seus periféricos (MCHOSE V9 Turbo, Attack Shark RS6 Ultra, VK 87 Max V2, etc.).

### 2. Ativar Sobreposição (HUD Overlay):
- Clique no botão **`Sobrepor`** no cabeçalho da janela principal (ou clique com o botão direito em qualquer ícone da bandeja e selecione *Sobreposição na Tela (HUD)*).
- O HUD aparecerá no topo da tela. Arraste-o para onde preferir.
- Clique com o botão direito no HUD para configurar opacidade, alternar formato (horizontal/vertical) ou travar a posição.

### 3. Personalizar a Cor e Ícone de uma Bateria:
1. No painel, clique no botão **"Cor"** do cartão do dispositivo desejado (ou clique com o botão direito no ícone da bandeja).
2. Escolha qualquer cor na paleta e clique em OK. A barra do cartão, o HUD e o número na barra de tarefas mudarão imediatamente.

### 4. Modo Administrador (Opcional):
- Se o Windows restringir a leitura em tempo real de portas HID de baixo nível de algum dongle USB, clique no botão **"Admin"** no cabeçalho do aplicativo para reiniciar com privilégios elevados.

### 5. Modo Diagnóstico no Terminal (CLI):
Para conferir a varredura no prompt/PowerShell sem abrir a interface gráfica:
```powershell
.\Batuja.exe --scan
```

---

## ⚙️ Como Recompilar

O projeto já inclui o executável compilado. Caso faça alterações no código-fonte em `src/`, basta rodar:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```
