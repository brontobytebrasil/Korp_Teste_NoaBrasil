namespace Estoque;

/// <summary>
/// Exceções com significado de negócio. Existem para o tratamento central
/// saber qual código HTTP devolver sem precisar interpretar mensagem de texto.
/// </summary>
public class ProdutoInvalidoException(string mensagem) : Exception(mensagem);

public class CodigoDuplicadoException(string codigo)
    : Exception($"Já existe um produto com o código {codigo}.");

public class ProdutoNaoEncontradoException(string mensagem = "Produto não encontrado.")
    : Exception(mensagem);

public class SaldoInsuficienteException(string mensagem) : Exception(mensagem);

public class ConcorrenciaException()
    : Exception("Outra operação alterou este produto agora. Tente novamente.");
