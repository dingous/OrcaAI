# OrçaAI — .NET MAUI 10

MVP multiplataforma para autônomos e pequenos negócios criarem orçamentos profissionais rapidamente.

## O que já está pronto

- Dashboard com clientes, orçamentos do mês, pendências e total aprovado.
- Cadastro, edição, busca e exclusão de clientes.
- Criação e edição de orçamentos.
- Gerador local de rascunho a partir de texto, sem API e sem custo por uso.
- Extração simples de quantidades da descrição, por exemplo: `trocar 3 tomadas e instalar 2 luminárias`.
- Itens editáveis, desconto, validade, observações e status.
- Persistência local em JSON no `FileSystem.AppDataDirectory`.
- Geração de PDF sem biblioteca paga e compartilhamento usando a API nativa do MAUI.
- Dados da empresa/profissional no PDF.
- Layout responsivo para Android/iOS e desktop, com largura máxima de conteúdo no desktop.
- Sem chave de IA dentro do aplicativo.

## Stack

- .NET 10
- .NET MAUI 10
- XAML + MVVM leve
- `System.Text.Json`
- `Microsoft.Maui.ApplicationModel.DataTransfer` para compartilhamento
- Sem banco externo, sem serviço pago e sem SDK de IA no MVP

## Executar

Pré-requisitos: .NET 10 SDK e workload .NET MAUI instalados.

```bash
cd src/OrcaAI
dotnet restore
```

Windows:

```bash
dotnet build -f net10.0-windows10.0.19041.0
```

Android:

```bash
dotnet build -f net10.0-android
```

Também é possível abrir `OrcaAI.sln` no Visual Studio com o workload .NET MAUI.

> Para compilar/assinar iOS é necessário o toolchain da Apple e, em desenvolvimento Windows, um Mac conectado.

## Arquitetura de IA

`IAiQuoteDraftService` é a abstração usada pela tela. Hoje ela aponta para `LocalAiQuoteDraftService`, que é gratuito e funciona offline.

Para produção com um LLM, mantenha essa interface e implemente um adaptador HTTP que converse com **seu backend**. O backend guarda a chave do provedor e retorna apenas um `QuoteDraft`. Não coloque segredo de OpenAI, Azure OpenAI ou outro provedor no app MAUI.

Fluxo recomendado:

```text
MAUI -> POST /api/quotes/draft -> seu backend -> modelo de IA
```

Assim você consegue aplicar autenticação, limites por plano, auditoria, cache e proteção da chave.

## Persistência

O arquivo `orcaai-data.json` é gravado na pasta privada do aplicativo. Para o MVP isso elimina custo de banco e permite uso offline. Na evolução comercial, a interface `IOrcaDataStore` pode ser trocada por sincronização com backend sem reescrever as telas.

## Próximos passos antes da loja

1. Definir nome jurídico/política de privacidade e ícones finais.
2. Criar assinatura do Android e perfis de distribuição Apple/Windows.
3. Adicionar autenticação/backend somente quando houver necessidade de sincronização ou plano pago.
4. Conectar uma IA remota via backend caso o gerador local não seja suficiente para o nicho validado.
5. Testar PDF e compartilhamento em dispositivos reais.

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
```
