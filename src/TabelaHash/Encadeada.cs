namespace Conde.TabelaHash;

/// <summary>
/// Tabela por encadeamento: cada posicao guarda uma lista.
///
/// E a mais simples e a mais tolerante. A ocupacao pode passar de 1 sem que nada
/// quebre, porque a lista cresce; remover e trivial, porque so se tira da lista;
/// e o pior caso, com todas as chaves na mesma posicao, degrada para uma busca
/// linear em vez de travar.
///
/// O preco e de memoria e de localidade: cada no e uma alocacao separada, e
/// percorrer a lista pula pela memoria. Em tabela grande isso custa mais que as
/// comparacoes.
/// </summary>
public sealed class Encadeada<TChave, TValor> : ITabela<TChave, TValor> where TChave : notnull
{
    private sealed class No
    {
        public TChave Chave = default!;
        public TValor Valor = default!;
        public No? Proximo;
    }

    private No?[] _vetor;
    private readonly Func<TChave, uint> _espalhar;
    private readonly double _limiteDeOcupacao;

    public Encadeada(int capacidade = 16, Func<TChave, uint>? espalhar = null, double limiteDeOcupacao = 1.0)
    {
        if (capacidade < 1) throw new ArgumentOutOfRangeException(nameof(capacidade));
        if (limiteDeOcupacao <= 0) throw new ArgumentOutOfRangeException(nameof(limiteDeOcupacao));
        _vetor = new No?[PotenciaDeDois(capacidade)];
        _espalhar = espalhar ?? (c => Espalhamento.Murmur((uint)c.GetHashCode()));
        _limiteDeOcupacao = limiteDeOcupacao;
    }

    internal static int PotenciaDeDois(int minimo)
    {
        var n = 1;
        while (n < minimo) n <<= 1;
        return n;
    }

    public int Quantos { get; private set; }
    public int Capacidade => _vetor.Length;
    public long SondasDaUltimaBusca { get; private set; }

    /// <summary>
    /// A posicao. Com capacidade potencia de dois, o resto vira um E de bits, que
    /// e muito mais rapido que a divisao; o preco e so os bits BAIXOS do hash
    /// contarem, e e por isso que o espalhamento precisa misturar de verdade.
    /// </summary>
    private int Posicao(TChave chave) => (int)(_espalhar(chave) & (uint)(_vetor.Length - 1));

    public bool Por(TChave chave, TValor valor)
    {
        var i = Posicao(chave);
        for (var no = _vetor[i]; no is not null; no = no.Proximo)
        {
            if (!EqualityComparer<TChave>.Default.Equals(no.Chave, chave)) continue;
            no.Valor = valor;
            return false;
        }
        _vetor[i] = new No { Chave = chave, Valor = valor, Proximo = _vetor[i] };
        Quantos++;
        if ((double)Quantos / _vetor.Length > _limiteDeOcupacao) Crescer();
        return true;
    }

    public bool Pegar(TChave chave, out TValor valor)
    {
        SondasDaUltimaBusca = 0;
        for (var no = _vetor[Posicao(chave)]; no is not null; no = no.Proximo)
        {
            SondasDaUltimaBusca++;
            if (!EqualityComparer<TChave>.Default.Equals(no.Chave, chave)) continue;
            valor = no.Valor;
            return true;
        }
        valor = default!;
        return false;
    }

    public bool Tirar(TChave chave)
    {
        var i = Posicao(chave);
        No? anterior = null;
        for (var no = _vetor[i]; no is not null; no = no.Proximo)
        {
            if (!EqualityComparer<TChave>.Default.Equals(no.Chave, chave)) { anterior = no; continue; }
            if (anterior is null) _vetor[i] = no.Proximo;
            else anterior.Proximo = no.Proximo;
            Quantos--;
            return true;
        }
        return false;
    }

    public IEnumerable<TChave> Chaves
    {
        get
        {
            foreach (var cabeca in _vetor)
                for (var no = cabeca; no is not null; no = no.Proximo) yield return no.Chave;
        }
    }

    /// <summary>O tamanho da maior lista. Com bom espalhamento fica perto de log n / log log n.</summary>
    public int MaiorLista()
    {
        var maior = 0;
        foreach (var cabeca in _vetor)
        {
            var n = 0;
            for (var no = cabeca; no is not null; no = no.Proximo) n++;
            maior = Math.Max(maior, n);
        }
        return maior;
    }

    private void Crescer()
    {
        var antigo = _vetor;
        _vetor = new No?[antigo.Length * 2];
        foreach (var cabeca in antigo)
        {
            var no = cabeca;
            while (no is not null)
            {
                var proximo = no.Proximo;
                var i = Posicao(no.Chave);
                no.Proximo = _vetor[i];
                _vetor[i] = no;
                no = proximo;
            }
        }
    }
}
