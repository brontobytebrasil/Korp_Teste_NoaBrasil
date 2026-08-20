import { Routes } from '@angular/router';

// Carregamento sob demanda: cada tela vira um pedaço separado do pacote e
// só é baixada quando o usuário entra nela.
export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'produtos' },
  {
    path: 'produtos',
    loadComponent: () => import('./produtos/produtos').then((m) => m.ProdutosComponent),
  },
  {
    path: 'notas',
    loadComponent: () => import('./notas/notas').then((m) => m.NotasComponent),
  },
  {
    path: 'diagnostico',
    loadComponent: () => import('./diagnostico/diagnostico').then((m) => m.DiagnosticoComponent),
  },
  { path: '**', redirectTo: 'produtos' },
];
