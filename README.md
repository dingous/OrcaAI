# OrçaAI — .NET MAUI 10

Aplicativo multiplataforma para profissionais autônomos e pequenos negócios criarem, organizarem e compartilharem orçamentos profissionais.

## Estado atual

- .NET MAUI 10.
- Android explicitamente direcionado ao Android 16 / API 36.
- Android mínimo: API 24.
- Application ID: `br.com.dingous.orcaai`.
- Login Google por meio do backend Dingous ChatTrade.
- Sessão protegida com `SecureStorage`.
- Dados de clientes e orçamentos locais, isolados por conta autenticada.
- Backup automático Android desativado para reduzir exposição de dados locais.
- HTTP sem TLS desativado no Android.
- Release Android configurado para gerar AAB.
- Geração local de PDF e compartilhamento nativo.
- Sem chave Google, senha ou segredo dentro do aplicativo.

## Executar

Pré-requisitos:

- .NET 10 SDK;
- workload .NET MAUI/Android;
- Android SDK com API 36.

```powershell
cd src\OrcaAI
dotnet restore
dotnet build -f net10.0-android36.0 -c Debug
```

Windows:

```powershell
dotnet build -f net10.0-windows10.0.19041.0 -c Debug
```

## Google Play

### 1. Criar a chave de upload

A chave fica fora do Git em `%LOCALAPPDATA%\Dingous\AndroidSigning\br.com.dingous.orcaai`.

```powershell
.\tools\Initialize-PlaySigning.ps1
```

A senha é armazenada usando DPAPI do Windows. Faça backup seguro do `upload.keystore` e da senha antes de trocar de computador.

### 2. Gerar AAB Release assinado

```powershell
.\tools\Publish-GooglePlay.ps1
```

O script usa `net10.0-android36.0`, Release, formato AAB e a chave local de upload. Nenhuma credencial é versionada.

A primeira versão deve ser enviada manualmente ao Google Play Console.

## Privacidade e exclusão de conta

O aplicativo contém caminhos visíveis para:

- Política de Privacidade: https://www.dingous.com.br/privacy-policy
- Exclusão de conta: https://www.dingous.com.br/exclusao-de-conta
- Termos de Serviço: https://www.dingous.com.br/term-service

A tela Empresa disponibiliza os três links.

Consulte:

- `docs/google-play-data-safety.md`
- `docs/google-play-listing.md`

## Persistência

Os dados de clientes, orçamentos e perfil comercial são armazenados localmente em JSON no `FileSystem.AppDataDirectory`. O arquivo é separado por conta Google autenticada.

O login utiliza o Dingous ChatTrade, mas a versão atual não sincroniza clientes, itens, preços ou PDFs com o backend.

## Arquitetura de IA

`IAiQuoteDraftService` continua sendo a abstração do gerador de rascunhos. A implementação atual funciona localmente e sem custo de API.

Caso uma IA remota seja usada no futuro, a chave deve permanecer no backend Dingous e nunca no aplicativo MAUI.

## Estrutura

```text
src/OrcaAI
├── Models
├── Services
├── ViewModels
├── Pages
├── Infrastructure
├── Resources
└── Platforms

docs/
tools/
```
