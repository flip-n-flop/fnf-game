# Contexto do Projeto: FlipNflop (Unity C#)

> **Diretório Raiz de Contextos:** `Assets/.agents/`  
> **Escopo Principal de Leitura/Edição:** `Assets/Scripts/` e `Assets/UI Toolkit/`  
> **Padrão Obrigatório de Código C#:** [csharp_coding_standards.md](file:///D:/Projetos/FlipNflop/Assets/.agents/csharp_coding_standards.md)

---

## 1. Visão Geral do Projeto

**FlipNflop** é um jogo 2D de plataforma e puzzle educativo focado em **Eletrônica Digital** (Flip-Flops e Diagramas de Tempo). 

- **Mecânica Central:** O jogador controla um personagem que corre horizontalmente por um nível, podendo pular, dar dash e **inverter a gravidade** (alternar entre chão e teto).
- **Rastreamento de Sinal:** Conforme o jogador se move, o rastro da sua trajetória é desenhado em tempo real (`SignalPath`) representando o sinal lógico de saída \(Q\) do Flip-Flop (1 = teto/alto, 0 = chão/baixo).
- **Simulação Lógica:** O nível carrega dados de sinais de entrada (J, K, Clock, Preset, Clear, D, T) a partir de arquivos JSON (`LevelData`). O simulador interno (`FlipFlopSimulator`) calcula a linha do tempo e os eventos de transição esperados.
- **Verificação e Avaliação:** Ao final da fase (ou na morte do jogador), o gabarito correto (`GabaritoGenerator`) é comparado com o trajeto do jogador (`PathChecker` / `PathVerifier`), gerando feedback visual (linhas verdes para acertos, tracejadas vermelhas para erros) e calculando a pontuação (`ScoreController`).

---

## 2. Arquitetura do Código (`Assets/Scripts/`)

A pasta `Assets/Scripts/` está organizada nos seguintes subdomínios modulares:

```
Assets/Scripts/
├── Common/            # Controladores comuns (Câmera, utilitários)
├── Gla/               # Game Learning Analytics (Serialization/: GlaJson, assembly FlipNflop.Gla.Serialization)
├── Hint/              # Sistema de dicas visuais em tempo real
├── Level/             # Dados da fase, simulação lógica, carregamento e renderização de Tilemaps
│   ├── Loading/       # Deserialização JSON, sequência de fases e interop WebGL
│   ├── Obstacles/     # Spawning e controle de obstáculos (ex: maces)
│   └── Rendering/     # Renderização de tilemaps, rótulos Canvas e parallax
├── Menus/             # Gerenciadores de interface (Menu Principal, Pausa, Tela de Resultados, WebGL)
├── Player/            # Física do jogador, geração de rastro, gabarito e verificação de caminho
└── Settings/          # Gerenciamento de configurações (Cores, Áudio, Vídeo, Rebind de Teclas)
```

---

### 2.1. Módulo Level (`Assets/Scripts/Level/`)

- **`LevelData.cs`**: Data Transfer Object (DTO) serializável para representação de fases JSON. Possui conversores customizados do `Newtonsoft.Json`:
  - `BinaryBoolArrayConverter`: Converte strings binárias (ex: `"1010 00"`) para `bool[]`.
  - `UnityColorConverter`: Converte strings hex (ex: `"#FF0000"`) para `UnityEngine.Color`.
  - `AsyncActiveConverter`: Trata fallbacks de nível ativo assíncrono (High/Low ou 1/0).
- **`FlipFlopSimulator.cs`**: Classe estática responsável pela simulação puramente lógica dos flip-flops (JK, SR, D, T).
  - O `SimulateJK`, o único usado pelas fases, delega ao `JkSimulator` do FlipNflopCommons (seção 2.7). Só aceita passo de clock 6 e comprimento múltiplo dele; fora disso lança `InvalidOperationException`. `CreateJkPhase` monta a `PhaseDefinition` do commons (tipo JK fixo, porque o `LevelData` não lê `ffType`). SR, D e T continuam locais e sem uso.
  - Suporta entradas síncronas (bordas de clock) e assíncronas (Preset e Clear).
  - Gera uma linha do tempo com **dupla resolução** (índices pares = síncrono em `X.0`, índices ímpares = assíncrono em `X.5`).
  - Lança exceções caso ocorram combinações inválidas (ex: Preset e Clear ativos simultaneamente).
