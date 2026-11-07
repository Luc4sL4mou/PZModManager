<p align="center"><a href="README.md">English</a> | <b>Português</b></p>

<p align="center">
  <img src="docs/img/icon.svg" width="120" alt="Ícone do PZ Mod Manager: metade zumbi, metade robô">
</p>

<h1 align="center">PZ Mod Manager</h1>

<p align="center">
  Gerenciador de mods para <b>Project Zomboid (Build 42)</b> no Windows.<br>
  Ativa mods com as dependências, mostra conflitos e mods obsoletos, configura o <b>Project Viewpoint</b> em um clique e ajusta a memória RAM do jogo.
</p>

<p align="center">
  <img src="docs/img/pt-BR/painel.png" width="860" alt="Painel do PZ Mod Manager">
</p>

> A interface tem **inglês** (padrão) e **português do Brasil**: troque pela chave **EN | PT** no topo da janela.

## O que ele faz

### Memória do jogo (RAM)
O Project Zomboid vem limitado a **3 GB de RAM**. Com muitos mods, mapas grandes ou mods Java, o jogo trava ou fecha ao carregar o mundo (`OutOfMemoryError`). O app estima quanto os seus mods ativos precisam e mostra se o valor escolhido é **Insuficiente**, **Apertado**, **Bom** ou se deixa pouca RAM para o Windows. A mudança só é gravada quando você clica em **Aplicar**.

<p align="center"><img src="docs/img/pt-BR/memoria.gif" width="860" alt="Pop-up sobre a RAM e barra de memória sendo ajustada"></p>

### Conflitos entre mods
Mostra quando dois mods ativos mexem na mesma coisa e qual deles vence (o que carrega por último). Ele compara arquivos de código, itens, veículos, receitas, modelos 3D e células de mapa. Cada caso vem explicado em português e com botões para **escolher quem vence**, **desativar** um dos mods ou **ignorar** o alerta. Patches e dependências são reconhecidos como **Esperados**, e há um mapa visual dos cruzamentos.

<p align="center"><img src="docs/img/pt-BR/conflitos.gif" width="860" alt="Aba Conflitos com cartões e mapa visual"></p>

### Mods, dependências e obsoletos
- Ativa e desativa mods por perfil: **novo jogo**, cada **save** e cada **servidor** que você hospeda (`Mods=`, `WorkshopItems=` e `Map=` atualizados juntos).
- Ativa as dependências junto, avisa o que falta e abre a página do Workshop para você se inscrever.
- Ajusta a **ordem de carregamento** respeitando `require`, `loadModAfter` e `loadModBefore`.
- Marca mods **obsoletos** (só B41, removidos do Workshop, feitos para uma versão anterior do jogo ou marcados pelo autor como descontinuados) e os que **podem ficar obsoletos** em breve.
- Busca, filtros e o botão **Corrigir automaticamente**.

<p align="center"><img src="docs/img/pt-BR/mods.gif" width="860" alt="Aba Mods com busca, filtro de obsoletos e detalhes"></p>

### Project Viewpoint em um clique
Inscreve no **ZombieBuddy** e no **Project Viewpoint**, instala o ZombieBuddy na pasta do jogo e ativa os dois no perfil escolhido. A aba **Recomendados** lista os addons do Viewpoint mais usados, direto do Workshop.

## Download

<p align="center">
  <a href="https://github.com/Luc4sL4mou/PZModManager/releases/latest/download/PZModManager-win-x64.zip"><b>⬇ Baixar PZ Mod Manager (Windows, .zip)</b></a><br>
  <sub><a href="https://github.com/Luc4sL4mou/PZModManager/releases/latest">ver todas as versões e o SHA-256</a></sub>
</p>

1. Baixe o `.zip` acima.
2. Extraia em qualquer pasta (clique com o botão direito → **Extrair tudo**).
3. Abra o **`PZModManager.exe`**. Não precisa instalar nada.

Ele encontra sozinho a Steam, o jogo, a pasta do Workshop e a pasta `Zomboid`. Depois, escolha o perfil no topo (novo jogo, um save ou um servidor) e ative os mods na aba **Mods**.

