namespace Faturamento;

public class NotaInvalidaException(string mensagem) : Exception(mensagem);

public class NotaNaoEncontradaException() : Exception("Nota não encontrada.");

public class NotaJaFechadaException()
    : Exception("Esta nota já foi impressa. Só é possível imprimir nota aberta.");

/// <summary>
/// O Estoque não respondeu. Diferente de ele ter respondido "não" — são
/// situações distintas e a mensagem ao usuário também precisa ser.
/// </summary>
public class EstoqueIndisponivelException(string mensagem) : Exception(mensagem);

/// <summary>
/// O Estoque respondeu, e a resposta foi uma recusa de negócio: saldo
/// insuficiente, produto inexistente. A mensagem vem de lá e é repassada.
/// </summary>
public class EstoqueRecusouException(string mensagem) : Exception(mensagem);
