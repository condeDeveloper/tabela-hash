using System.Globalization;
using Conde.TabelaHash;

// A ferramenta RODA para registrar numeros. Nao ha asseveracao de tempo: os
// numeros que entram no README saem daqui, nunca da minha cabeca.

// Com InvariantGlobalization a cultura pt-BR nao existe e lancaria. O formato vai
// montado na mao, que e o padrao ja adotado nos outros projetos.
var br = new NumberFormatInfo { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };
string N(long x) => x.ToString("N0", br);
string D(double x, int casas = 2) => x.ToString("N" + casas, br);

var comando = args.Length > 0 ? args[0] : "tudo";
switch (comando)
{
    case "ocupacao": Ocupacao(); break;
    case "agrupamento": Agrupamento(); break;
    case "cauda": Cauda(); break;
    case "avalanche": Avalanche(); break;
    case "lapide": Lapide(); break;
    default: Avalanche(); Console.WriteLine(); Ocupacao(); Console.WriteLine(); Agrupamento(); Console.WriteLine(); Cauda(); Console.WriteLine(); Lapide(); break;
}

return;

// A formula de livro para a sondagem linear, contra a medida.
void Ocupacao()
{
    Console.WriteLine("sondas por busca contra a ocupacao, e a formula de livro");
    Console.WriteLine();
    Console.WriteLine("  ocupacao   linear   formula   dupla   formula   encadeada   formula");
    foreach (var alvo in new[] { 0.30, 0.50, 0.70, 0.80, 0.90 })
    {
        const int capacidade = 1 << 16;
        var quantos = (int)(capacidade * alvo);

        var linear = Medir(new Aberta<int, int>(capacidade, Sondagem.Linear, limiteDeOcupacao: 0.99), quantos);
        var dupla = Medir(new Aberta<int, int>(capacidade, Sondagem.Dupla, limiteDeOcupacao: 0.99), quantos);
        var encadeada = Medir(new Encadeada<int, int>(capacidade, limiteDeOcupacao: 10), quantos);

        // Busca com SUCESSO. Linear: (1 + 1/(1-a))/2. Dupla: ln(1/(1-a))/a.
        // Encadeada: 1 + a/2.
        var fLinear = (1 + 1 / (1 - alvo)) / 2;
        var fDupla = Math.Log(1 / (1 - alvo)) / alvo;
        var fEncadeada = 1 + alvo / 2;

        Console.WriteLine($"  {D(alvo),8}   {D(linear),6}   {D(fLinear),7}   {D(dupla),5}   {D(fDupla),7}   {D(encadeada),9}   {D(fEncadeada),7}");
    }
    Console.WriteLine();
    Console.WriteLine("  A formula da sondagem linear so vale com espalhamento bom. Com espalhamento");
    Console.WriteLine("  ruim ela nao erra um pouco: ela deixa de descrever o que acontece.");
}

double Medir(ITabela<int, int> tabela, int quantos)
{
    var r = new Random(1953);
    var chaves = new int[quantos];
    for (var k = 0; k < quantos; k++) { chaves[k] = r.Next(); tabela.Por(chaves[k], k); }
    long sondas = 0;
    foreach (var c in chaves) { tabela.Pegar(c, out _); sondas += tabela.SondasDaUltimaBusca; }
    return (double)sondas / quantos;
}

// Agrupamento: o que a sondagem linear faz com os blocos, e o que a dupla nao faz.
void Agrupamento()
{
    Console.WriteLine("agrupamento: o preco de cada jeito de andar");
    Console.WriteLine();
    Console.WriteLine("  ocupacao   linear   quadratica   dupla   robin hood");
    foreach (var alvo in new[] { 0.50, 0.70, 0.85, 0.95 })
    {
        const int capacidade = 1 << 16;
        var quantos = (int)(capacidade * alvo);
        var linear = Medir(new Aberta<int, int>(capacidade, Sondagem.Linear, limiteDeOcupacao: 0.99), quantos);
        var quad = Medir(new Aberta<int, int>(capacidade, Sondagem.Quadratica, limiteDeOcupacao: 0.99), quantos);
        var dupla = Medir(new Aberta<int, int>(capacidade, Sondagem.Dupla, limiteDeOcupacao: 0.99), quantos);
        var robin = Medir(new RobinHood<int, int>(capacidade, limiteDeOcupacao: 0.99), quantos);
        Console.WriteLine($"  {D(alvo),8}   {D(linear),6}   {D(quad),10}   {D(dupla),5}   {D(robin),10}");
    }
}