> **"O Windows protegeu o computador"?** O app ainda não tem assinatura digital, então o Windows mostra esse aviso na primeira vez que ele é aberto. Clique em **Mais informações → Executar assim mesmo**. O código é aberto, e o SHA-256 de cada versão está na página da Release.

**Requisitos:** Windows 10 ou 11 (64 bits), Project Zomboid **Build 42** pela Steam e o WebView2 Runtime, que já vem no Windows 11 e com o Edge. O .NET já vai dentro do exe.

As configurações e os backups do app ficam na pasta `dados`, ao lado do exe. Para "desinstalar", é só apagar a pasta.

## O que ele altera no seu PC
| Arquivo | Quando |
|---|---|
| `Zomboid\mods\default.txt`, `Saves\...\mods.txt`, `Server\*.ini` | Ao ativar ou desativar mods |
| `ProjectZomboid64.json` / `.bat` (pasta do jogo) | Ao aplicar a memória ou instalar o ZombieBuddy (`-Xmx`, `-agentlib:zbNative`) |
| `zbNative.dll`, `ZombieBuddy.jar` (pasta do jogo) | Ao instalar o ZombieBuddy |

- Antes de alterar qualquer um deles, o app guarda uma cópia em `dados\backups\AAAA-MM-DD\`.
- O botão **Restaurar originais** (Painel → Instalação) volta o `ProjectZomboid64.json` e o `.bat` para a cópia de antes da primeira alteração do app. Isso tira a configuração do ZombieBuddy e da memória.
- A gravação é feita primeiro num arquivo temporário, que só depois substitui o original. Assim, uma queda no meio não corrompe o arquivo.
- Com o jogo aberto, o app não mexe nesses arquivos.
- O app não envia nenhum dado seu. Ele só consulta a API pública e as páginas do Steam Workshop, para títulos, imagens e itens obrigatórios.

> **Mods Java** (como o Viewpoint) rodam com acesso total ao PC. O ZombieBuddy pergunta antes de carregar cada um, e o app mantém essa proteção ligada. Use apenas mods em que você confia.

## Segurança
O projeto passou por um pentest com duas rodadas, e todos os achados foram corrigidos e retestados. O relatório está em [`docs/seguranca/pentest-2026-11-07.md`](docs/seguranca/pentest-2026-11-07.md).

## Publicação
O zip de cada Release é compilado pelo GitHub Actions a partir deste código ([`.github/workflows/release.yml`](.github/workflows/release.yml)), com atestado de proveniência. Para conferir que o arquivo baixado saiu deste repositório:
```powershell
gh attestation verify PZModManager-win-x64.zip --repo Luc4sL4mou/PZModManager
```
Passos de VirusTotal, assinatura (SignPath) e divulgação: [`docs/PUBLICACAO.md`](docs/PUBLICACAO.md).

## Para desenvolvedores
Precisa do [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
```powershell
powershell -ExecutionPolicy Bypass -File build.ps1    # exe rápido na raiz (usa o .NET instalado)
powershell -ExecutionPolicy Bypass -File release.ps1  # zip da Release em release\ (exe com o .NET dentro + SHA-256)
```

## Estrutura
```
src/
  Core/        detecção do jogo, leitura de mods, perfis, conflitos, memória, ZombieBuddy, Steam
  ui/          interface (HTML + JS, roda dentro do WebView2)
  MainForm.cs  janela e ponte entre a interface e o C#
docs/          imagens do README, relatório de segurança e guia de publicação
.github/       build automático da Release (GitHub Actions)
build.ps1      compila e copia o exe para a raiz
release.ps1    gera o zip da Release (exe self-contained + SHA-256)
```

## Créditos
- **Project Viewpoint**, de ellu e norkus: [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3809306528)
- **ZombieBuddy**, de zed_0xff: [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3619862853)
- **Project Zomboid** é da The Indie Stone.

Este é um projeto independente e não tem relação oficial com a The Indie Stone, com os autores do Project Viewpoint nem com os do ZombieBuddy.

## Licença
[MIT](LICENSE). O app não redistribui arquivos do ZombieBuddy nem do Viewpoint: eles vêm do Workshop, nos itens em que você se inscreve.
