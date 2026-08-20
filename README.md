# Sistema de Emissão de Notas Fiscais

Cadastro de produtos, emissão de notas fiscais e baixa de estoque, em arquitetura
de microsserviços.

## Como rodar

```
docker compose up
```

| Serviço | Endereço |
|---|---|
| Aplicação web | http://localhost:4200 |
| Faturamento | http://localhost:5002 |
| Estoque | http://localhost:5001 |
| Banco | localhost:5433 |

O banco é publicado em **5433** e não na 5432 porque a porta padrão costuma já
estar ocupada por uma instalação local do PostgreSQL. Dentro da rede do Compose
os serviços continuam falando com o banco na 5432 — a porta diferente vale só
para quem acessa a partir da máquina.

### Rodando sem Docker

Com o banco de pé pelo Compose, cada serviço sobe direto. Um terminal para cada,
todos a partir da raiz do repositório:

```
docker compose up banco -d

(cd servicos/Estoque     && dotnet run --urls http://localhost:5001)
(cd servicos/Faturamento && dotnet run --urls http://localhost:5002)
(cd web                  && npm start)
```

As migrações são aplicadas automaticamente quando cada serviço sobe.

## Arquitetura

```
web (Angular)
      │
      ▼
Faturamento ──HTTP──► Estoque
      │                  │
      └──────► banco ◄───┘
```

**Faturamento** cuida das notas fiscais. **Estoque** cuida dos produtos e do saldo.
O Faturamento nunca altera saldo direto no banco: ele pede a baixa ao Estoque por
HTTP, enviando uma chave de idempotência.

A separação existe porque saldo é o recurso disputado do sistema. Concentrar a
escrita dele em um serviço só é o que permite tratar concorrência em um lugar
apenas, em vez de espalhar trava por toda a aplicação.

## Funcionalidades

- Cadastro de produtos com código, descrição e saldo
- Nota fiscal com numeração sequencial e múltiplos itens
- Impressão da nota: baixa o saldo dos produtos e fecha a nota
- Nota fechada não pode ser impressa novamente
- Nota que pede mais do que o saldo é recusada já na criação (400)

O saldo é verificado duas vezes, e não é redundância: a checagem da criação
evita nota que nunca vai imprimir; a da impressão, dentro da transação da baixa,
é a que vale — entre criar e imprimir, outra operação pode levar o saldo.

A aba **Demonstração** executa os requisitos opcionais na tela — idempotência e
concorrência — e mostra os números de cada passo. É um artefato de avaliação,
separado do fluxo de uso. Cada teste cria o produto que usa (código `DEMO-*`) e
o apaga no fim.

## Fora do escopo

Autenticação, autorização e multiempresa não foram implementados: o desafio não
pede e incluí-los sem requisito seria inventar regra. O que ficou no lugar delas
foi tratar tudo que é entrada como não confiável — o servidor valida os dados,
resolve o produto pelo catálogo em vez de aceitar o id enviado pela tela, e o
CORS libera apenas a origem da aplicação web.

## Testando o cenário de falha

Com tudo rodando, derrube o serviço de estoque:

```
docker compose stop estoque
```

Tente imprimir uma nota. O esperado: a nota **permanece aberta**, o saldo **não muda**
e a interface exibe a falha. Suba o serviço de novo e repita a impressão — dessa vez
conclui.

Com o Estoque fora, **criar** nota também responde 503: o Faturamento resolve o
produto contra o catálogo dele. Listar notas continua funcionando.

```
docker compose start estoque
```

## Documentação técnica

Decisões de implementação, bibliotecas e tratamento de erros estão em
[docs/detalhamento-tecnico.md](docs/detalhamento-tecnico.md).
