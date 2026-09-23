# OrçaAI — referência para Segurança dos dados do Google Play

Esta referência descreve o comportamento da versão 1.0.0 atual. Revise novamente se o aplicativo passar a enviar orçamentos, clientes ou dados da empresa para um backend.

## Dados transmitidos para fora do dispositivo

O login é realizado pelo Dingous ChatTrade usando Google. Durante autenticação, o backend Dingous recebe e mantém os dados necessários para identificar a conta:

- nome;
- endereço de e-mail;
- identificador da conta Google;
- foto de perfil, quando fornecida pelo Google;
- token/sessão de autenticação e metadados técnicos necessários à segurança do acesso.

Uso principal: autenticação, gerenciamento da conta e segurança.

## Dados que permanecem somente no dispositivo nesta versão

Os seguintes dados do OrçaAI são persistidos localmente e não são sincronizados com o backend:

- clientes cadastrados no OrçaAI;
- telefone/e-mail/anotações dos clientes;
- orçamentos e itens;
- valores, descontos e observações;
- dados do profissional/empresa informados na tela Empresa;
- PDFs gerados pelo aplicativo.

Os dados locais são isolados por conta autenticada e o backup automático Android está desativado.

## Compartilhamento

O OrçaAI não vende dados pessoais. O login usa Google e a infraestrutura Dingous apenas para autenticação. Prestadores que atuam como operadores/processadores necessários ao serviço devem ser declarados conforme as regras do formulário do Google Play e a Política de Privacidade vigente.

## Segurança

- tráfego de autenticação somente por HTTPS;
- cleartext HTTP desativado no Android;
- token de sessão no SecureStorage;
- dados de negócio locais separados por conta;
- sem chaves privadas ou segredos do Google dentro do APK/AAB.

## Exclusão

Caminho no app:

Empresa → Privacidade e conta → Solicitar exclusão da conta

Recurso web público:

https://www.dingous.com.br/exclusao-de-conta

Política de Privacidade:

https://www.dingous.com.br/privacy-policy

## Lembrete para o Play Console

Preencha as respostas com base no comportamento da versão efetivamente enviada. Se qualquer SDK, analytics, crash reporter, publicidade, push ou sincronização for adicionado depois, este documento e o formulário Segurança dos dados precisam ser revistos.
