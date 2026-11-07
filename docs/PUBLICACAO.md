# Publicação: VirusTotal, assinatura e divulgação

Passos manuais do dono do projeto. Nada aqui é feito automaticamente pelo build.

## Nova versão (resumo)
1. Suba a `<Version>` em `src/PZModManager.csproj` e atualize `.github/release-notes.md`.
2. Commit, `git push`, depois `git tag vX.Y.Z` e `git push origin vX.Y.Z`.
3. O workflow [`release.yml`](../.github/workflows/release.yml) compila, gera o atestado de proveniência e publica `PZModManager-win-x64.zip` + `.sha256` na Release da tag. Acompanhe com `gh run watch`.
4. Faça o passo do VirusTotal abaixo.

## (a) VirusTotal
1. Baixe o zip da Release: `https://github.com/Luc4sL4mou/PZModManager/releases/latest/download/PZModManager-win-x64.zip`.
2. Confira o hash: `Get-FileHash PZModManager-win-x64.zip -Algorithm SHA256` tem de bater com o `.sha256` da Release.
3. Abra https://www.virustotal.com/gui/home/upload, clique em **Choose file** e envie o zip (sem conta funciona; com conta dá para comentar).
4. Espere a análise terminar. O endereço da página fica `https://www.virustotal.com/gui/file/<sha256>` (o mesmo SHA-256 do passo 2, em minúsculas).
5. Edite a Release (`gh release edit vX.Y.Z --notes-file notas.md` ou pelo site) e acrescente nas duas línguas:
   - `Análise no VirusTotal: https://www.virustotal.com/gui/file/<sha256>`
   - `VirusTotal scan: https://www.virustotal.com/gui/file/<sha256>`
6. Alguns antivírus podem acusar falso positivo em exe novo e sem assinatura. Se acontecer, envie o arquivo como falso positivo pelo site do fabricante (Microsoft: https://www.microsoft.com/wdsi/filesubmission) e cite o link do VirusTotal.

## (b) Assinatura gratuita no SignPath Foundation
A assinatura tira o aviso "O Windows protegeu o computador" com o tempo (a reputação do certificado vai crescendo).

**Requisitos** (confira os atuais em https://about.signpath.io/product/open-source e https://signpath.org/terms):
- Licença aprovada pela OSI: MIT ([`LICENSE`](../LICENSE)). OK.
- Repositório público no GitHub. OK.
- Build no CI a partir do código do repositório: GitHub Actions ([`release.yml`](../.github/workflows/release.yml)). OK.
- MFA (autenticação em dois fatores) ligado na conta do GitHub de quem tem acesso de escrita.
- Texto de crédito nas releases e no README, por exemplo: "Free code signing provided by SignPath.io, certificate by SignPath Foundation".
- Projeto já publicado e mantido (ter algumas releases ajuda).

**Pedido**
1. Ligue o MFA no GitHub (Settings → Password and authentication).
2. Preencha o formulário de projeto open source em https://about.signpath.io/product/open-source (nome, repositório, descrição, como o build é feito).
3. Aguarde a aprovação. Eles criam a organização no SignPath e passam o `organization-id`, o `project-slug` e a `signing-policy-slug`.
4. No SignPath, conecte o repositório ao GitHub (Trusted Build System: GitHub.com) e crie um API token para o CI.

**O que mudar no workflow depois de aprovado**
1. No GitHub: Settings → Secrets and variables → Actions → crie o secret `SIGNPATH_API_TOKEN`.
2. Em `release.yml`, depois de compilar e antes de gerar o zip final:
   - enviar o `PZModManager.exe` como artefato do run (`actions/upload-artifact`, fixado por SHA) e pegar o `artifact-id`;
   - chamar `signpath/github-action-submit-signing-request` (fixado por SHA) com `api-token`, `organization-id`, `project-slug`, `signing-policy-slug`, `github-artifact-id`, `wait-for-completion: true` e `output-artifact-directory`;
   - montar o zip com o exe assinado (separar o `Compress-Archive` do `release.ps1` em um passo próprio ou passar um parâmetro para ele pular o zip);
   - manter o atestado e o upload da Release como estão (o atestado passa a cobrir o zip com o exe assinado).
3. Acrescente o texto de crédito do SignPath nas notas da Release e no README.

## (c) Checklist de divulgação
Faça em ordem e só avance quando a etapa anterior estiver tranquila.

- [ ] Release com zip, `.sha256`, atestado e link do VirusTotal nas notas.
- [ ] Testar o zip baixado da Release num Windows limpo (ou outro PC), do zero até abrir o jogo com o Viewpoint.
- [ ] **Criadores primeiro:** mandar mensagem educada para ellu e norkus (Project Viewpoint) e para zed_0xff (ZombieBuddy), com o link do repositório, explicando que o app não redistribui os arquivos deles, só usa o Workshop. Pedir permissão/opinião antes de divulgar com o nome dos mods, e ajustar o que pedirem.
- [ ] **r/projectzomboid:** post curto com o GIF, o que o app faz, aviso do SmartScreen, links do repositório e do VirusTotal. Seguir as regras do subreddit (flair de mod/ferramenta).
- [ ] **Fórum da The Indie Stone** (seção de mods/ferramentas): mesmo texto, deixando claro que é não oficial.
- [ ] **Discords BR** de Project Zomboid: texto em português, com o README em pt-BR.
- [ ] Responder dúvidas e bugs (Issues no GitHub) e soltar correções rápidas nas primeiras semanas.
- [ ] **Streamers/criadores de conteúdo:** só depois de algumas semanas estável, sem bugs graves abertos.
