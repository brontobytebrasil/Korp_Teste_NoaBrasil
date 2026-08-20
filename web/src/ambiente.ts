// Endereços dos serviços num lugar só. Em produção isso viria de configuração
// injetada no build; aqui vale o padrão de desenvolvimento.
export const ambiente = {
  estoque: 'http://localhost:5001',
  faturamento: 'http://localhost:5002',
};
