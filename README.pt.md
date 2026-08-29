<p align="center">
  <img src="src/WinCopyQueue.App/Assets/logo-full.png" alt="WinCopyQueue" width="420">
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.pl.md">Polski</a> · <a href="README.de.md">Deutsch</a> · <a href="README.fr.md">Français</a> · <a href="README.es.md">Español</a> · <strong>Português</strong> · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a>
</p>

# WinCopyQueue

WinCopyQueue adiciona ao Explorador do Windows uma fila simples para copiar e mover ficheiros. Em vez de executar várias transferências ao mesmo tempo, processa-as de forma sequencial — uma sessão após a outra e um ficheiro de cada vez.

A aplicação funciona na área de notificação e não mantém uma janela principal aberta permanentemente. O painel compacto da fila aparece apenas quando uma transferência é adicionada, pode ser ocultado a qualquer momento e as operações continuam em segundo plano.

## Download

Versão atual: **1.0.0**

- [Transferir o instalador WinCopyQueue 1.0.0](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue-Setup-1.0.0-x64.exe)
- [Transferir WinCopyQueue.exe autónomo](https://github.com/quendae/WinCopyQueue/releases/download/v1.0.0/WinCopyQueue.exe)
- [Ver a versão v1.0.0](https://github.com/quendae/WinCopyQueue/releases/tag/v1.0.0)

WinCopyQueue funciona no **Windows 10 1809 ou posterior**, incluindo o Windows 11. O instalador é por utilizador e não requer privilégios de administrador.

> Este repositório não inclui atualmente um ficheiro `LICENSE`.

## Como funciona

1. Inicie `WinCopyQueue.exe`.
2. No Explorador do Windows, copie ou corte ficheiros normalmente com `Ctrl+C` / `Ctrl+X`.
3. Na pasta de destino, prima `Ctrl+V` ou escolha **Colar com WinCopyQueue** no menu de contexto.

Se já existir uma transferência em curso, a seguinte é simplesmente adicionada ao fim da fila. Assim, várias operações grandes não competem ao mesmo tempo pelo mesmo disco.

No Windows 11, a entrada estática do menu de contexto pode aparecer em **Mostrar mais opções**.

<p align="center">
  <img src="docs/images/WinCopyQueue_screenshot.png" alt="WinCopyQueue durante uma transferência ativa" width="480">
</p>

## Principais funcionalidades

- copiar e mover ficheiros individuais ou pastas completas,
- várias sessões independentes numa única fila sequencial,
- pausar e retomar toda a fila ou ficheiros individuais,
- cancelar uma sessão inteira ou um ficheiro selecionado,
- cancelar uma sessão sem remover ficheiros que já tenham sido copiados com sucesso,
- gestão de conflitos com comparação de caminho, tamanho e data de modificação,
- opções **Substituir**, **Ignorar** e **Cancelar sessão**, com possibilidade de aplicar a escolha a conflitos seguintes,
- painel compacto com ficheiro atual, progresso, número de ficheiros e velocidade de transferência,
- lista virtualizada e expansível de todos os ficheiros e respetivos estados,
- histórico de sessões concluídas, canceladas e com erro,
- notificações do sistema quando uma transferência é adicionada, concluída ou falha,
- arranque automático opcional com o Windows,
- oito idiomas de interface: inglês, polaco, alemão, francês, espanhol, português, chinês simplificado e japonês.

## Cópias e movimentos mais seguros

WinCopyQueue não grava um ficheiro incompleto diretamente com o nome final. Os dados são primeiro gravados num ficheiro temporário `*.queue-part-*` e só são publicados com o nome definitivo depois de a transferência terminar corretamente.

Para cópias normais, pode ser ativada uma verificação opcional **SHA-256**. WinCopyQueue calcula o hash da origem durante a cópia e volta a ler o destino para comparar o resultado.

Ao mover ficheiros entre volumes diferentes, a verificação é executada automaticamente antes de a origem ser eliminada, independentemente da definição escolhida na interface. Se a cópia, a verificação ou a finalização falhar, a origem permanece intacta.

## Painel da fila e área de notificação

O painel da fila abre automaticamente quando uma transferência é adicionada e aparece junto ao canto inferior direito do ecrã sem retirar o foco ao Explorador. Pode ser minimizado enquanto as transferências continuam em segundo plano.

Faça duplo clique no ícone da área de notificação ou escolha **Mostrar fila** para voltar a abrir o painel. O menu também permite pausar ou retomar toda a fila, ativar ou desativar o arranque automático, reparar a integração com o Explorador e sair da aplicação.

## Conflitos de ficheiros

Se já existir no destino um ficheiro com o mesmo nome, WinCopyQueue mostra ambos os ficheiros, incluindo os respetivos tamanhos e datas de modificação. Estão disponíveis três ações:

- **Substituir**,
- **Ignorar**,
- **Cancelar sessão**.

Substituir ou Ignorar também pode ser aplicado a todos os conflitos seguintes da mesma sessão.

## Definições e diagnóstico

As definições do utilizador são guardadas em:

```text
%LOCALAPPDATA%\WinCopyQueue\settings.json
```

O registo de diagnóstico encontra-se em:

```text
%LOCALAPPDATA%\WinCopyQueue\WinCopyQueue.log
```

O idioma escolhido e a preferência de verificação SHA-256 são guardados entre execuções.

## Linha de comandos

WinCopyQueue também pode receber transferências diretamente pela linha de comandos:

```powershell
WinCopyQueue.exe --copy "D:\Destino" "D:\Ficheiro.txt" "D:\Pasta"
WinCopyQueue.exe --move "D:\Destino" "D:\Ficheiro.txt"
WinCopyQueue.exe --paste "D:\Destino"
```

Abrir novamente a aplicação não cria uma segunda fila. Os comandos são encaminhados para o processo principal através de uma named pipe.

## Compilar o projeto

É necessário o .NET 10 SDK.

```powershell
dotnet restore WinCopyQueue.slnx --configfile NuGet.Config
dotnet build WinCopyQueue.slnx --no-restore -c Release
```

Executar a aplicação a partir do repositório:

```powershell
dotnet run --project src\WinCopyQueue.App\WinCopyQueue.App.csproj --no-restore
```

### Testes

```powershell
dotnet run --project tests\WinCopyQueue.Core.SmokeTests\WinCopyQueue.Core.SmokeTests.csproj --no-build -c Release
dotnet run --project tests\WinCopyQueue.App.SmokeTests\WinCopyQueue.App.SmokeTests.csproj --no-build -c Release
```

Os smoke tests do núcleo executam operações reais em ficheiros temporários isolados e verificam, entre outras coisas, a ordem das sessões, conflitos, SHA-256, pausa/retoma, cancelamento, limpeza do histórico e controlos por ficheiro. Os testes da aplicação abrangem WPF, localização, diálogo de conflitos e cenários de encerramento.

### Instalador

Criar o instalador com:

```powershell
.\installer\Build-Installer.ps1
```

O script publica uma compilação autónoma `win-x64` e cria um instalador com Inno Setup 7. Os binários finais são publicados em [Releases](https://github.com/quendae/WinCopyQueue/releases) e não ficam armazenados no repositório.

## Estrutura do projeto

```text
src/WinCopyQueue.Core/       lógica da fila e operações de ficheiros
src/WinCopyQueue.App/        aplicação WPF, área de notificação e integração com Explorer
tests/                       smoke tests do núcleo e da aplicação
installer/                   definição do Inno Setup e script de compilação
```
