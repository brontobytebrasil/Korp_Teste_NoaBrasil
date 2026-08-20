/**
 * Traduz a falha de uma chamada HTTP na frase que vai para o usuário.
 *
 * São três situações diferentes, e a que mais importa é a do meio: quando um
 * serviço está fora, o navegador nem chega a receber resposta e o Angular
 * entrega status 0 com corpo vazio. Sem tratar esse caso, o cenário de falha
 * — que é requisito — aparece na tela como um "não foi possível carregar"
 * genérico, e o usuário não fica sabendo que tem serviço no chão.
 */
export function mensagemDeErro(falha: unknown, alternativa: string): string {
  const resposta = falha as { status?: number; error?: { erro?: string } };

  if (resposta?.error?.erro) return resposta.error.erro;

  if (resposta?.status === 0) {
    return 'Serviço fora do ar. Verifique se os dois microsserviços estão rodando.';
  }

  return alternativa;
}
