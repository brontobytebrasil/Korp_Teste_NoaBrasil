import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ambiente } from '../ambiente';

export interface Produto {
  id?: number;
  codigo: string;
  descricao: string;
  saldo: number;
}

@Injectable({ providedIn: 'root' })
export class ProdutoService {
  private readonly http = inject(HttpClient);

  // Produto é assunto do Estoque, então o front fala com ele direto.
  // O Faturamento só entra em cena quando a nota precisa baixar saldo —
  // aí a chamada é entre serviços, não do navegador.
  private readonly api = `${ambiente.estoque}/produtos`;

  listar(): Observable<Produto[]> {
    return this.http.get<Produto[]>(this.api);
  }

  criar(produto: Produto): Observable<Produto> {
    return this.http.post<Produto>(this.api, produto);
  }

  atualizar(id: number, produto: Produto): Observable<Produto> {
    return this.http.put<Produto>(`${this.api}/${id}`, produto);
  }

  remover(id: number): Observable<void> {
    return this.http.delete<void>(`${this.api}/${id}`);
  }
}