- **`LevelManager.cs`**: Singleton MonoBehaviour que centraliza as dimensões lógicas da fase (`diagramEndX`, `phaseSlackTiles`, `phaseEndX`, `levelEndX`, `clockStepX`).
- **`Loading/`**:
  - **`LevelJsonLoader.cs`**: Carrega o JSON ativo (seja via WebGL upload `UploadedLevelJson`, `Resources/Levels/` pelo `MenuManager`, ou `TextAsset` do editor). Executa o `FlipFlopSimulator`, invoca o `TilemapRenderer` e o `ObstacleSpawner`, e registra ouvintes para atualização de cores em tempo real (`SignalColorManager.OnColorsChanged`). `CreatePhaseDefinition()` entrega a `PhaseDefinition` do commons da fase carregada, com o mesmo passo e comprimento da simulação.
  - **`LevelSequenceManager.cs`**: Gerencia o índice e a lista sequencial de fases.
  - **`UploadLevelJson.cs`**: Armazena estaticamente o conteúdo JSON enviado via WebGL upload.
- **`Obstacles/`**:
  - **`ObstacleSpawner.cs`** & **`MaceController.cs`**: Instanciam e gerenciam obstáculos no mapa com base nas posições relativas ao chão/teto definidas no JSON.
- **`Rendering/`**:
  - **`TilemapRenderer.cs`**: Manipula Tilemaps do Unity (Input, Terrain, Clock). Converte vizinhanças de 3 bits (0..7 para 000..111) em tiles específicos de diagrama e renderiza bandas de chão/teto com suporte a espelhamento vertical.
  - **`SignalLabelRenderer.cs`**: Instancia e posiciona dinamicamente rótulos UI no Canvas (J, K, PRE, CLR, CLK com sobrelinha `Overline` para sinais ativos em nível baixo) nas coordenadas Y reais dos Tilemaps.
  - **`CameraFollower.cs`** & **`BackgroundMove.cs`**: Suporte para acompanhamento suave da câmera e efeito parallax de fundo.

---

### 2.2. Módulo Player (`Assets/Scripts/Player/`)

- **`PlayerController.cs`**: Controlador 2D baseado no novo Unity Input System (`InputActionReference`).
  - **Movimentação:** Suavização por aceleração, desaceleração e controle aéreo.
  - **Pulo & Gravidade:** Pulo direcional e inversão lógica/física da gravidade (`gravityScale *= -1` e rotação de 180°).
  - **Dash:** Override de velocidade horizontal com congelamento no eixo Y (`FreezePositionY`) e cooldown.
  - **Eventos:** Notifica o término da fase ou a morte do jogador (ao colidir com a tag `"Enemy"` ou cair fora dos limites).
