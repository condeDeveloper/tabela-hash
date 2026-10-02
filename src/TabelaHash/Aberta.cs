namespace Conde.TabelaHash;

/// <summary>Como andar quando a posicao esta ocupada.</summary>
public enum Sondagem
{
    /// <summary>Uma posicao por vez. Simples, e forma blocos.</summary>
    Linear,
    /// <summary>Passos que crescem como quadrado. Quebra os blocos e cria outros.</summary>
    Quadratica,
    /// <summary>Um passo proprio de cada chave, tirado de um segundo hash.</summary>
    Dupla,
}

/// <summary>
/// Tabela por endereçamento aberto: tudo mora no proprio vetor, sem lista
/// nenhuma.
///
/// A vantagem e de localidade, e ela e grande: as posicoes examinadas sao
/// vizinhas na memoria, entao cabem na mesma linha de cache. Em tabela grande
/// isso vale mais que o numero de comparacoes.
///
/// O preco e que remover fica DIFICIL. Apagar uma posicao corta a cadeia de
/// sondagem e esconde tudo que foi inserido depois dela; o conserto classico e a
/// LAPIDE, uma marca de "aqui tinha alguem" que continua deixando a busca passar.
/// E a lapide tem o seu proprio preco: ela ocupa lugar sem guardar nada, e uma
/// tabela que insere e remove muito enche de lapides e degrada mesmo com poucos
/// elementos de verdade.
///
/// Os tres modos de andar estao aqui para serem MEDIDOS, nao escolhidos:
///
/// - **linear** forma agrupamento PRIMARIO: dois blocos vizinhos se fundem, e o
///   bloco maior atrai mais chaves, num efeito que se realimenta;
/// - **quadratica** quebra o primario e cria o SECUNDARIO: chaves que caem na
///   mesma posicao inicial seguem exatamente o mesmo caminho;
/// - **dupla** da a cada chave um passo proprio, e e a unica que nao tem
///   agrupamento nenhum.
/// </summary>
public sealed class Aberta<TChave, TValor> : ITabela<TChave, TValor> where TChave : notnull
{
    private enum Estado : byte { Vazia = 0, Cheia = 1, Lapide = 2 }

    private TChave[] _chaves;
    private TValor[] _valores;
    private Estado[] _estado;
    private readonly Func<TChave, uint> _espalhar;
    private readonly Sondagem _sondagem;
    private readonly double _limiteDeOcupacao;
    private int _lapides;

    public Aberta(int capacidade = 16, Sondagem sondagem = Sondagem.Dupla,
                  Func<TChave, uint>? espalhar = null, double limiteDeOcupacao = 0.7)
    {
        if (capacidade < 1) throw new ArgumentOutOfRangeException(nameof(capacidade));
        if (limiteDeOcupacao is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(limiteDeOcupacao), "em endereçamento aberto a ocupacao fica entre 0 e 1");
        var n = Encadeada<TChave, TValor>.PotenciaDeDois(capacidade);
        _chaves = new TChave[n];
        _valores = new TValor[n];
        _estado = new Estado[n];
        _espalhar = espalhar ?? (c => Espalhamento.Murmur((uint)c.GetHashCode()));
        _sondagem = sondagem;
        _limiteDeOcupacao = limiteDeOcupacao;
    }

    public int Quantos { get; private set; }
    public int Capacidade => _chaves.Length;
    public long SondasDaUltimaBusca { get; private set; }

    /// <summary>Quantas lapides existem. Elas ocupam lugar sem guardar nada.</summary>
    public int Lapides => _lapides;

    private int Inicio(TChave chave) => (int)(_espalhar(chave) & (uint)(_chaves.Length - 1));