// A cauda: Robin Hood nao melhora a media, melhora o PIOR CASO.
void Cauda()
{
    Console.WriteLine("robin hood: a media nao melhora, a cauda sim");
    Console.WriteLine();
    Console.WriteLine("  ocupacao   linear media   linear pior   robin media   robin pior");
    foreach (var alvo in new[] { 0.50, 0.70, 0.85, 0.95 })
    {
        const int capacidade = 1 << 16;
        var quantos = (int)(capacidade * alvo);
        var r = new Random(1986);

        var linear = new Aberta<int, int>(capacidade, Sondagem.Linear, limiteDeOcupacao: 0.99);
        var robin = new RobinHood<int, int>(capacidade, limiteDeOcupacao: 0.99);
        var chaves = new int[quantos];
        for (var k = 0; k < quantos; k++) { chaves[k] = r.Next(); linear.Por(chaves[k], k); robin.Por(chaves[k], k); }

        long somaLinear = 0;
        long piorLinear = 0;
        foreach (var c in chaves)
        {
            linear.Pegar(c, out _);
            somaLinear += linear.SondasDaUltimaBusca;
            piorLinear = Math.Max(piorLinear, linear.SondasDaUltimaBusca);
        }
        Console.WriteLine($"  {D(alvo),8}   {D((double)somaLinear / quantos),12}   {N(piorLinear),11}   {D(robin.DistanciaMedia() + 1),11}   {N(robin.MaiorDistancia() + 1),10}");
    }
    Console.WriteLine();
    Console.WriteLine("  A media e a mesma por teorema. E a cauda que define a latencia que aparece");
    Console.WriteLine("  no grafico de producao.");
}

// Avalanche: mudar um bit da entrada deveria mudar metade dos bits da saida.
void Avalanche()
{
    Console.WriteLine("avalanche: quantos dos 32 bits mudam ao virar UM bit da entrada");
    Console.WriteLine();
    Console.WriteLine("  funcao        bits mudados   ideal");
    foreach (var (nome, f) in new (string, Func<uint, uint>)[]
             {
                 ("identidade", x => x),
                 ("fnv-1a", x => Espalhamento.Fnv1a((int)x)),
                 ("murmur", Espalhamento.Murmur),
                 ("splitmix", x => (uint)Espalhamento.SplitMix(x)),
             })
    {
        Console.WriteLine($"  {nome,-12}  {D(Espalhamento.MedirAvalanche(f, 2000, 7)),12}   {D(16.0),5}");
    }
    Console.WriteLine();
    Console.WriteLine("  Abaixo de 16 a funcao deixa estrutura da entrada passar para a saida, e e");
    Console.WriteLine("  dessa estrutura que nascem as colisoes em bloco.");
}

// Lapides: o preco de remover em endereçamento aberto.
void Lapide()
{
    Console.WriteLine("lapides: o preco de remover em endereçamento aberto");
    Console.WriteLine();
    Console.WriteLine("  ciclos de vai e vem   elementos   lapides   sondas por busca");
    var tabela = new Aberta<int, int>(1 << 14, Sondagem.Dupla, limiteDeOcupacao: 0.7);
    var r = new Random(1962);
    var vivas = new List<int>();
    for (var k = 0; k < 5000; k++) { var c = r.Next(); tabela.Por(c, k); vivas.Add(c); }

    foreach (var ciclos in new[] { 0, 10_000, 50_000, 200_000 })
    {
        for (var k = 0; k < ciclos; k++)
        {
            var i = r.Next(vivas.Count);
            tabela.Tirar(vivas[i]);
            var nova = r.Next();
            tabela.Por(nova, k);
            vivas[i] = nova;
        }
        long sondas = 0;
        foreach (var c in vivas) { tabela.Pegar(c, out _); sondas += tabela.SondasDaUltimaBusca; }
        Console.WriteLine($"  {N(ciclos),19}   {N(tabela.Quantos),9}   {N(tabela.Lapides),7}   {D((double)sondas / vivas.Count),16}");
    }
    Console.WriteLine();
    Console.WriteLine("  A lapide ocupa lugar sem guardar nada. O Robin Hood nao tem esse problema:");
    Console.WriteLine("  ele remove por retrocesso, puxando a cadeia para tras.");
}
