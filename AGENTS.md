# AGENTS.md — fnf-game

Instruções para assistentes de IA de código, qualquer que seja a ferramenta.

Cliente de jogo do Flip n' Flop: Unity 6000.4, URP 2D, UI Toolkit, Input System e Localization, publicado em WebGL. O jogador percorre piso e teto, e o traçado desenha a saída Q de um flip-flop.

## Leia antes

- `Assets/.agents/context.md` — arquitetura de `Assets/Scripts/`, módulo por módulo.
- `Docs/CriandoNiveis.md` — formato atual dos arquivos de fase.
- Para integração com a API e dados de jogo: `fnf-docs/CLAUDE.md` (no monorepo, `../fnf-docs/`). O formato de fase e de log previsto lá é diferente do atual e está em `04-esquemas-json.md`.

## Padrão de código C#

**Obrigatório** em todo script de `Assets/Scripts/`: leia [`Assets/.agents/csharp_coding_standards.md`](Assets/.agents/csharp_coding_standards.md) antes de criar ou alterar código C#, e rode o checklist da seção 3 dele antes de concluir. Em resumo: documentação XML `/// <summary>` em inglês citando dependências externas, nenhum comentário em português, comentários dentro de métodos só quando indispensáveis, e nomes, `[Header]`, `[Tooltip]` e `#region` em inglês.

## Mapa rápido

- `Assets/Scripts/Level/` — `LevelData` (DTO com conversores Newtonsoft), `FlipFlopSimulator`, `LevelManager`, carregamento, obstáculos, renderização
- `Assets/Scripts/Player/` — movimento e gravidade, `SignalPath`, `GabaritoGenerator`, `PathChecker`/`PathVerifier`, `ScoreController`
- `Assets/Scripts/Menus/`, `Settings/`, `Hint/`, `Common/`
- `Assets/UI Toolkit/` — UXML e USS
- `Assets/Resources/Levels/` — fases embarcadas no modo história

## Integração com a API

O jogo ainda não fala com a API. Quando falar, seguindo `fnf-docs` e a fase 1 do roadmap:

- token de sessão só no cabeçalho `Authorization: Bearer`, guardado em `sessionStorage`, nunca em `localStorage`;
- encerramento em `pagehide`/`beforeunload` com `fetch(..., { keepalive: true })` — não em `visibilitychange` e não com `sendBeacon`;
- sinal de atividade a cada 2 minutos, e só se houve interação;
- `attemptId` gerado no cliente; sem fila local de envios;
- `401` no meio da fase não interrompe a partida: ela termina, o registro é descartado e o jogador é avisado;
- acerto medido **por coluna** (`correctColumns`/`totalColumns`); hoje o jogo mede por quinas, e a semântica final depende de Q11 e Q12 em `fnf-docs/01-requisitos.md`.

Código compartilhado com a API vai para o `fnf-commons`, não é duplicado aqui.

## Cuidados

- Não há Editor Unity neste ambiente: não é possível compilar nem rodar o jogo. Diga isso ao concluir e liste o que conferir no Editor.
- Não edite `.meta`, cenas, prefabs nem `ProjectSettings/` à mão sem necessidade clara: GUIDs quebrados perdem referências. Script novo tem o `.meta` gerado pelo Editor.
- Pense em WebGL: sem threads, rede por `UnityWebRequest` ou interop JavaScript (`.jslib`), atenção ao tamanho do build.

## Versionamento

Git Flow (`feature/*` a partir de `develop`) e *Conventional Commits*. A CI (`.github/workflows/release.yml`) gera changelog e versão a partir dos commits e atualiza o `bundleVersion` sozinha: não altere a versão manualmente.