- **`SignalPath.cs`**: Registra os pontos do trajeto do jogador via `LineRenderer`. Insere segmentos verticais em transições de gravidade e limpa o histórico caso o jogador recue para `X <= 0`.
- **`GabaritoGenerator.cs`**: Gera as quinas do gabarito no mundo (`CorrectCorners`) com o `AnswerKeyCorners` do commons, a partir da `PhaseDefinition` do `LevelJsonLoader` e da geometria da cena; expõe o objeto do commons em `AnswerKeyCorners`. `BuildFromEvents` mantém a assinatura, mas as quinas seguem a fase carregada: eventos diferentes dos dela, ou estado inicial alto, só geram um `Debug.LogError`.
- **`PathChecker.cs`**: Avaliador independente (não-MonoBehaviour) que testa o caminho do jogador contra as quinas do gabarito com o `CornerEvaluator` do commons, retornando a struct imutável `PathCheckResult`. Avalia só com o construtor `PathChecker(AnswerKeyCorners, bool)`; o construtor só com tolerância é mantido por compatibilidade e faz `Evaluate` lançar `InvalidOperationException`.
- **`PathVerifier.cs`**: Orquestrador da verificação final. Desenha linhas de feedback sólidas (`successColor`) ou tracejadas (`failureColor`), com o critério do `SegmentValidator` do commons, enviando os resultados para o `ScoreController` e `ResultScreenController`. Publica o resultado da validação no evento estático `ValidationCompleted` (seção 2.7).
- **`ScoreController.cs`**: Singleton responsável pelo cronômetro em tempo real, pelo cálculo do score final (\(\text{Score} = \max(0, \text{AccuracyPart} - \text{TimePenalty})\), pelo `ScoreRules` do commons, com o `maxScore` do componente) e por repassar os dados de exibição. `ReportResult` devolve a pontuação mostrada.
- **`TraceValidationResult.cs`**: Resultado da validação (C# puro): `Conclusion`, `OverallAccuracy`, `Performance`, `Trace` e `Correct` (`int?[]`, um por trecho). `ForShortTrace(n)` é o resultado do traçado com menos de 2 pontos.
- **`PathPointConversion.cs`**: Extensões que convertem `Vector3` ↔ `PathPoint` do commons (z descartado).

---

### 2.3. Módulo Menus (`Assets/Scripts/Menus/`)

- **`MenuManager.cs`**: Gerencia a interface do menu principal usando **UI Toolkit** (`UIDocument`). Controla a navegação entre submenus (`MainMenu`, `About`, `LevelSelect`, `HowToPlay`), a seleção de níveis e a integração com upload WebGL.
- **`GameMenuManager.cs`**: Gerencia o menu de pausa in-game (`UIDocument`). Trata a parada do tempo (`Time.timeScale = 0`), bloqueia os inputs do jogador (`PlayerInput.Disable()`) e gerencia a navegação entre as opções da pausa.
- **`ResultScreenController.cs`**: Modal de fim de jogo (Vitória/Derrota). Utiliza **Unity Localization** (`LocalizationSettings`) para internacionalização de títulos e estatísticas (Score, Tempo).
- **`UploadMenuManager.cs`**: Ponte WebGL (`[DllImport("__Internal")]`) para abrir o seletor de arquivos do navegador e carregar arquivos JSON customizados do usuário.
- **`PauseManager.cs`**: Script legado para manipulação simples de pause.

---

### 2.4. Módulo Settings (`Assets/Scripts/Settings/`)

- **`SignalColorManager.cs`**: Classe pura C# (POCO Singleton, não MonoBehaviour) que encapsula de forma independente **tudo** sobre cores de sinais e paletas de acessibilidade: presets (`PresetColors`), paletas (`Palettes`, `CUSTOM_INDEX`), persistência via `PlayerPrefs` e o evento `OnColorsChanged`.
- **`AudioSettings.cs`**: Classe pura C# (POCO Singleton, não MonoBehaviour) que encapsula os dados de volume (Master, SFX, Música), valores padrão, persistência via `PlayerPrefs` e o evento `OnAudioChanged`.
- **`AudioManager.cs`**: MonoBehaviour de cena na fase responsável pelo runtime de áudio (`AudioMixer`, `AudioSource`). Ouve `AudioSettings.OnAudioChanged` e aplica decibéis ao `AudioMixer`. Destruído naturalmente ao sair da fase para não sobrepor a música do menu principal.
- **`ConfigManager.cs`**: Controlador central da tela de configurações (`UIDocument`). Gerencia as abas (Cores, Áudio, Vídeo, Controles) e delega o comportamento para instâncias que implementam `ISettingsTab`.
- **`ISettingsTab.cs`**: Contrato genérico (`Init`, `RegisterCallbacks`, `UnregisterCallbacks`, `OnLocaleChanged`) para os módulos de configuração.
- **`ColorSettingsTab.cs`**: Interface UI Toolkit para personalização de cores dos sinais lógicos. Interage diretamente com `SignalColorManager.Instance`.
- **`AudioSettingsTab.cs`**: Interface UI Toolkit para controle de volume. Interage diretamente com `AudioSettings.Instance`.
- **`VideoSettingsTab.cs`**: Interface UI Toolkit para contraste e idioma. Lê/grava contraste via `PlayerPrefs` e aplica no sprite de overlay.
- **`RebindManager.cs`**: Sistema de remapeamento dinâmico de teclas com suporte a localização e persistência de bindings via JSON (`SaveBindingOverridesAsJson`).

---

### 2.5. Módulo Hint (`Assets/Scripts/Hint/`)

- **`HintController.cs`**: Escuta as ações do Input System dedicadas às dicas (`toggleClockLinesAction`, `showOperationHintAction`) e delega para os componentes responsáveis.
- **`ClockLineHint.cs`**: Desenha linhas verticais tracejadas no mundo correspondentes às bordas de clock e transições assíncronas nos modos: *Off*, *ClockOnly*, *ClockAndAsync*.
- **`OperationHint.cs`**: Instancia temporariamente labels 3D TextMeshPro indicando a operação do próximo ciclo de clock (Set, Reset, Comuta, Mantém, Preset, Clear) em relação à posição do jogador.

---

### 2.6. Módulo Common (`Assets/Scripts/Common/`)

- **`CameraController.cs`**: Controlador de câmera ortográfica com dois modos:
  1. `FollowPlayer`: Acompanha o jogador no eixo X com `Lerp` e limites (`minX`, `maxX`).
  2. `ManualControl`: Permite ao jogador rolar a câmera manualmente (usado ao final da fase ou após a morte com travamento de limite à direita).
- **`PrintController.cs`**: Utilitário auxiliar.

---

### 2.7. Regras comuns e GLA (FlipNflopCommons, `Assets/Scripts/Gla/`)

- **Biblioteca comum:** `Assets/Plugins/FlipNflopCommons/` tem o `FlipNflopCommons.dll` (`netstandard2.1`, sem dependências), a documentação XML e o `VERSION` (versão e commit do `fnf-commons`). Não edite à mão: rode `scripts/update-commons.sh [caminho-do-fnf-commons]` (padrão `../fnf-commons`), que também copia os exemplos canônicos do contrato para `Assets/Tests/Gla/Contract/`. O `Assets/link.xml` preserva o *assembly* no *stripping* do IL2CPP.
- **Regras do traçado:** gabarito, diagrama correto, acerto geral, verde/vermelho, nível e acerto por trecho e pontuação vêm de `FlipNflop.Commons.PhaseRules`. Não reimplemente essas regras no jogo: mude-as no commons e atualize o DLL. A geometria é a da cena `Custom.unity`: área jogável a partir de x = 0, `startX` 4, níveis −0,5 e 3,25 e **tolerância 0,2** (o padrão 1,0 do código não vale).
- **`PathVerifier.ValidationCompleted`** (`Action<TraceValidationResult>`): disparado em `RunPipeline` na chegada e na morte (inclusive no traçado curto), depois do cálculo e antes de `ResultScreenController.Show`. É o gancho de lógica da coleta (gla-14): assine em `OnEnable` e desinscreva em `OnDisable`. O evento é zerado em `SubsystemRegistration` (*Domain Reload* desligado), e a exceção de um assinante é registrada sem interromper a validação. Em *Development Build*, o resultado é impresso no console.
- **`GlaJson`** (`Assets/Scripts/Gla/Serialization/`, *assembly* `FlipNflop.Gla.Serialization`, referenciado automaticamente pelo `Assembly-CSharp`): serializador do contrato em Newtonsoft.Json. Nomes de `ContractNames`, todo campo escrito (com `null` explícito), campo ausente ou desconhecido é erro na leitura, causa em minúsculas e datas em UTC `yyyy-MM-ddTHH:mm:ss.fffZ`. Use `GlaJson.Serialize` e `GlaJson.Deserialize<T>`; não serialize o contrato com `JsonUtility`.
- **Testes:** `Assets/Tests/Gla/` (*assembly* EditMode `FlipNflop.Gla.Tests`) confere o `GlaJson` contra os exemplos canônicos. Rode pelo Test Runner do editor depois de atualizar o DLL.

---

## 3. Interface de Usuário com UI Toolkit (`Assets/UI Toolkit/`)

Toda a interface moderna do projeto é construída via **Unity UI Toolkit**:

- **Layouts (`Assets/UI Toolkit/Layouts/`)**:
  - `MainMenu.uxml`: Menu inicial, seleção de níveis, tela Sobre, HowToPlay e Configurações integradas.
  - `GameMenu.uxml`: Menu de pausa em jogo.
  - `ConfigMenu.uxml`: Menu de configurações em abas (Cores, Áudio, Vídeo, Controles).
  - `ResultScreen.uxml`: Modal de vitória e derrota com opção de esconder/mostrar o desenho da onda.
  - `HowToPlayMenu.uxml`: Instruções do jogo e conceitos de eletrônica digital.
- **Estilos (`Assets/UI Toolkit/Styles/`)**:
  - `Panel.uss`: Estilização principal de botões (`.menu-button`), contêineres, abas, seletores de cor, sliders e popups.
  - `MainMenu.uss`: Estilos específicos da tela principal.
  - **Fonte:** Utiliza a fonte de projeto `Exo2-VariableFont_wght.ttf` via `TextMesh Pro`.

---

## 4. Padrões e Boas Práticas Estabelecidas

1. **Separação de Responsabilidades (POCO vs MonoBehaviour):**
   - Classes puras de C# (POCOs) tratam lógica de dados, simulação e configurações sem acoplamento com a cena (ex: `FlipFlopSimulator`, `PathChecker`, `GabaritoGenerator`, `TilemapRenderer`, `SignalColorManager`, `AudioSettings`, `AudioSettingsTab`, `VideoSettingsTab`, `ColorSettingsTab`).
   - Monobehaviours atuam como orquestradores de ciclo de vida e visualização (`LevelJsonLoader`, `PathVerifier`, `PlayerController`, `ConfigManager`, `AudioManager`).
2. **Eventos e Desacoplamento:**
   - Evento `SignalColorManager.OnColorsChanged` para atualização reativa das cores em `LevelJsonLoader` e `PathVerifier`.
   - Evento `AudioSettings.OnAudioChanged` para sincronização reativa do `AudioMixer` em `AudioManager`.
   - Evento `LocalizationSettings.SelectedLocaleChanged` para atualização dinâmica de textos em tempo real.
3. **Persistência de Dados:**
   - Cada domínio gerencia seus `PlayerPrefs` (`SignalColorManager` para cores/paletas, `AudioSettings` para volumes, `VideoSettingsTab` para contraste). Rebinds de Input são gerenciados separadamente por `RebindManager` via JSON.
4. **Input System (Unity New Input System):**
   - Todas as ações do jogador e menus usam `InputActionReference` com callbacks (`performed`, `canceled`) e tratamentos adequados em `OnEnable` / `OnDisable`.
5. **Simulação de Dupla Resolução:**
   - A timeline de saída duplica os pontos por tile para acomodar avaliações síncronas (`X.0`) e assíncronas (`X.5`).

---

## 5. Diretrizes para Agentes de IA

Ao efetuar alterações ou criar novos recursos neste codebase:
- **Respeite o Escopo:** Foque as edições em `Assets/Scripts/` e `Assets/UI Toolkit/`.
- **Mantenha os Contratos de API:** Não altere assinaturas de métodos públicos de utilitários como `FlipFlopSimulator`, `PathChecker` ou `LevelData` sem atualizar todas as suas referências.
- **Siga a Arquitetura POCO/MonoBehaviour:** Lógicas de simulação ou regras puras de cálculo devem ser isoladas em classes C# testáveis e sem dependência desnecessária de `MonoBehaviour`.
- **Manutenção de UI Toolkit:** Ao criar ou modificar telas, utilize arquivos `.uxml` e estilizações centralizadas no `.uss` existente (`Panel.uss`), garantindo suporte às classes css padrão como `.hidden` e `.menu-button`.
