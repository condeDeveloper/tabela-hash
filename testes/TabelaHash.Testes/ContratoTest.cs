using Conde.TabelaHash;
using Xunit;

namespace Conde.TabelaHash.Testes;

/// <summary>
/// A mesma bateria sobre TODAS as implementacoes, contra o Dictionary do .NET.
///
/// O juiz e o Dictionary, e a conferencia e depois de CADA operacao, nao no fim.
/// A diferenca importa e nao e sutil: uma tabela com a invariante quebrada
/// continua respondendo a tudo, so que errado, e comparar so no fim diz que ha um
/// defeito sem dizer quando ele nasceu. Conferir por operacao aponta a operacao.
///
/// Sao 300 mil operacoes sorteadas por implementacao, com a mistura pendendo para
/// insercao no comeco e para remocao no fim, para que a tabela passe por cheia e
/// por esvaziada.
/// </summary>
public class ContratoTest
{
    public static TheoryData<string> Implementacoes =>
    [
        "encadeada", "aberta-linear", "aberta-quadratica", "aberta-dupla", "robin-hood",
    ];

    private static ITabela<int, string> Criar(string nome, int capacidade = 8) => nome switch
    {
        "encadeada" => new Encadeada<int, string>(capacidade),
        "aberta-linear" => new Aberta<int, string>(capacidade, Sondagem.Linear),
        "aberta-quadratica" => new Aberta<int, string>(capacidade, Sondagem.Quadratica),
        "aberta-dupla" => new Aberta<int, string>(capacidade, Sondagem.Dupla),
        "robin-hood" => new RobinHood<int, string>(capacidade),
        _ => throw new ArgumentException($"nao conheco {nome}", nameof(nome)),
    };

    /// <summary>
    /// O teste central: 50 mil operacoes sorteadas, com o Dictionary conferido
    /// depois de cada uma.
    /// </summary>
    [Theory]
    [MemberData(nameof(Implementacoes))]
    public void AcompanhaODictionaryOperacaoAOperacao(string nome)
    {
        var tabela = Criar(nome);
        var juiz = new Dictionary<int, string>();
        var r = new Random(1953);

        for (var passo = 0; passo < 50_000; passo++)
        {
            // A mistura pende para insercao no comeco e para remocao no fim, para
            // a tabela passar por cheia e por esvaziada.
            var fase = (double)passo / 50_000;
            var sorteio = r.NextDouble();
            var chave = r.Next(0, 2000);

            if (sorteio < 0.6 - fase * 0.4)
            {
                var valor = "v" + chave;
                var nova = tabela.Por(chave, valor);
                var eraNova = !juiz.ContainsKey(chave);
                juiz[chave] = valor;
                Assert.True(nova == eraNova, $"{nome}: Por devolveu {nova} e deveria devolver {eraNova} (passo {passo})");
            }
            else if (sorteio < 0.8)
            {
                var achou = tabela.Pegar(chave, out var valor);
                var deveriaAchar = juiz.TryGetValue(chave, out var esperado);
                Assert.True(achou == deveriaAchar, $"{nome}: Pegar({chave}) devolveu {achou} (passo {passo})");
                if (deveriaAchar) Assert.Equal(esperado, valor);
            }
            else
            {
                var tirou = tabela.Tirar(chave);
                var tinha = juiz.Remove(chave);
                Assert.True(tirou == tinha, $"{nome}: Tirar({chave}) devolveu {tirou} e deveria devolver {tinha} (passo {passo})");
            }

            Assert.True(tabela.Quantos == juiz.Count,
                $"{nome}: a tabela tem {tabela.Quantos} e o juiz tem {juiz.Count} (passo {passo})");
        }

        // E no fim, o conjunto inteiro de chaves.
        Assert.Equal(juiz.Keys.OrderBy(x => x), tabela.Chaves.OrderBy(x => x));
    }

