namespace Conde.TabelaHash;

/// <summary>
/// O contrato que todas as tabelas daqui cumprem.
///
/// E ele que permite o teste de CONTRATO: a mesma bateria roda sobre todas as
/// implementacoes, e cada uma e conferida contra o Dictionary do .NET depois de
/// CADA operacao, nao no fim. A diferenca importa: uma tabela com a invariante
/// quebrada continua respondendo, e so a conferencia por operacao acha o momento
/// em que ela quebrou.
/// </summary>
public interface ITabela<TChave, TValor> where TChave : notnull
{
    /// <summary>Quantos pares existem.</summary>
    int Quantos { get; }

    /// <summary>Quantas posicoes o vetor tem. Serve para medir a ocupacao.</summary>
    int Capacidade { get; }

    /// <summary>
    /// A ocupacao: quantos elementos por posicao. E o unico numero que importa
    /// para o desempenho, e e por isso que ele e exposto.
    /// </summary>
    double Ocupacao => Capacidade == 0 ? 0 : (double)Quantos / Capacidade;

    /// <summary>Insere ou substitui. Devolve verdadeiro quando a chave e nova.</summary>
    bool Por(TChave chave, TValor valor);

    /// <summary>Busca. Devolve falso quando a chave nao esta la.</summary>
    bool Pegar(TChave chave, out TValor valor);

    /// <summary>Remove. Devolve verdadeiro quando havia o que remover.</summary>
    bool Tirar(TChave chave);

    /// <summary>Todas as chaves, em ordem nao especificada.</summary>
    IEnumerable<TChave> Chaves { get; }

    /// <summary>
    /// Quantas posicoes foram examinadas na ultima operacao de busca.
    ///
    /// Esse numero e o assunto do projeto inteiro: e ele que separa uma tabela boa
    /// de uma ruim, e e ele que as formulas de livro preveem. Expo-lo permite
    /// comparar a previsao com a medida.
    /// </summary>
    long SondasDaUltimaBusca { get; }
}
