# Expense Control

Aplicação web para controle de finanças pessoais: cada usuário registra **despesas** e **receitas** por categoria, acompanha tudo em um **painel com gráficos**, define **orçamentos mensais** e exporta os dados.

Feita com **Blazor Server** (.NET 9) e **MySQL**.

## O que o sistema faz

- **Painel**: saldo, receitas, despesas e taxa de economia do mês (com variação em relação ao mês anterior); gráficos de receitas x despesas (6 meses), despesas por categoria e gastos acumulados ao longo do mês; orçamentos e lançamentos recentes. Navegação mês a mês. Cada gráfico tem uma **visão em tabela** (acessibilidade).
- **Lançamentos**: lista com filtros (período, tipo, categoria e busca por texto), ordenação, paginação, **edição**, **exclusão com confirmação** e **exportação CSV** (abre direto no Excel, com separador e decimais do idioma).
- **Orçamentos**: limite mensal por categoria de despesa, com barra de progresso que muda de cor (e de ícone e texto) ao se aproximar e ao ultrapassar o limite.
- **Português, inglês e espanhol**: troca na hora, sem recarregar; datas, números e meses seguem o idioma. O símbolo da moeda (`App:CurrencySymbol`, padrão `R$`) não muda com o idioma.
- **Modo claro e escuro**: respeita a preferência do sistema na primeira visita e lembra a escolha.
- Campos de senha com **ícone de olho** para ver o que foi digitado.
- Layout responsivo (celular, tablet e desktop).

## Como rodar

