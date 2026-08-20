import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Subject, takeUntil } from 'rxjs';
import { mensagemDeErro } from '../erros';
import { Nota, NotaItem, NotaService, StatusNota } from '../nota.service';
import { Produto, ProdutoService } from '../produto.service';

@Component({
  selector: 'app-notas',
  standalone: true,
  imports: [
    FormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './notas.html',
  styleUrl: './notas.css',
})
export class NotasComponent implements OnInit, OnDestroy {
  private readonly notaService = inject(NotaService);
  private readonly produtoService = inject(ProdutoService);
  private readonly aviso = inject(MatSnackBar);
  private readonly encerrar$ = new Subject<void>();

  readonly StatusNota = StatusNota;
  readonly colunas = ['numero', 'itens', 'status', 'acoes'];

  notas: Nota[] = [];
  produtos: Produto[] = [];
  carregando = true;

  // Itens da nota que está sendo montada, antes de salvar.
  rascunho: NotaItem[] = [];
  produtoSelecionado: number | null = null;
  quantidade = 1;

  // Guarda o id da nota em impressão. É o que permite mostrar o indicador
  // de processamento na linha certa, e não na tabela inteira.
  imprimindo: number | null = null;
  salvando = false;

  ngOnInit(): void {
    this.carregar();
  }

  ngOnDestroy(): void {
    this.encerrar$.next();
    this.encerrar$.complete();
  }

  private carregar(): void {
    this.carregando = true;

    this.produtoService
      .listar()
      .pipe(takeUntil(this.encerrar$))
      .subscribe({
        next: (lista) => (this.produtos = lista),
        error: (falha) => this.mostrarErro(falha, 'Não foi possível carregar os produtos.'),
      });

    this.notaService
      .listar()
      .pipe(takeUntil(this.encerrar$))
      .subscribe({
        next: (lista) => {
          this.notas = lista;
          this.carregando = false;
        },
        error: (falha) => {
          this.mostrarErro(falha, 'Não foi possível carregar as notas.');
          this.carregando = false;
        },
      });
  }

  adicionarItem(): void {
    const produto = this.produtos.find((p) => p.id === this.produtoSelecionado);
    if (!produto?.id || this.quantidade < 1) return;

    const existente = this.rascunho.find((i) => i.produtoId === produto.id);
    const total = (existente?.quantidade ?? 0) + this.quantidade;

    // O servidor recusa nota acima do saldo. Barrar aqui é só para o usuário
    // não montar a nota inteira e descobrir no botão de criar.
    if (total > produto.saldo) {
      this.aviso.open(
        `${produto.codigo} tem saldo ${produto.saldo}. Não dá para pedir ${total}.`,
        'Fechar',
        { duration: 5000 },
      );
      return;
    }

    if (existente) {
      existente.quantidade = total;
    } else {
      this.rascunho = [
        ...this.rascunho,
        { produtoId: produto.id, codigo: produto.codigo, quantidade: this.quantidade },
      ];
    }

    this.produtoSelecionado = null;
    this.quantidade = 1;
  }

  removerItem(item: NotaItem): void {
    this.rascunho = this.rascunho.filter((i) => i !== item);
  }

  criarNota(): void {
    if (this.rascunho.length === 0) {
      this.aviso.open('Adicione ao menos um item à nota.', 'Fechar', { duration: 3000 });
      return;
    }

    this.salvando = true;
    this.notaService
      .criar(this.rascunho)
      .pipe(takeUntil(this.encerrar$))
      .subscribe({
        next: (nota) => {
          this.aviso.open(`Nota ${nota.numero} criada.`, 'Fechar', { duration: 3000 });
          this.rascunho = [];
          this.salvando = false;
          this.carregar();
        },
        error: (falha) => {
          this.mostrarErro(falha, 'Não foi possível criar a nota.');
          this.salvando = false;
        },
      });
  }

  imprimir(nota: Nota): void {
    this.imprimindo = nota.id;

    this.notaService
      .imprimir(nota.id)
      .pipe(takeUntil(this.encerrar$))
      .subscribe({
        next: () => {
          this.aviso.open(`Nota ${nota.numero} impressa e fechada.`, 'Fechar', { duration: 3000 });
          this.imprimindo = null;
          this.carregar();
        },
        error: (falha) => {
          // A nota continua aberta e o saldo intacto — a mensagem vem do
          // servidor e diz exatamente o que impediu.
          this.mostrarErro(falha, 'Não foi possível imprimir a nota.');
          this.imprimindo = null;
        },
      });
  }

  totalItens(nota: Nota): number {
    return nota.itens.reduce((soma, item) => soma + item.quantidade, 0);
  }

  private mostrarErro(falha: unknown, alternativa: string): void {
    this.aviso.open(mensagemDeErro(falha, alternativa), 'Fechar', { duration: 6000 });
  }
}