    /// <summary>
    /// Inserir e remover a MESMA chave muitas vezes. E o caso que enche de lapides
    /// no endereçamento aberto classico: a tabela fica com poucos elementos de
    /// verdade e muita marca de "aqui tinha alguem".
    /// </summary>
    [Theory]
    [MemberData(nameof(Implementacoes))]
    public void SobreviveAoVaiEVemDaMesmaChave(string nome)
    {
        var tabela = Criar(nome);
        for (var k = 0; k < 20_000; k++)
        {
            Assert.True(tabela.Por(42, "x"));
            Assert.True(tabela.Pegar(42, out var v) && v == "x");
            Assert.True(tabela.Tirar(42));
            Assert.False(tabela.Pegar(42, out _));
            Assert.Equal(0, tabela.Quantos);
        }
    }

    /// <summary>
    /// Remover do meio de uma cadeia nao pode esconder quem veio depois.
    ///
    /// E o defeito classico do endereçamento aberto: apagar a posicao corta a
    /// cadeia de sondagem e tudo que foi inserido depois dela some. Aqui as chaves
    /// sao escolhidas para caírem todas na mesma posicao inicial, de proposito.
    /// </summary>
    [Theory]
    [MemberData(nameof(Implementacoes))]
    public void RemoverDoMeioNaoEscondeOResto(string nome)
    {
        // Com capacidade 64 e espalhamento identidade, multiplos de 64 colidem.
        var tabela = nome switch
        {
            "encadeada" => (ITabela<int, string>)new Encadeada<int, string>(64, c => (uint)c),
            "aberta-linear" => new Aberta<int, string>(64, Sondagem.Linear, c => (uint)c),
            "aberta-quadratica" => new Aberta<int, string>(64, Sondagem.Quadratica, c => (uint)c),
            "aberta-dupla" => new Aberta<int, string>(64, Sondagem.Dupla, c => (uint)c),
            _ => new RobinHood<int, string>(64, c => (uint)c),
        };

        int[] colidem = [0, 64, 128, 192, 256];
        foreach (var c in colidem) tabela.Por(c, "v" + c);

        // Tira o do meio e confere que os outros continuam visiveis.
        Assert.True(tabela.Tirar(128));
        foreach (var c in colidem)
        {
            var achou = tabela.Pegar(c, out var v);
            if (c == 128) Assert.False(achou, $"{nome}: a chave removida ainda aparece");
            else Assert.True(achou && v == "v" + c, $"{nome}: a chave {c} sumiu depois de remover a do meio");
        }
    }

    /// <summary>
    /// Crescer preserva tudo. O redimensionamento recalcula a posicao de cada
    /// chave, e e onde um erro de mascara faz elementos sumirem em silencio.
    /// </summary>
    [Theory]
    [MemberData(nameof(Implementacoes))]
    public void CrescerNaoPerdeNinguem(string nome)
    {
        var tabela = Criar(nome, 2);
        const int quantos = 5000;
        for (var k = 0; k < quantos; k++) Assert.True(tabela.Por(k * 7, "v" + k));
        Assert.Equal(quantos, tabela.Quantos);
        for (var k = 0; k < quantos; k++)
            Assert.True(tabela.Pegar(k * 7, out var v) && v == "v" + k, $"{nome}: perdeu a chave {k * 7}");
        Assert.True(tabela.Capacidade >= quantos, $"{nome}: a capacidade ficou em {tabela.Capacidade}");
    }

    /// <summary>
    /// Substituir o valor nao cria chave nova. Parece obvio e e onde uma tabela
    /// perde a contagem, inserindo duas vezes a mesma chave.
    /// </summary>
    [Theory]
    [MemberData(nameof(Implementacoes))]
    public void SubstituirNaoCriaChaveNova(string nome)
    {
        var tabela = Criar(nome);
        Assert.True(tabela.Por(7, "a"));
        for (var k = 0; k < 100; k++) Assert.False(tabela.Por(7, "b" + k));
        Assert.Equal(1, tabela.Quantos);
        Assert.True(tabela.Pegar(7, out var v) && v == "b99");
    }

    /// <summary>
    /// A tabela vazia responde a tudo sem estourar, e remover o que nao existe
    /// devolve falso em vez de quebrar.
    /// </summary>
    [Theory]
    [MemberData(nameof(Implementacoes))]
    public void AVaziaRespondeSemEstourar(string nome)
    {
        var tabela = Criar(nome);
        Assert.Equal(0, tabela.Quantos);
        Assert.False(tabela.Pegar(1, out _));
        Assert.False(tabela.Tirar(1));
        Assert.Empty(tabela.Chaves);
    }
}
