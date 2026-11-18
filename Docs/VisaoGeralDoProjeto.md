[← Voltar para a página inicial](../README.md)

---

# 🚀 Visão Geral e Arquitetura do Projeto: Flip'n Flop

Bem-vindo à documentação oficial do **Flip'n Flop**! Este documento foi elaborado para que qualquer pessoa (desenvolvedor, designer ou educador) consiga entender rapidamente o conceito do jogo, sua arquitetura de código, fluxo de execução e como contribuir.

---

## 📋 Sumário
1. [O que é o Flip'n Flop?](#1-o-que-é-o-flipn-flop)
2. [Conceitos do Jogo e Mecânicas](#2-conceitos-do-jogo-e-mecânicas)
3. [Estrutura de Pastas do Repositório](#3-estrutura-de-pastas-do-repositório)
4. [Arquitetura de Código (Módulos C#)](#4-arquitetura-de-código-módulos-c)
5. [Fluxo de Execução de uma Fase](#5-fluxo-de-execução-de-uma-fase)
6. [Integração WebGL e Customização](#6-integração-webgl-e-customização)
7. [Padrões de Código e Boas Práticas](#7-padrões-de-código-e-boas-práticas)
8. [Documentações Relacionadas](#8-documentações-relacionadas)

---

## 1. O que é o Flip'n Flop?

O **Flip'n Flop** é um jogo educativo 2D que combina elementos de **Plataforma e Puzzle** com conceitos de **Eletrônica Digital**. 

O objetivo principal é ensinar o funcionamento prático de **Flip-Flops** (JK, D, T, SR) e **Diagramas de Tempo**. O jogador assume o controle de um personagem em corrida horizontal contínua que precisa alternar sua gravidade entre o chão (nível lógico `0`) e o teto (nível lógico `1`) para desenhar a forma de onda correta correspondente à saída lógica \(Q\) do circuito.

---

## 2. Conceitos do Jogo e Mecânicas

### 🎮 Gameplay Central
- **Corrida e Gravidade:** O personagem avança no eixo X. Pressionar a ação de gravidade faz o personagem inverter de orientação (chão \(\rightarrow\) teto ou teto \(\rightarrow\) chão).
- **Sinal de Trajetória (`SignalPath`):** Conforme o jogador se move, um `LineRenderer` registra o caminho percorrido no espaço.
  - **Chão (Bottom Tilemap):** Representa o nível lógico **Baixo (0)**.
  - **Teto (Top Tilemap):** Representa o nível lógico **Alto (1)**.
- **Rótulos dos Sinais:** Os sinais digitais de entrada (J, K, Clock, Preset, Clear) são desenhados na tela com rótulos visuais sincronizados com os ciclos de tempo.
- **Dicas Visuais (`Hint`):** O jogador pode ativar linhas verticais guia nos momentos exatos das bordas de clock (`rising` ou `falling`) ou overlays de operação.

### 📐 Simulação Lógica
- Cada fase é definida por um arquivo **JSON** (`LevelData`).
- As entradas digitais mudam ao longo do tempo (divididas em passos de tempo / ciclos de clock).
- O simulador de Flip-Flop interno (`FlipFlopSimulator`) calcula a saída lógica ideal esperada \(Q\) em cada borda de subida/descida do clock e em eventos assíncronos (Preset e Clear). A simulação J-K, o gabarito, a validação do traçado e a pontuação vêm da biblioteca comum **FlipNflopCommons** (ver [Regras comuns](#regras-comuns-flipnflopcommons)).

---

## 3. Estrutura de Pastas do Repositório

```text
FlipNflop/
├── Assets/
│   ├── .agents/          # Arquivos de contexto e regras mandatória para Assistentes de IA
│   ├── Animations/       # Animações de personagem e objetos do jogo
│   ├── Arts/             # Sprites 2D, texturas e elementos visuais
│   ├── Materials/        # Materiais URP e Shaders
│   ├── Plugins/          # DLLs de terceiros e a biblioteca comum FlipNflopCommons (Plugins/FlipNflopCommons/)
│   ├── Prefab/           # Prefabs (Jogador, Obstáculos, UI Elementos)
│   ├── Resources/        # Fases JSON nativas, a tela de entrada do GLA (UI/) e recursos carregáveis via script
│   ├── Scenes/           # Cenas Unity (MainMenu, GameScene)
│   ├── Scripts/          # Código-fonte C# organizado modularmente
│   ├── StreamingAssets/  # gla-config.json: URL da API e liga/desliga da coleta do GLA, editável depois do build
│   ├── Tests/Gla/        # Testes EditMode do GLA (Core/: identificação e sessão) e exemplos canônicos do contrato (Contract/)
│   ├── UI Toolkit/       # Documentos de UI (.uxml) e estilos (.uss)
│   └── link.xml          # Protege os tipos do FlipNflopCommons do stripping do IL2CPP
├── scripts/              # update-commons.sh (atualiza a biblioteca comum) e set_unity_version.py
├── Docs/                 # Documentação em Markdown para desenvolvedores
├── Packages/             # Pacotes de dependência do Unity (Input System, URP, Localization)
└── ProjectSettings/      # Configurações do projeto Unity
```

---

## 4. Arquitetura de Código (`Assets/Scripts/`)

A pasta `Assets/Scripts/` é dividida em 6 subdomínios totalmente modulares:

```text
Assets/Scripts/
├── Common/       # Utilitários compartilhados (Controle de Câmera, Captura de Tela)
├── Gla/          # Game Learning Analytics: GlaServices e tela de entrada; Core/ (identificação e sessão); Serialization/ (GlaJson)
├── Hint/         # Sistema de dicas visuais em tempo real (Linhas de Clock, Overlays)
├── Level/        # Dados da fase, simulação de Flip-Flops, carregamento e Tilemaps
│   ├── Loading/  # Leitura de JSON, sequência de fases e interop WebGL
│   ├── Obstacles/# Spawning e patrulha de obstáculos (Maces giratórias)
│   └── Rendering/# Renderização de terrenos, rótulos de sinais e efeito Parallax
├── Menus/        # Interfaces UI Toolkit (Menu Principal, Pausa, Resultados)
├── Player/       # Física do jogador, geração do rastro, gabarito e cálculo de pontuação
└── Settings/     # Configurações de cores acessíveis, áudio, vídeo e remapeamento de teclas
```

### Detalhamento dos Componentes Chave

| Módulo | Script Principal | Responsabilidade |
| :--- | :--- | :--- |
| **Level** | [`FlipFlopSimulator.cs`](file:///D:/Projetos/FlipNflop/Assets/Scripts/Level/FlipFlopSimulator.cs) | Núcleo matemático/lógico. Simula transições síncronas (borda de clock) e assíncronas (PRE/CLR). O J-K delega ao `JkSimulator` do commons. |
| **Level** | [`LevelJsonLoader.cs`](file:///D:/Projetos/FlipNflop/Assets/Scripts/Level/Loading/LevelJsonLoader.cs) | Carrega o JSON da fase, invoca o simulador e aciona a renderização dos Tilemaps e Rótulos. |
| **Level** | [`TilemapRenderer.cs`](file:///D:/Projetos/FlipNflop/Assets/Scripts/Level/Rendering/TilemapRenderer.cs) | Converte padrões de sinais (3 bits) nos tiles gráficos correspondentes do cenário. |
| **Player** | [`PlayerController.cs`](file:///D:/Projetos/FlipNflop/Assets/Scripts/Player/PlayerController.cs) | Controla físicas, gravidade, pulo, dash, colisões com inimigos e animações do personagem. |
| **Player** | [`GabaritoGenerator.cs`](file:///D:/Projetos/FlipNflop/Assets/Scripts/Player/GabaritoGenerator.cs) | Gera as quinas do gabarito no mundo Unity com o `AnswerKeyCorners` do commons. |
| **Player** | [`PathVerifier.cs`](file:///D:/Projetos/FlipNflop/Assets/Scripts/Player/PathVerifier.cs) | Compara o trajeto feito pelo jogador contra o gabarito ideal, gera feedback gráfico (verde/vermelho) e publica o resultado da validação (`ValidationCompleted`). |
| **Player** | [`ScoreController.cs`](file:///D:/Projetos/FlipNflop/Assets/Scripts/Player/ScoreController.cs) | Avalia a taxa de acerto do trajeto, aplica penalidades de tempo e calcula a pontuação final (`ScoreRules` do commons). |
| **Settings** | [`SignalColorManager.cs`](file:///D:/Projetos/FlipNflop/Assets/Scripts/Settings/SignalColorManager.cs) | Gerencia a paleta de cores dos sinais lógicos (suporta acessibilidade/daltonismo). |

### Regras comuns (FlipNflopCommons)

O jogo, a API e o script de dados sintéticos calculam o gabarito, a validação do traçado e a pontuação com o mesmo código: o *namespace* `FlipNflop.Commons.PhaseRules` da biblioteca comum `FlipNflopCommons` (repositório `fnf-commons`). Os scripts do jogo são adaptadores finos, que convertem `Vector3` em `PathPoint` e chamam o commons, mantendo as assinaturas e os campos serializados:

| Script do jogo | Regra do commons |
| :--- | :--- |
| `FlipFlopSimulator.SimulateJK` | `JkSimulator` (só J-K, passo de clock 6) |
| `GabaritoGenerator.BuildFromEvents` | `AnswerKeyCorners`, com a fase do `LevelJsonLoader` (`CreatePhaseDefinition`) |
| `PathChecker.Evaluate` | `CornerEvaluator` (diagrama correto e acerto geral) |
| `PathVerifier.IsSegmentValid` | `SegmentValidator` (verde ou vermelho) |
| `ScoreController.ComputeScore` | `ScoreRules`, com o `maxScore` do componente |
| `PathVerifier` (resultado por trecho) | `ColumnEvaluator` (nível traçado e acerto por trecho) |

A geometria vem da cena `Custom.unity`: início da área jogável em x = 0, `startX` = 4 do `LevelJsonLoader`, níveis −0,5 e 3,25 e **tolerância de 0,2** (`cornerTolerance` do `PathVerifier`; o padrão do código é 1,0, mas vale o valor da cena), com a folga do `LevelManager`.

**Resultado da validação.** Ao fim de cada tentativa concluída ou encerrada por morte, `PathVerifier` dispara o evento estático `PathVerifier.ValidationCompleted` com um `TraceValidationResult`: `Conclusion` (diagrama correto), `OverallAccuracy` (segmentos cobertos / total), `Performance` (a pontuação mostrada na tela), `Trace` e `Correct` (um valor por trecho, `null` onde não há traçado). Traçado com menos de 2 pontos gera `Conclusion = false`, pontuação 0, acerto 0 e vetores só com `null`. É um gancho de lógica para a coleta: quem assina deve se desinscrever em `OnDisable`, e o evento é zerado ao iniciar o *Play Mode*. Em *Development Build*, o resultado é impresso no console.

**Serialização do contrato.** O `GlaJson` (`Assets/Scripts/Gla/Serialization/`, *assembly* `FlipNflop.Gla.Serialization`) escreve e lê os tipos do contrato do commons com o Newtonsoft.Json, nos nomes de `ContractNames`, com `null` explícito, a causa em minúsculas e as datas em UTC com `Z`. Os testes EditMode (`Assets/Tests/Gla/`) o conferem contra os exemplos canônicos.

**Como atualizar a biblioteca comum.** Com o `fnf-commons` ao lado (no monorepo, `../fnf-commons`) e o .NET SDK instalado, rode na raiz do jogo:

```bash
scripts/update-commons.sh [caminho-do-fnf-commons]
```

O script compila o commons para `netstandard2.1`, copia `FlipNflopCommons.dll`, a documentação XML e um `VERSION` (versão e commit de origem) para `Assets/Plugins/FlipNflopCommons/`, e os exemplos canônicos do contrato para `Assets/Tests/Gla/Contract/`. Depois, abra o projeto no editor para importar o DLL e rode os testes EditMode (*Window > General > Test Runner*). O `Assets/link.xml` impede que o IL2CPP remova os tipos do DLL no build WebGL.

### Identificação do jogador (GLA)

Ao abrir o jogo, o estudante escolhe entre informar o e-mail institucional, o que é aceitar o registro das jogadas, e jogar como convidado, podendo recusar o registro. Tudo é criado por código, sem editar cena:

- **`GlaServices`** nasce antes da primeira cena (`RuntimeInitializeOnLoadMethod`), fica entre as cenas (`DontDestroyOnLoad`) e grava o instante do carregamento da página (`PageLoadedAt`), que é o `started_at` de **toda** sessão aberta naquele carregamento.
- **`gla-config.json`** (`Assets/StreamingAssets/`) traz `api_base_url` e `collection_enabled`. Pode ser editado no build publicado, sem compilar de novo. Enquanto ele é lido, o menu fica coberto por uma tela de espera. Coleta desligada, arquivo ausente ou inválido, ou leitura que passa de 5 segundos: o jogo abre como na v0.15.0, sem tela de entrada e sem nenhuma requisição à API.
- **Tela de entrada** (`Assets/Resources/UI/Identification.uxml`, controlada por `IdentificationScreenController`): e-mail (opcional) → "Receber código" → código → "Confirmar", com "Reenviar código", "Voltar" e "Entrar como convidado". O texto diz: "Ao informar seu e-mail institucional, você aceita que suas jogadas sejam registradas e vistas pelo seu professor." Os erros mostram a mensagem em português da API; sem resposta, uma mensagem que sugere tentar de novo ou entrar como convidado.
- **Convidado:** o jogo pergunta se ele aceita o registro anônimo. Quem aceita abre uma sessão de convidado; quem recusa joga normalmente, e nada é enviado até a página ser recarregada.
- **Credencial:** depois da verificação, o navegador guarda só `gla.credential` e `gla.credential_expires_at` (4 horas), em `PlayerPrefs` (IndexedDB no WebGL). O e-mail não é guardado, e o token da sessão fica só na memória. Com a credencial válida, a recarga pula a tela; vencida, ou recusada pela API (`invalid_credential`), ela é apagada e a tela volta. O botão "Identificado — Trocar e-mail" (ou "Convidado — Identificar-me") do menu apaga a credencial e o token e mostra a tela de novo.
- **Sessão (`PlayerSession`):** `POST /sessions` com a versão do jogo, a credencial (ou `null`) e `started_at`. Sem rede, com *timeout* (10 s), 429 ou 5xx, ela fica `Pending` e é aberta mais tarde, antes de enviar uma tentativa (gla-15). Recusada com 400 (relógio do computador adiantado mais de 5 minutos), ela fica `Rejected`: o jogo segue jogável, não tenta de novo, grava a recusa no log e não monta nem envia registros até a página ser recarregada.

```mermaid
sequenceDiagram
    autonumber
    actor Estudante
    participant Menu as MenuManager / IdentificationScreenController
    participant Gla as GlaServices / PlayerSession
    participant API as API do GLA

    Gla->>Gla: PageLoadedAt e leitura do gla-config.json (até 5 s)
    alt credencial válida no navegador
        Gla->>API: POST /sessions (credencial, started_at = PageLoadedAt)
        Menu-->>Estudante: menu direto, "Identificado — Trocar e-mail"
    else sem credencial
        Menu-->>Estudante: tela de entrada
        Estudante->>Menu: e-mail institucional
        Menu->>Gla: Receber código
        Gla->>API: POST /identification/code-requests
        Estudante->>Menu: código recebido
        Menu->>Gla: Confirmar
        Gla->>API: POST /identification/code-verifications
        API-->>Gla: credencial (guardada por 4 h)
        Gla->>API: POST /sessions (credencial, started_at = PageLoadedAt)
    else convidado que aceita o registro
        Gla->>API: POST /sessions (credential = null, started_at = PageLoadedAt)
    end
    API-->>Gla: session_token (só em memória)
```

---

## 5. Fluxo de Execução de uma Fase

Abaixo está o ciclo de vida completo de uma fase no jogo, desde o clique no menu até a tela de resultados:

```mermaid
sequenceDiagram
    autonumber
    actor Jogador
    participant Menu as MenuManager / UI
    participant Loader as LevelJsonLoader
    participant Sim as FlipFlopSimulator
    participant Render as Tilemap & Signal Renderers
    participant Play as Player & SignalPath
    participant Eval as PathVerifier & ScoreController

    Jogador->>Menu: Seleciona ou carrega fase (JSON)
    Menu->>Loader: Inicia carregamento da fase
    Loader->>Sim: Passa os sinais de entrada (J, K, PRE, CLR)
    Sim-->>Loader: Retorna a linha do tempo calculada do sinal Q
    Loader->>Render: Constrói Tilemaps e Rótulos na tela
    Loader->>Play: Posiciona o Jogador no início (X=0)
    
    loop Durante a corrida
        Jogador->>Play: Comanda Pulo / Inversão de Gravidade / Dash
        Play->>Play: Registra pontos do trajeto via LineRenderer
    end

    Jogador->>Play: Atinge a linha final da fase
    Play->>Eval: Envia pontos percorridos pelo jogador
    Eval->>Eval: Compara quinas do trajeto com o Gabarito ideal
    Eval->>Jogador: Desenha linhas de feedback (Verde = Acerto, Vermelho = Erro)
    Eval->>Eval: Publica ValidationCompleted (TraceValidationResult) para a coleta
    Eval->>Menu: Exibe ResultScreenController (Pontuação, Tempo, Precisão)
```

---

## 6. Integração WebGL e Customização

O **Flip'n Flop** foi construído com suporte nativo a builds **WebGL** para execução em navegadores:
- **Upload Dinâmico de Fases:** Em builds WebGL, o jogador pode carregar arquivos `.json` diretamente do seu computador usando a ponte WebGL nativa (`UploadMenuManager.cs`).
- **Configuração do GLA depois do build:** o `StreamingAssets/gla-config.json` fica ao lado do build e pode ser editado para apontar o jogo para a API da hospedagem (`api_base_url`) ou desligar a coleta (`collection_enabled`). A API precisa autorizar a origem do jogo em `Cors:AllowedOrigins`.
- **Internacionalização (i18n):** O jogo utiliza o pacote **Unity Localization** para alternar facilmente os textos entre Português e Inglês nas telas de UI Toolkit e nas descrições.

---

## 7. Padrões de Código e Boas Práticas

Todos os scripts C# do repositório seguem rigorosamente o padrão documentado em **[`Assets/.agents/csharp_coding_standards.md`](file:///D:/Projetos/FlipNflop/Assets/.agents/csharp_coding_standards.md)**:

1. **Documentação XML em Inglês (`/// <summary>`)**:
   - Todo método e classe possui comentário descritivo em inglês.
   - Resumos explicitam dependências e conexões externas com outros scripts.
2. **Ausência de Comentários em Português**:
   - Nenhum comentário em português deve permanecer no código C#.
   - Comentários in-line no meio dos métodos são mantidos apenas quando estritamente necessários.
3. **Atributos de Inspetor em Inglês**:
   - Todos os atributos `[Header("...")]` e `[Tooltip("...")]` devem ser em inglês.
4. **Organização por Blocos `#region`**:
   - Scripts devem utilizar `#region` e `#endregion` nomeados em inglês para agrupar campos, métodos do ciclo de vida Unity, APIs públicas e auxiliares.

---

## 8. Documentações Relacionadas

Para mais detalhes sobre aspectos específicos do projeto, consulte:

- 🧩 **[Como Criar Seus Próprios Níveis (`CriandoNiveis.md`)](file:///D:/Projetos/FlipNflop/Docs/CriandoNiveis.md)**: Guia completo para criação e sintaxe dos arquivos JSON de fases.
- 🔄 **[Fluxo de Versionamento e Releases (`Versionamento.md`)](file:///D:/Projetos/FlipNflop/Docs/Versionamento.md)**: Padrão Git Flow, Conventional Commits e automações do GitHub Actions.
- ⚙️ **[Diretrizes C# para Agents (`csharp_coding_standards.md`)](file:///D:/Projetos/FlipNflop/Assets/.agents/csharp_coding_standards.md)**: Normas detalhadas de escrita de código C#.

---

> *Em caso de dúvidas ou sugestões de melhoria nesta documentação, abra uma issue ou envie um pull request seguindo as diretrizes de versionamento do projeto.*