Pré-requisitos: [.NET SDK 9](https://dotnet.microsoft.com/download) e [Docker Desktop](https://www.docker.com/products/docker-desktop/) (aberto).

1. Suba o MySQL (container `expense-control-db`, dados guardados no volume `db-data`):

   ```bash
   docker compose up -d
   ```

2. Rode a aplicação:

   ```bash
   dotnet run --project src/ExpenseControl.Web
   ```

   Em desenvolvimento a conexão já vem configurada em `appsettings.Development.json`, apontando para esse container (as senhas ali valem só para o MySQL local, que escuta apenas em `127.0.0.1:3307`; a porta 3307 evita conflito com outro MySQL na 3306). Ao iniciar, a aplicação aplica as migrations pendentes. Abra `https://localhost:5001`.

Comandos úteis: `docker compose stop` (pausa), `docker compose down` (remove o container, mantém os dados) e `docker compose down -v` (remove também os dados).

**Produção:** não use essas credenciais. Defina a variável de ambiente `ConnectionStrings__DefaultConnection` (ou, no seu computador, `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project src/ExpenseControl.Web`, que tem prioridade sobre o `appsettings`).

### E-mail (recuperação de senha)

Por padrão (`EmailSettings:Enabled = false`) nenhum e-mail é enviado: em desenvolvimento, o link de redefinição aparece **no log do console**. Para enviar de verdade, configure o SMTP (no Gmail, use uma [senha de app](https://support.google.com/accounts/answer/185833)):

```bash
dotnet user-secrets set "EmailSettings:Enabled" "true" --project src/ExpenseControl.Web
dotnet user-secrets set "EmailSettings:FromEmail" "voce@gmail.com" --project src/ExpenseControl.Web
dotnet user-secrets set "EmailSettings:FromPassword" "senha-de-app" --project src/ExpenseControl.Web
```

Em produção, defina também `App:PublicBaseUrl` (ex.: `https://gastos.exemplo.com`) para que os links enviados não dependam do cabeçalho `Host` da requisição.

## Estrutura

```
ExpenseControl.sln
├── src/
│   ├── ExpenseControl.Core            Regras de negócio, sem dependência de banco nem de UI
│   │   ├── Domain/                    User, LedgerEntry, LedgerReport, regras de conta
│   │   ├── Abstractions/              Interfaces: repositórios, hash de senha, envio de e-mail
│   │   └── Services/                  AuthService, LedgerService
│   ├── ExpenseControl.Infrastructure  Implementações das interfaces
│   │   ├── Persistence/               Repositórios MySQL e migrations (SQL embutido)
│   │   ├── Security/                  Hash de senha (PBKDF2)
│   │   └── Email/                     SMTP e versão "só log" para desenvolvimento
│   └── ExpenseControl.Web             Interface Blazor Server
│       ├── Components/                Pages, Layout (AuthLayout, AppLayout) e componentes compartilhados
│       ├── Localization/              Idiomas: TranslationStore, Formatter e Resources/{pt,en,es}.json
│       ├── Authentication/            Sessão do usuário (AuthenticationStateProvider)
│       ├── Models/                    Modelos de tela (ex.: ChartSpec)
│       └── wwwroot/                   CSS (tema), js/charts.js, js/prefs.js e bibliotecas locais
└── tests/
    └── ExpenseControl.Tests           Testes unitários (xUnit)
```

A dependência vai sempre em um sentido: `Web → Infrastructure → Core`. O `Core` não conhece MySQL, SMTP nem Blazor, então as regras são testáveis sem infraestrutura.

## Como evoluir

- **Novo idioma**: copie `Localization/Resources/en.json` para `xx.json`, traduza e acrescente o idioma em `TranslationStore.Languages`. Um teste falha se faltar alguma chave ou se uma página usar um texto sem tradução.
- **Nova categoria**: acrescente em `Categories` (Core), as traduções `category.{chave}` e, se quiser cor própria nos gráficos, `--viz-cat-{chave}` em `site.css`. Nunca renomeie uma chave existente: ela fica gravada no banco.
- **Novo gráfico**: monte um `ChartSpec` (tipo `columns`, `barsH` ou `line`) e use `<Chart>` dentro de `<ChartCard>`. As cores vêm de variáveis CSS (`--viz-*`), então o modo escuro funciona sem código extra.
- **Nova funcionalidade** (ex.: metas, lançamentos recorrentes): entidade em `Core/Domain`, interface em `Core/Abstractions`, regra em `Core/Services`, implementação em `Infrastructure/Persistence`, página em `Web/Components/Pages` e uma linha no menu em `MainMenu.razor`. Registre serviços em `CoreServiceCollectionExtensions` / `InfrastructureServiceCollectionExtensions`.
- **Mudança no banco**: crie um novo script em `src/ExpenseControl.Infrastructure/Persistence/Migrations` com o próximo número (`004_descricao.sql`). Ele roda uma única vez e fica registrado na tabela `SchemaVersions`. **Nunca edite** um script já publicado.
- **Outro banco ou outro provedor de e-mail**: implemente as interfaces de `Core/Abstractions`; nada em `Core` ou `Web` muda.
- **Versão do .NET**: o `TargetFramework` está em um único lugar, `Directory.Build.props`. Versões de pacotes ficam em `Directory.Packages.props`.

## Testes

```bash
dotnet test
```

Cobrem cadastro, login, recuperação de senha, lançamentos (validação, edição, exclusão, filtros), orçamentos, painel (totais, tendência, categorias, acumulado, orçamentos), exportação CSV, hash de senha, ordem das migrations, **traduções** (mesmas chaves nos 3 idiomas, mesmos parâmetros, nenhum texto usado sem tradução) e formatação por idioma.

Os testes de integração (migrations e consultas SQL reais) só rodam quando a variável `EXPENSECONTROL_TEST_CONNECTION` aponta para um MySQL; sem ela, são ignorados (é o caso do CI). Cada teste cria e apaga um banco temporário. Com o container do `compose.yaml` no ar:

```powershell
$env:EXPENSECONTROL_TEST_CONNECTION = "Server=127.0.0.1;Port=3307;User=root;Password=root_dev_password;"
dotnet test
```

## Segurança

- Senhas com **PBKDF2-SHA256** (600 mil iterações, sal aleatório). Contas criadas pela versão antiga (SHA-256 sem sal) continuam funcionando e são atualizadas automaticamente no próximo login.
- O token de redefinição é aleatório, vale 1 hora, é de uso único e só o **hash** dele fica no banco.
- "Esqueci minha senha" responde igual exista ou não o e-mail, para não revelar quem tem conta.
- A sessão fica no `sessionStorage` do navegador **criptografada e assinada** (Data Protection), vale por até 12 h e termina após 30 min sem atividade. Os prazos ficam em `Session` no `appsettings.json`.
- Em produção com mais de uma instância, compartilhe as chaves do Data Protection entre elas.

### Dados de exemplo

Para ver o painel com dados, crie uma conta e lance despesas/receitas pela própria tela. As datas são calculadas no fuso do servidor.

### Limitações conhecidas

- Não há bloqueio nem limite de tentativas de login.
- A sessão é por aba (como antes): abrir uma nova aba exige entrar de novo.
- O app não converte moedas: o símbolo é apenas um rótulo para os números.
- Ainda não há lançamentos recorrentes, anexos nem importação de extratos.
