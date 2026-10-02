namespace Conde.TabelaHash;

/// <summary>
/// Robin Hood, de Celis (1986): rouba dos ricos e da aos pobres.
///
/// E endereçamento aberto com sondagem linear e UMA regra a mais: ao inserir, se
/// a posicao esta ocupada por alguem que andou MENOS que o candidato, os dois
/// trocam de lugar e quem foi deslocado continua a busca.
///
/// A regra parece arbitraria e tem uma consequencia forte: ela nao reduz o numero
/// MEDIO de sondas, que continua sendo o mesmo da sondagem linear, mas reduz
/// drasticamente a VARIANCIA. Nenhum elemento fica muito longe da casa dele.
///
/// E a variancia e o que machuca na pratica. Uma tabela com media 2 e maximo 200
/// e pior que uma com media 3 e maximo 6, porque o caso ruim e o que define a
/// latencia de cauda, e e a cauda que aparece no grafico de producao.
///
/// A regra tambem da um criterio de parada de graca na busca: se a posicao atual
/// tem alguem que andou MENOS do que eu ja andei, a chave procurada nao esta na
/// tabela. Nao e preciso andar ate achar um vazio.
/// </summary>
public sealed class RobinHood<TChave, TValor> : ITabela<TChave, TValor> where TChave : notnull
{
    private TChave[] _chaves;
    private TValor[] _valores;
    private int[] _distancia;    // -1 = vazia; senao, quanto andou desde a casa
    private readonly Func<TChave, uint> _espalhar;
    private readonly double _limiteDeOcupacao;

    public RobinHood(int capacidade = 16, Func<TChave, uint>? espalhar = null, double limiteDeOcupacao = 0.9)
    {
        if (capacidade < 1) throw new ArgumentOutOfRangeException(nameof(capacidade));
        if (limiteDeOcupacao is <= 0 or >= 1) throw new ArgumentOutOfRangeException(nameof(limiteDeOcupacao));
        var n = Encadeada<TChave, TValor>.PotenciaDeDois(capacidade);
        _chaves = new TChave[n];
        _valores = new TValor[n];
        _distancia = new int[n];
        Array.Fill(_distancia, -1);
        _espalhar = espalhar ?? (c => Espalhamento.Murmur((uint)c.GetHashCode()));
        _limiteDeOcupacao = limiteDeOcupacao;
    }

    public int Quantos { get; private set; }
    public int Capacidade => _chaves.Length;
    public long SondasDaUltimaBusca { get; private set; }

    private int Casa(TChave chave) => (int)(_espalhar(chave) & (uint)(_chaves.Length - 1));

    public bool Por(TChave chave, TValor valor)
    {
        if ((double)(Quantos + 1) / _chaves.Length > _limiteDeOcupacao) Recriar();

        var i = Casa(chave);
        var distancia = 0;
        while (true)
        {
            if (_distancia[i] < 0)
            {
                _chaves[i] = chave; _valores[i] = valor; _distancia[i] = distancia;
                Quantos++;
                return true;
            }
            if (_distancia[i] == distancia && EqualityComparer<TChave>.Default.Equals(_chaves[i], chave))
            {
                _valores[i] = valor;
                return false;
            }
            // O rico e quem andou menos. Ele cede o lugar e sai procurando.
            if (_distancia[i] < distancia)
            {
                (chave, _chaves[i]) = (_chaves[i], chave);
                (valor, _valores[i]) = (_valores[i], valor);
                (distancia, _distancia[i]) = (_distancia[i], distancia);
            }
            i = (i + 1) & (_chaves.Length - 1);
            distancia++;
        }
    }

    public bool Pegar(TChave chave, out TValor valor)
    {
        SondasDaUltimaBusca = 0;
        var i = Casa(chave);
        var distancia = 0;
        while (true)
        {
            SondasDaUltimaBusca++;
            if (_distancia[i] < 0) break;
            // Criterio de parada de graca: quem esta aqui andou menos do que eu ja
            // andei, entao a minha chave teria tomado este lugar na insercao.
            if (_distancia[i] < distancia) break;
            if (_distancia[i] == distancia && EqualityComparer<TChave>.Default.Equals(_chaves[i], chave))
            {
                valor = _valores[i];
                return true;
            }
            i = (i + 1) & (_chaves.Length - 1);
            distancia++;
            if (distancia > _chaves.Length) break;
        }
        valor = default!;
        return false;
    }

    /// <summary>
    /// Remocao por RETROCESSO, sem lapide nenhuma: puxa-se para tras quem veio
    /// depois, enquanto a distancia for maior que zero. Isso mantem a tabela
    /// limpa, o que o endereçamento aberto classico nao consegue.
    /// </summary>
    public bool Tirar(TChave chave)
    {
        var i = Casa(chave);
        var distancia = 0;
        while (true)
        {
            if (_distancia[i] < 0) return false;
            if (_distancia[i] < distancia) return false;
            if (_distancia[i] == distancia && EqualityComparer<TChave>.Default.Equals(_chaves[i], chave)) break;
            i = (i + 1) & (_chaves.Length - 1);
            distancia++;
            if (distancia > _chaves.Length) return false;
        }

        _distancia[i] = -1;
        _chaves[i] = default!;
        _valores[i] = default!;
        Quantos--;

        // Puxa a cadeia para tras ate achar um vazio ou alguem que ja esta em casa.
        var anterior = i;
        var atual = (i + 1) & (_chaves.Length - 1);
        while (_distancia[atual] > 0)
        {
            _chaves[anterior] = _chaves[atual];
            _valores[anterior] = _valores[atual];
            _distancia[anterior] = _distancia[atual] - 1;
            _distancia[atual] = -1;
            _chaves[atual] = default!;
            _valores[atual] = default!;
            anterior = atual;
            atual = (atual + 1) & (_chaves.Length - 1);
        }
        return true;
    }

    public IEnumerable<TChave> Chaves
    {
        get
        {
            for (var i = 0; i < _chaves.Length; i++)
                if (_distancia[i] >= 0) yield return _chaves[i];
        }
    }

    /// <summary>A maior distancia da casa. E a cauda, e e ela que o Robin Hood corta.</summary>
    public int MaiorDistancia()
    {
        var maior = 0;
        foreach (var d in _distancia) maior = Math.Max(maior, d);
        return maior;
    }

    /// <summary>A distancia media. Ela NAO melhora com Robin Hood; a variancia sim.</summary>
    public double DistanciaMedia()
    {
        if (Quantos == 0) return 0;
        long soma = 0;
        foreach (var d in _distancia) if (d >= 0) soma += d;
        return (double)soma / Quantos;
    }

    private void Recriar()
    {
        var chaves = _chaves;
        var valores = _valores;
        var distancia = _distancia;
        var n = _chaves.Length * 2;
        _chaves = new TChave[n];
        _valores = new TValor[n];
        _distancia = new int[n];
        Array.Fill(_distancia, -1);
        Quantos = 0;
        for (var i = 0; i < chaves.Length; i++)
            if (distancia[i] >= 0) Por(chaves[i], valores[i]);
    }
}
