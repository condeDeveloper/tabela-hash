# tabela-hash

Tabelas de espalhamento do zero em C# e .NET 8: encadeamento, endereçamento
aberto com três sondagens, e Robin Hood. O juiz é o `Dictionary` do .NET,
conferido depois de **cada** operação.

```
$ dotnet medidor.dll cauda

  ocupacao   linear media   linear pior   robin media   robin pior
      0,50           1,50            21          1,50            8
      0,70           2,15            87          2,15           16
      0,85           3,78           329          3,78           26
      0,95          12,15         2.367         12,15           59
```

## O juiz confere por operação, não no fim

Uma tabela com a invariante quebrada **continua respondendo** a tudo. Ela devolve
valores, devolve a contagem, não dá exceção — só está errada. Comparar com o juiz
só no fim diz que há um defeito sem dizer quando ele nasceu.

Então o `Dictionary` é conferido depois de cada uma das 50 mil operações
sorteadas, por implementação, com a mistura pendendo para inserção no começo e
para remoção no fim, de modo que a tabela passe por cheia e por esvaziada.

E a mesma bateria roda sobre as **cinco** implementações, pelo mesmo contrato. Os
casos que importam estão todos lá, e nenhum deles aparece por sorteio:

- **remover do meio de uma cadeia** com chaves escolhidas para colidirem de
  propósito — é o defeito clássico do endereçamento aberto, em que apagar a
  posição corta a sondagem e some com tudo que foi inserido depois;
- **o vai e vem da mesma chave** 20 mil vezes, que é o que enche de lápides;
- **crescer de 2 para 5.000 elementos**, porque o redimensionamento recalcula
  cada posição e é ali que um erro de máscara faz elementos sumirem calados.

## A fórmula de livro, medida

```
$ dotnet medidor.dll ocupacao

  ocupacao   linear   formula   dupla   formula   encadeada   formula
      0,30     1,22      1,21    1,19      1,19        1,15      1,15
      0,50     1,49      1,50    1,38      1,39        1,25      1,25
      0,70     2,17      2,17    1,72      1,72        1,35      1,35
      0,80     3,00      3,00    2,00      2,01        1,40      1,40
      0,90     5,35      5,50    2,55      2,56        1,45      1,45
```

As três fórmulas — `(1 + 1/(1−α))/2` para a linear, `ln(1/(1−α))/α` para a dupla,
`1 + α/2` para o encadeamento — batem com a medida até a segunda casa. Elas estão
em todo livro e quase nunca com o número ao lado.

E a leitura delas é o que importa: o custo **não depende do tamanho da tabela**,
só da ocupação. Uma tabela com um bilhão de elementos a 70% de ocupação faz as
mesmas 2,17 sondas que uma com mil.

## Robin Hood não melhora a média. Melhora a cauda

Na tabela do topo, a média do Robin Hood é **idêntica** à da sondagem linear —
1,50, 2,15, 3,78, 12,15 — até a segunda casa, em todas as ocupações. Isso não é
coincidência: é teorema. A soma das distâncias é a mesma, só a distribuição muda.

O que muda é o pior caso. Com 95% de ocupação, a sondagem linear tem um elemento
a **2.367 sondas** da casa dele, e o Robin Hood tem o pior a **59**. Quarenta
vezes melhor, com a mesma média.

E é a cauda que machuca. Uma tabela com média 2 e máximo 200 é pior que uma com
média 3 e máximo 6, porque o caso ruim é o que define a latência de cauda — e é a
cauda que aparece no gráfico de produção, não a média.

A regra também dá um critério de parada de graça: se a posição atual tem alguém
que andou **menos** do que eu já andei, a chave procurada não está na tabela,
porque ela teria tomado aquele lugar na inserção.

## O agrupamento tem nome e tem preço

```
$ dotnet medidor.dll agrupamento

  ocupacao   linear   quadratica   dupla   robin hood
      0,50     1,49         1,43    1,38         1,49
      0,70     2,17         1,84    1,72         2,17
      0,85     3,79         2,46    2,22         3,79
      0,95    10,01         3,60    3,14        10,01
```

- **Linear** forma agrupamento **primário**: dois blocos vizinhos se fundem, e o
  bloco maior atrai mais chaves, num efeito que se realimenta. Em 95% de ocupação
  isso custa 10 sondas por busca.
- **Quadrática** quebra o primário e cria o **secundário**: chaves que caem na
  mesma posição inicial seguem exatamente o mesmo caminho.
- **Dupla** dá a cada chave um passo próprio, e é a única sem agrupamento nenhum.

O passo da sondagem dupla precisa ser **ímpar** com capacidade potência de dois.
Passo par só alcança metade das posições, e a tabela passa a dizer que está cheia
com metade do espaço livre.

## FNV-1a é mensuravelmente fraca

```
$ dotnet medidor.dll avalanche

  funcao        bits mudados   ideal
  identidade            1,00   16,00
  fnv-1a               12,77   16,00
  murmur               16,00   16,00
  splitmix             16,00   16,00
```

Avalanche é: virando **um** bit da entrada, quantos dos 32 bits da saída mudam. O
ideal é exatamente metade, 16.

A identidade dá 1,00, que é o esperado e serve de piso. FNV-1a dá **12,77** — ela
funciona, é rápida, é amplamente usada, e deixa estrutura da entrada passar para a
saída. Murmur e SplitMix dão 16,00 cravado.

Isso importa porque a tabela usa capacidade potência de dois e resto por máscara,
que olha só os **bits baixos**. Com uma função que não mistura de verdade, chaves
sequenciais caem em blocos, e a fórmula da sondagem linear não erra um pouco: ela
deixa de descrever o que acontece.

## A lápide, e o que a medição me corrigiu

Eu ia escrever que a lápide degrada a tabela com o tempo. A medição não mostrou
isso:

```
$ dotnet medidor.dll lapide

  ciclos de vai e vem   elementos   lapides   sondas por busca
                    0       5.000         0               1,18
               10.000       5.000       287               1,08
               50.000       5.000    18.059               1,08
              200.000       5.000     6.397               1,02
```

As lápides **acumulam de verdade** — chegam a 18.059 com só 5.000 elementos vivos
— mas as sondas por busca não pioram. O motivo é que o redimensionamento conta
lápide junto com elemento ao decidir crescer, e recriar a tabela **varre todas as
lápides de uma vez**. É por isso que os números caem de 18.059 para 6.397 entre
as duas últimas linhas: houve um rehash no meio.

Então a afirmação certa é outra, e mais interessante: a degradação por lápide
existe e é limitada pelo rehash, ao preço de rehashes que acontecem por causa de
espaço que não guarda nada. O Robin Hood não paga esse preço, porque remove por
**retrocesso**, puxando a cadeia para trás, e nunca deixa marca.

## As peças

| arquivo | o que faz |
|---|---|
| `ITabela` | o contrato, que é o que permite a mesma bateria rodar em todas |
| `Espalhamento` | identidade, FNV-1a, Murmur, SplitMix, e a medida de avalanche |
| `Encadeada` | uma lista por posição; tolerante, e paga em localidade |
| `Aberta` | tudo no vetor, com as três sondagens e a lápide |
| `RobinHood` | rouba dos ricos; mesma média, cauda muito melhor, sem lápide |

São 30 testes.

## Como rodar

```
dotnet test testes/TabelaHash.Testes/TabelaHash.Testes.csproj -c Release
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- avalanche
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- ocupacao
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- agrupamento
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- cauda
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- lapide
```

## Licença

MIT.
