namespace Conde.TabelaHash;

/// <summary>
/// Funcoes de espalhamento, da pior a melhor.
///
/// Elas estao todas aqui de proposito, inclusive as ruins: o projeto nao e sobre
/// qual usar, e sobre MEDIR o que cada uma faz com a tabela. Uma funcao ruim nao
/// quebra nada e nao da erro; ela so transforma O(1) em O(n) sem avisar.
/// </summary>
public static class Espalhamento
{
    /// <summary>
    /// A identidade. Serve para mostrar o pior caso sem precisar de adversario:
    /// com chaves multiplas da capacidade, todas caem na mesma posicao.
    /// </summary>
    public static int Identidade(int x) => x;

    /// <summary>
    /// FNV-1a de 32 bits, de Fowler, Noll e Vo.
    ///
    /// E simples, rapida e amplamente usada, e tem um defeito conhecido: ela e
    /// fraca nos bits ALTOS para entradas curtas e sequenciais, o que importa
    /// quando a tabela usa o resto por uma potencia de dois e so olha os bits
    /// baixos.
    /// </summary>
    public static uint Fnv1a(ReadOnlySpan<byte> dados)
    {
        const uint offset = 2166136261;
        const uint primo = 16777619;
        var h = offset;
        foreach (var b in dados)
        {
            h ^= b;
            h *= primo;
        }
        return h;
    }

    public static uint Fnv1a(int x)
    {
        Span<byte> b = stackalloc byte[4];
        BitConverter.TryWriteBytes(b, x);
        return Fnv1a(b);
    }

    /// <summary>
    /// O finalizador do MurmurHash3, que e a mistura de bits usada em quase todo
    /// lugar hoje.
    ///
    /// Sao tres rodadas de deslocar-e-xorar com multiplicacao no meio. O que ele
    /// faz e AVALANCHE: mudar um bit da entrada muda, em media, metade dos bits da
    /// saida. E essa propriedade, e so ela, que a tabela precisa.
    /// </summary>
    public static uint Murmur(uint h)
    {
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return h;
    }

    public static uint Murmur(int x) => Murmur((uint)x);

    /// <summary>
    /// Mistura de 64 bits do SplitMix, que e o que o Java usa no HashMap moderno e
    /// o que vai nos geradores pseudoaleatorios.
    /// </summary>
    public static ulong SplitMix(ulong z)
    {
        z += 0x9e3779b97f4a7c15;
        z = (z ^ (z >> 30)) * 0xbf58476d1ce4e5b9;
        z = (z ^ (z >> 27)) * 0x94d049bb133111eb;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// Mede a AVALANCHE: mudando um bit da entrada, quantos bits da saida mudam,
    /// em media, sobre muitas amostras.
    ///
    /// O ideal e exatamente metade. Abaixo disso a funcao deixa estrutura da
    /// entrada passar para a saida, e e dessa estrutura que nascem as colisoes em
    /// bloco.
    /// </summary>
    public static double MedirAvalanche(Func<uint, uint> funcao, int amostras, int semente)
    {
        ArgumentNullException.ThrowIfNull(funcao);
        var r = new Random(semente);
        long total = 0;
        long casos = 0;
        for (var k = 0; k < amostras; k++)
        {
            var x = (uint)r.Next(int.MinValue, int.MaxValue);
            var hx = funcao(x);
            for (var bit = 0; bit < 32; bit++)
            {
                var mudados = System.Numerics.BitOperations.PopCount(hx ^ funcao(x ^ (1u << bit)));
                total += mudados;
                casos++;
            }
        }
        return (double)total / casos;
    }
}