    /// <summary>
    /// O passo. Na sondagem dupla ele precisa ser IMPAR para que a sequencia
    /// visite todas as posicoes de um vetor de tamanho potencia de dois: passo par
    /// so alcanca metade delas, e a tabela passa a dizer que esta cheia com
    /// metade do espaco livre.
    /// </summary>
    private int Passo(TChave chave, int k) => _sondagem switch
    {
        Sondagem.Linear => k,
        Sondagem.Quadratica => (k * k + k) / 2,
        _ => k * (int)((Espalhamento.SplitMix((ulong)_espalhar(chave)) | 1) & 0x7FFF),
    };

    public bool Por(TChave chave, TValor valor)
    {
        if ((double)(Quantos + _lapides + 1) / _chaves.Length > _limiteDeOcupacao) Recriar(_chaves.Length * 2);

        var inicio = Inicio(chave);
        var primeiraLapide = -1;
        for (var k = 0; k < _chaves.Length; k++)
        {
            var i = (inicio + Passo(chave, k)) & (_chaves.Length - 1);
            if (_estado[i] == Estado.Lapide) { if (primeiraLapide < 0) primeiraLapide = i; continue; }
            if (_estado[i] == Estado.Vazia)
            {
                // Reaproveita a primeira lapide do caminho: assim a cadeia nao
                // cresce a toa.
                var destino = primeiraLapide >= 0 ? primeiraLapide : i;
                if (primeiraLapide >= 0) _lapides--;
                _chaves[destino] = chave;
                _valores[destino] = valor;
                _estado[destino] = Estado.Cheia;
                Quantos++;
                return true;
            }
            if (!EqualityComparer<TChave>.Default.Equals(_chaves[i], chave)) continue;
            _valores[i] = valor;
            return false;
        }
        throw new InvalidOperationException("a sondagem nao achou lugar; o passo nao cobre a tabela");
    }

    public bool Pegar(TChave chave, out TValor valor)
    {
        SondasDaUltimaBusca = 0;
        var inicio = Inicio(chave);
        for (var k = 0; k < _chaves.Length; k++)
        {
            var i = (inicio + Passo(chave, k)) & (_chaves.Length - 1);
            SondasDaUltimaBusca++;
            // Vazia ENCERRA a busca; lapide NAO encerra. E essa distincao que faz
            // a remocao funcionar.
            if (_estado[i] == Estado.Vazia) break;
            if (_estado[i] == Estado.Lapide) continue;
            if (!EqualityComparer<TChave>.Default.Equals(_chaves[i], chave)) continue;
            valor = _valores[i];
            return true;
        }
        valor = default!;
        return false;
    }

    public bool Tirar(TChave chave)
    {
        var inicio = Inicio(chave);
        for (var k = 0; k < _chaves.Length; k++)
        {
            var i = (inicio + Passo(chave, k)) & (_chaves.Length - 1);
            if (_estado[i] == Estado.Vazia) return false;
            if (_estado[i] == Estado.Lapide) continue;
            if (!EqualityComparer<TChave>.Default.Equals(_chaves[i], chave)) continue;
            _estado[i] = Estado.Lapide;
            _chaves[i] = default!;
            _valores[i] = default!;
            Quantos--;
            _lapides++;
            return true;
        }
        return false;
    }

    public IEnumerable<TChave> Chaves
    {
        get
        {
            for (var i = 0; i < _chaves.Length; i++)
                if (_estado[i] == Estado.Cheia) yield return _chaves[i];
        }
    }

    /// <summary>Refaz a tabela do zero, o que tambem varre as lapides.</summary>
    private void Recriar(int capacidade)
    {
        var chaves = _chaves;
        var valores = _valores;
        var estado = _estado;
        var n = Encadeada<TChave, TValor>.PotenciaDeDois(Math.Max(capacidade, Quantos * 2));
        _chaves = new TChave[n];
        _valores = new TValor[n];
        _estado = new Estado[n];
        Quantos = 0;
        _lapides = 0;
        for (var i = 0; i < chaves.Length; i++)
            if (estado[i] == Estado.Cheia) Por(chaves[i], valores[i]);
    }
}
