import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { Subject, takeUntil } from 'rxjs';
import { mensagemDeErro } from '../erros';
import { Produto, ProdutoService } from '../produto.service';

@Component({
  selector: 'app-produtos',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
  ],
  templateUrl: './produtos.html',
  styleUrl: './produtos.css',
})
export class ProdutosComponent implements OnInit, OnDestroy {
  private readonly produtoService = inject(ProdutoService);
  private readonly fb = inject(FormBuilder);
  private readonly aviso = inject(MatSnackBar);

  // Um único Subject encerra todas as inscrições quando o componente sai
  // de cena. É o que evita resposta chegando para componente já destruído.
  private readonly encerrar$ = new Subject<void>();

  readonly colunas = ['codigo', 'descricao', 'saldo', 'acoes'];

  produtos: Produto[] = [];
  carregando = true;
  salvando = false;
  emEdicao: number | null = null;
  confirmandoRemocao: number | null = null;

  readonly formulario = this.fb.nonNullable.group({
    codigo: ['', [Validators.required, Validators.maxLength(40)]],
    descricao: ['', [Validators.required, Validators.maxLength(200)]],
    saldo: [0, [Validators.required, Validators.min(0)]],
  });

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
        next: (lista) => {
          this.produtos = lista;
          this.carregando = false;
        },
        error: (falha) => {
          this.mostrarErro(falha, 'Não foi possível carregar os produtos.');
          this.carregando = false;
        },
      });
  }

  salvar(): void {
    if (this.formulario.invalid) {
      this.formulario.markAllAsTouched();
      return;
    }

    this.salvando = true;
    const produto = this.formulario.getRawValue();

    const requisicao = this.emEdicao
      ? this.produtoService.atualizar(this.emEdicao, produto)
      : this.produtoService.criar(produto);

    requisicao.pipe(takeUntil(this.encerrar$)).subscribe({
      next: () => {
        this.aviso.open(this.emEdicao ? 'Produto atualizado.' : 'Produto cadastrado.', 'Fechar', {
          duration: 3000,
        });
        this.cancelar();
        this.carregar();
      },
      error: (falha) => {
        this.mostrarErro(falha, 'Não foi possível salvar o produto.');
        this.salvando = false;
      },
    });
  }

  editar(produto: Produto): void {
    this.emEdicao = produto.id ?? null;
    this.formulario.setValue({
      codigo: produto.codigo,
      descricao: produto.descricao,
      saldo: produto.saldo,
    });
  }

  // Exclusão é o único botão sem volta desta tela, então ela pede confirmação
  // na própria linha antes de chamar a API.
  pedirConfirmacao(produto: Produto): void {
    this.confirmandoRemocao = produto.id ?? null;
  }

  desistirDaRemocao(): void {
    this.confirmandoRemocao = null;
  }

  remover(produto: Produto): void {
    if (!produto.id) return;

    this.confirmandoRemocao = null;

    this.produtoService
      .remover(produto.id)
      .pipe(takeUntil(this.encerrar$))
      .subscribe({
        next: () => {
          this.aviso.open('Produto removido.', 'Fechar', { duration: 3000 });
          this.carregar();
        },
        error: (falha) => this.mostrarErro(falha, 'Não foi possível remover o produto.'),
      });
  }

  cancelar(): void {
    this.emEdicao = null;
    this.salvando = false;
    this.formulario.reset({ codigo: '', descricao: '', saldo: 0 });
  }

  private mostrarErro(falha: unknown, alternativa: string): void {
    this.aviso.open(mensagemDeErro(falha, alternativa), 'Fechar', { duration: 5000 });
  }
}
