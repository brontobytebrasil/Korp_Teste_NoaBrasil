import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ambiente } from '../ambiente';

export enum StatusNota {
  Aberta = 0,
  Fechada = 1,
}

export interface NotaItem {
  produtoId: number;
  codigo: string;
  quantidade: number;
}

export interface Nota {
  id: number;
  numero: number;
  status: StatusNota;
  criadaEm: string;
  impressaEm: string | null;
  itens: NotaItem[];
}

@Injectable({ providedIn: 'root' })
export class NotaService {
  private readonly http = inject(HttpClient);
  private readonly api = ambiente.faturamento;

  listar(): Observable<Nota[]> {
    return this.http.get<Nota[]>(`${this.api}/notas`);
  }

  // Só o código e a quantidade vão para o servidor. O id do produto é ele
  // quem resolve, contra o catálogo do Estoque.
  criar(itens: NotaItem[]): Observable<Nota> {
    const enxutos = itens.map((i) => ({ codigo: i.codigo, quantidade: i.quantidade }));
    return this.http.post<Nota>(`${this.api}/notas`, { itens: enxutos });
  }

  imprimir(id: number): Observable<Nota> {
    return this.http.post<Nota>(`${this.api}/notas/${id}/imprimir`, {});
  }
}
