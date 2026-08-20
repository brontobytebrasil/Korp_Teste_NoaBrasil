import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ambiente } from '../../ambiente';

interface Passo {
  descricao: string;
  resultado: string;
  ok: boolean;
}

@Component({
  selector: 'app-diagnostico',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './diagnostico.html',
  styleUrl: './diagnostico.css',
})
export class DiagnosticoComponent {
  rodando: string | null = null;
  passos: Record<string, Passo[]> = {};

  // Esta tela usa fetch, e não o HttpClient das outras: aqui o código HTTP da
  // resposta É o resultado que se quer mostrar, inclusive quando é 400 ou 409.
  // O HttpClient trata esses códigos como erro e desvia do fluxo.
  private registrar(teste: string, descricao: string, resultado: string, ok: boolean): void {
    this.passos[teste] = [...(this.passos[teste] ?? []), { descricao, resultado, ok }];
  }

  private async criarProduto(codigo: string, saldo: number): Promise<number> {
    const resposta = await fetch(`${ambiente.estoque}/produtos`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ codigo, descricao: 'Produto criado pela demonstração', saldo }),
    });

    if (!resposta.ok) throw new Error(`não consegui criar o produto ${codigo}`);

    return (await resposta.json()).id;
  }

  private async saldoDe(id: number): Promise<number> {
    const resposta = await fetch(`${ambiente.estoque}/produtos/${id}`);
    return (await resposta.json()).saldo;
  }

  private baixar(produtoId: number, quantidade: number, chave: string) {
    return fetch(`${ambiente.estoque}/saldos/baixa`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': chave },
      body: JSON.stringify({ itens: [{ produtoId, quantidade }] }),
    });
  }

  // Cada teste cria o produto que usa e apaga no fim, para não deixar sobra no
  // catálogo do avaliador.
  private async descartar(teste: string, id: number | undefined): Promise<void> {
    if (!id) return;

    const resposta = await fetch(`${ambiente.estoque}/produtos/${id}`, { method: 'DELETE' }).catch(
      () => null,
    );

    if (!resposta?.ok) {
      this.registrar(teste, 'Limpeza', 'não consegui apagar o produto da demonstração', false);
    }
  }

  /**
   * Chama a baixa duas vezes com a mesma chave e mostra que o saldo cai
   * uma vez só. É o comportamento que protege contra clique duplo e
   * retentativa depois de a rede cair.
   */
  async testarIdempotencia(): Promise<void> {
    const teste = 'idempotencia';
    this.rodando = teste;
    this.passos[teste] = [];

    let criado: number | undefined;

    try {
      const codigo = `DEMO-IDEM-${Date.now().toString().slice(-6)}`;
      criado = await this.criarProduto(codigo, 10);
      this.registrar(teste, `Produto ${codigo} criado`, 'saldo inicial: 10', true);

      const chave = `demo-${criado}`;

      const primeira = await this.baixar(criado, 3, chave);
      this.registrar(
        teste,
        '1ª chamada da baixa (3 unidades)',
        `HTTP ${primeira.status}`,
        primeira.ok,
      );

      const segunda = await this.baixar(criado, 3, chave);
      this.registrar(
        teste,
        '2ª chamada, com a MESMA chave',
        `HTTP ${segunda.status} — resposta guardada, nada executado de novo`,
        segunda.ok,
      );

      const saldo = await this.saldoDe(criado);
      this.registrar(
        teste,
        'Saldo final',
        `${saldo} — caiu uma vez só (10 − 3). Se não fosse idempotente, seria 4.`,
        saldo === 7,
      );

      const semChave = await fetch(`${ambiente.estoque}/saldos/baixa`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ itens: [{ produtoId: criado, quantidade: 1 }] }),
      });
      this.registrar(
        teste,
        'Chamada sem a chave de idempotência',
        `HTTP ${semChave.status} — recusada`,
        semChave.status === 400,
      );
    } catch (erro) {
      this.registrar(teste, 'Falha ao executar', this.motivo(erro), false);
    } finally {
      await this.descartar(teste, criado);
      this.rodando = null;
    }
  }

  /**
   * Duas baixas simultâneas sobre um produto com saldo 1 — o cenário que a
   * especificação cita. Uma conclui, a outra é recusada, e o saldo nunca
   * fica negativo.
   */
  async testarConcorrencia(): Promise<void> {
    const teste = 'concorrencia';
    this.rodando = teste;
    this.passos[teste] = [];

    let criado: number | undefined;

    try {
      const codigo = `DEMO-CONC-${Date.now().toString().slice(-6)}`;
      criado = await this.criarProduto(codigo, 1);
      this.registrar(teste, `Produto ${codigo} criado`, 'saldo inicial: 1', true);

      const [a, b] = await Promise.all([
        this.baixar(criado, 1, `demo-conc-A-${criado}`),
        this.baixar(criado, 1, `demo-conc-B-${criado}`),
      ]);

      const aprovadas = [a, b].filter((r) => r.ok).length;
      const recusadas = [a, b].filter((r) => !r.ok).length;

      this.registrar(
        teste,
        'Duas baixas disparadas ao mesmo tempo',
        `${aprovadas} concluiu, ${recusadas} recusada`,
        aprovadas === 1 && recusadas === 1,
      );

      const recusada = [a, b].find((r) => !r.ok);
      if (recusada) {
        const corpo = await recusada.json();
        this.registrar(
          teste,
          `Mensagem de quem chegou depois (HTTP ${recusada.status})`,
          corpo.erro,
          true,
        );
      }

      const saldo = await this.saldoDe(criado);
      this.registrar(
        teste,
        'Saldo final',
        `${saldo} — nunca negativo. Sem tratamento, as duas passariam e o saldo ficaria −1.`,
        saldo === 0,
      );
    } catch (erro) {
      this.registrar(teste, 'Falha ao executar', this.motivo(erro), false);
    } finally {
      await this.descartar(teste, criado);
      this.rodando = null;
    }
  }

  private motivo(erro: unknown): string {
    const mensagem = (erro as Error)?.message;
    return mensagem
      ? `${mensagem} — verifique se os serviços estão no ar`
      : 'verifique se os serviços estão no ar';
  }
}
